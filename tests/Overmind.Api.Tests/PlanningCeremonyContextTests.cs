using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class PlanningCeremonyContextTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PlanningCeremonyContextTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var name = $"ceremony-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        project.ShouldNotBeNull();
        return project!;
    }

    private async Task<(string Phase, string? BriefExcerpt)> BoardRowAsync(Guid projectId, string slug)
    {
        var board = await _client.GetFromJsonAsync<BoardItemJson[]>(
            $"/api/agent/projects/{projectId}/planning/features", JsonOptions);
        var row = board!.Single(i => i.Slug == slug);
        return (row.Phase, row.BriefExcerpt);
    }

    private async Task<(string Phase, string? BriefExcerpt)> QueueRowAsync(Guid projectId, string slug)
    {
        var queue = await _client.GetFromJsonAsync<QueueResponseJson>(
            $"/api/agent/projects/{projectId}/planning/task-queue", JsonOptions);
        var row = queue!.Items.Single(i => i.Slug == slug);
        return (row.Phase, row.BriefExcerpt);
    }

    [Fact]
    public async Task BoardAndQueue_AgreeOnPhaseAndExcerpt()
    {
        var project = await CreateProjectAsync();
        await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features",
            new
            {
                Slug = "context-shared",
                Title = "Context shared",
                Body = "<goal>\nShip the demo without losing the trail.\n</goal>"
            });

        var spec = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features/context-shared/artifacts",
            new { Kind = "spec", Body = "# spec" });
        var specId = (await spec.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString();
        var validated = await _client.PostAsync(
            $"/api/planning/artifacts/{specId}/validate", null);
        validated.StatusCode.ShouldBe(HttpStatusCode.OK);

        var board = await BoardRowAsync(project.Id, "context-shared");
        var queue = await QueueRowAsync(project.Id, "context-shared");

        board.Phase.ShouldBe("planning");
        queue.Phase.ShouldBe(board.Phase);
        queue.BriefExcerpt.ShouldBe(board.BriefExcerpt);
        board.BriefExcerpt.ShouldNotBeNull();
    }

    [Fact]
    public async Task BoardAndQueue_BodylessFeature_ExcerptNull_PhaseStillDerived()
    {
        var project = await CreateProjectAsync();
        await _client.PostAsJsonAsync(
            $"/api/agent/projects/{project.Id}/planning/features",
            new { Slug = "context-bare", Title = "Context bare", Body = "" });

        var board = await BoardRowAsync(project.Id, "context-bare");
        var queue = await QueueRowAsync(project.Id, "context-bare");

        board.Phase.ShouldBe("drafting");
        queue.Phase.ShouldBe("drafting");
        board.BriefExcerpt.ShouldBeNull();
        queue.BriefExcerpt.ShouldBeNull();
    }

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    private sealed record BoardItemJson(
        string Slug, string Phase, string? BriefExcerpt);

    private sealed record QueueResponseJson(QueueItemJson[] Items);

    private sealed record QueueItemJson(string Slug, string Phase, string? BriefExcerpt);
}
