using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class OverlapMatchesApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _projectDir;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OverlapMatchesApiTests()
    {
        _client = _factory.CreateClient();
        _projectDir = Path.Combine(Path.GetTempPath(), $"overmind-overlap-matches-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_projectDir);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();

        if (Directory.Exists(_projectDir))
        {
            Directory.Delete(_projectDir, recursive: true);
        }
    }

    [Fact]
    public async Task TaskQueue_RowCarriesExactMatch()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project, "alpha", "Alpha");
        await CreateFeatureAsync(project, "beta", "Beta");
        await ClaimAsync(project, "alpha");
        await ClaimAsync(project, "beta");
        await PutCodeSurfacesAsync(project, "alpha", ["README.md"]);
        await PutCodeSurfacesAsync(project, "beta", ["README.md"]);

        var queue = await GetJsonAsync($"/api/agent/projects/{project}/planning/task-queue");
        var alphaRow = queue.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("slug").GetString() == "alpha");
        var overlap = alphaRow.GetProperty("potentialOverlaps")[0];

        overlap.GetProperty("sharedGlobs").EnumerateArray()
            .Select(v => v.GetString()).ShouldBe(["README.md"]);
        var match = overlap.GetProperty("matches")[0];
        match.GetProperty("pattern").GetString().ShouldBe("README.md");
        match.GetProperty("partnerPattern").GetString().ShouldBe("README.md");
        match.GetProperty("kind").GetString().ShouldBe("exact");
    }

    [Fact]
    public async Task WaveSnapshot_RowCarriesSharedDirectoryMatch()
    {
        var project = await CreateProjectAsync();
        await CreateFeatureAsync(project, "alpha", "Alpha");
        await CreateFeatureAsync(project, "beta", "Beta");
        await ClaimAsync(project, "alpha");
        await ClaimAsync(project, "beta");
        await PutCodeSurfacesAsync(project, "alpha", ["tests/A*.cs"]);
        await PutCodeSurfacesAsync(project, "beta", ["tests/B*.cs"]);

        var snapshot = await GetJsonAsync(
            $"/api/agent/projects/{project}/planning/wave-snapshot");
        var alphaRow = snapshot.GetProperty("queue").EnumerateArray()
            .Single(i => i.GetProperty("slug").GetString() == "alpha");
        var match = alphaRow.GetProperty("potentialOverlaps")[0].GetProperty("matches")[0];

        match.GetProperty("pattern").GetString().ShouldBe("tests/A*.cs");
        match.GetProperty("partnerPattern").GetString().ShouldBe("tests/B*.cs");
        match.GetProperty("kind").GetString().ShouldBe("sharedDirectory");
    }

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        var response = await _client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = $"overlap-matches-proj-{Guid.NewGuid():N}",
            DiskPath = _projectDir,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!.Id;
    }

    private async Task CreateFeatureAsync(Guid projectId, string slug, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features",
            new { Slug = slug, Title = title, Body = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task ClaimAsync(Guid projectId, string slug)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}/claim",
            new { Alias = $"coder-{slug}", Vendor = "test" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<HttpResponseMessage> PutCodeSurfacesAsync(
        Guid projectId, string slug, string[] codeSurfaces) =>
        await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/planning/features/{slug}",
            new { CodeSurfaces = codeSurfaces });

    public sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);
}
