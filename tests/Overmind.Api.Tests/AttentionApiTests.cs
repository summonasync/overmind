using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AttentionApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _sandboxDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AttentionApiTests()
    {
        _client = _factory.CreateClient();
        _sandboxDir = Path.Combine(Path.GetTempPath(), $"overmind-attention-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sandboxDir);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();

        if (Directory.Exists(_sandboxDir))
        {
            Directory.Delete(_sandboxDir, recursive: true);
        }
    }

    [Fact]
    public async Task Ceremonies_ReasonsPerFixture_DoneExcludedAndUrlInDeepLinkFormat()
    {
        var project = await CreateProjectAsync();

        await CreateFeature(project.Id, "101-spec-wait", "Spec waits");
        await CreateArtifact(project.Id, "101-spec-wait", "spec");

        await CreateFeature(project.Id, "102-plan-wait", "Plan waits");
        var spec = await CreateArtifact(project.Id, "102-plan-wait", "spec");
        await ValidateArtifact(spec);
        await CreateArtifact(project.Id, "102-plan-wait", "plan");

        await CreateFeature(project.Id, "103-plan-quiet", "Plan without spec");
        await CreateArtifact(project.Id, "103-plan-quiet", "plan");

        await CreateFeature(project.Id, "104-result-wait", "Result waits");
        await BringToStatusAsync(project.Id, "104-result-wait", "review");

        await CreateFeature(project.Id, "105-done-quiet", "Already done");
        await BringToStatusAsync(project.Id, "105-done-quiet", "done");

        var ceremonies = await CeremoniesAsync();
        var rows = ceremonies.Features.Where(f => f.ProjectId == project.Id).ToArray();

        rows.Length.ShouldBe(3);
        rows.Single(f => f.FeatureSlug == "101-spec-wait").Reasons
            .ShouldBe(new[] { "spec awaiting validation" });
        rows.Single(f => f.FeatureSlug == "102-plan-wait").Reasons
            .ShouldBe(new[] { "plan awaiting validation" });
        rows.Single(f => f.FeatureSlug == "104-result-wait").Reasons
            .ShouldBe(new[] { "result awaiting validation" });

        var row = rows.Single(f => f.FeatureSlug == "101-spec-wait");
        row.Number.ShouldBe("101");
        row.Title.ShouldBe("Spec waits");
        row.ProjectName.ShouldBe(project.Name);
        row.Url.ShouldBe($"http://overmind.localhost/dashboard?project={project.Id}&feature=101-spec-wait");
    }

    [Fact]
    public async Task Ceremonies_BlockedFeature_CountsOpenBlockersOnly()
    {
        var project = await CreateProjectAsync();
        await CreateFeature(project.Id, "201-blocked", "Blocked feature");
        await CreateFeature(project.Id, "202-open-a", "Open blocker A");
        await CreateFeature(project.Id, "203-open-b", "Open blocker B");
        await AddDependencyAsync(project.Id, "201-blocked", "202-open-a");
        await AddDependencyAsync(project.Id, "201-blocked", "203-open-b");

        var ceremonies = await CeremoniesAsync();
        var rows = ceremonies.Features.Where(f => f.ProjectId == project.Id).ToArray();

        rows.Single().FeatureSlug.ShouldBe("201-blocked");
        rows.Single().Reasons.ShouldBe(new[] { "blocked by 2" });
    }

    [Fact]
    public async Task Ceremonies_EveryReturnedRow_CarriesAtLeastOneReason()
    {
        var project = await CreateProjectAsync();

        await CreateFeature(project.Id, "301-spec-wait", "Spec waits");
        await CreateArtifact(project.Id, "301-spec-wait", "spec");

        await CreateFeature(project.Id, "302-blocked", "Blocked feature");
        await CreateFeature(project.Id, "303-open", "Open blocker");
        await AddDependencyAsync(project.Id, "302-blocked", "303-open");

        var ceremonies = await CeremoniesAsync();
        var rows = ceremonies.Features.Where(f => f.ProjectId == project.Id).ToArray();

        rows.Length.ShouldBe(2);
        rows.All(r => r.Reasons.Length > 0).ShouldBeTrue();
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"attention-proj-{Guid.NewGuid():N}",
            DiskPath = _sandboxDir,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task CreateFeature(Guid projectId, string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<Guid> CreateArtifact(Guid projectId, string slug, string kind)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}/artifacts",
            new { Kind = kind, Body = $"# {kind} for {slug}" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var artifact = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        artifact.ShouldNotBeNull();
        return artifact!.Id;
    }

    private async Task ValidateArtifact(Guid id)
    {
        var response = await _client.PostAsync($"/api/planning/artifacts/{id}/validate", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task BringToStatusAsync(Guid projectId, string slug, string target)
    {
        string[] path = target switch
        {
            "review" => ["inProgress", "review"],
            "done" => ["inProgress", "review", "done"],
            _ => throw new ArgumentException($"unknown status '{target}'"),
        };

        if (target == "done")
        {
            var seed = await _client.PostAsJsonAsync(
                $"/api/projects/{projectId}/planning/features/{slug}/artifacts",
                new { Kind = "result", Body = "close-out" });
            seed.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
        }

        foreach (var step in path)
        {
            var response = await _client.PutAsJsonAsync(
                $"/api/projects/{projectId}/planning/features/{slug}",
                new { Status = step });
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    private async Task AddDependencyAsync(Guid projectId, string slug, string blockedBy)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}/dependencies",
            new { BlockedBy = blockedBy });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<CeremoniesJson> CeremoniesAsync() =>
        await _client.GetFromJsonAsync<CeremoniesJson>("/api/attention/ceremonies", JsonOptions)
            ?? throw new InvalidOperationException("ceremonies body was null");

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    public sealed record ArtifactJson(Guid Id);

    public sealed record CeremonyJson(
        Guid ProjectId,
        string ProjectName,
        string FeatureSlug,
        string? Number,
        string Title,
        string[] Reasons,
        string? Url);

    public sealed record CeremoniesJson(CeremonyJson[] Features);
}
