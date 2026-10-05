using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class CrossProjectIssuesApiTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CrossProjectIssuesApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-cross-issues-{Guid.NewGuid():N}.db");
        _factory = new ApiFactory(databasePath);
        _client = _factory.CreateClient();
        _db = new SqliteConnection($"Data Source={databasePath};Pooling=false");
        _db.Open();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _db.Dispose();
    }

    private async Task<ProjectJson> CreateProjectAsync(string namePrefix)
    {
        var name = $"{namePrefix}-{Guid.NewGuid():N}";
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

    private async Task<string> CreateIssueAsync(Guid projectId, string title)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/issues",
            new { AuthorProjectId = projectId, Title = title, Body = "body" },
            JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString()!;
    }

    private async Task SetStatusAsync(Guid projectId, string issueId, string status)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/issues/{issueId}/status",
            new { Status = status },
            JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_MergesProjects_WithIdentitySortAndRowShape()
    {
        var first = await CreateProjectAsync("first");
        var second = await CreateProjectAsync("second");
        var olderOpen = await CreateIssueAsync(first.Id, "older open");
        var newerOpen = await CreateIssueAsync(second.Id, "newer open");
        var closedNewest = await CreateIssueAsync(second.Id, "closed newest");
        await SetStatusAsync(second.Id, closedNewest, "closed");
        await _client.PostAsJsonAsync(
            $"/api/projects/{first.Id}/issues/{olderOpen}/append",
            new { AuthorProjectId = first.Id, Text = "follow-up" },
            JsonOptions);

        var response = await _client.GetAsync("/api/issues");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var rows = (await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .EnumerateArray().ToArray();
        rows.Select(r => r.GetProperty("id").GetString()).ShouldBe(
            [newerOpen, olderOpen, closedNewest]);

        rows[1].EnumerateObject().Select(p => p.Name).ToArray().ShouldBe(
            ["id", "projectId", "projectName", "title", "status", "authorProjectId",
             "createdAt", "closedAt", "promotedToArtifactId", "appendCount"]);
        rows[1].GetProperty("projectId").GetString().ShouldBe(first.Id.ToString());
        rows[1].GetProperty("projectName").GetString().ShouldBe(first.Name);
        rows[1].GetProperty("appendCount").GetInt32().ShouldBe(1);
        rows[2].GetProperty("projectName").GetString().ShouldBe(second.Name);
        rows[2].GetProperty("status").GetString().ShouldBe("closed");
    }

    [Fact]
    public async Task List_EmptyUntilIssuesExist_AndStatusFilterHolds()
    {
        var empty = await _client.GetAsync("/api/issues");
        empty.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await empty.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .EnumerateArray().ShouldBeEmpty();

        var project = await CreateProjectAsync("solo");
        await CreateIssueAsync(project.Id, "open only");

        var closedFilter = await _client.GetAsync("/api/issues?status=closed");
        closedFilter.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await closedFilter.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .EnumerateArray().ShouldBeEmpty();

        var openFilter = await _client.GetAsync("/api/issues?status=open");
        var openRows = (await openFilter.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .EnumerateArray().ToArray();
        openRows.ShouldHaveSingleItem();
        openRows[0].GetProperty("title").GetString().ShouldBe("open only");

        var rejected = await _client.GetAsync("/api/issues?status=nope");
        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);
}
