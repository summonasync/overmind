using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class IssueDoorApiTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IssueDoorApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-issues-{Guid.NewGuid():N}.db");
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

    private async Task CreateFeatureAsync(Guid projectId, string slug)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            new { Slug = slug, Title = slug, Body = "" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<long> NotificationCountAsync(Guid projectId) =>
        await ScalarAsync<long>(
            $"SELECT COUNT(*) FROM AgentNotifications WHERE ProjectId = '{projectId.ToString().ToUpperInvariant()}'");

    private async Task<long> ScalarAsync<T>(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task AgentDoor_CreateOnForeignBoard_PostsNotificationWithIssueLink()
    {
        var board = await CreateProjectAsync("board");
        var author = await CreateProjectAsync("author");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{board.Id}/issues",
            new { AuthorProjectId = author.Id, Title = "Crash on save", Body = "## Repro\nOpen the file." },
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var echo = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var issueId = echo.GetProperty("id").GetString();

        (await NotificationCountAsync(board.Id)).ShouldBe(1);
        var linked = await ScalarAsync<long>(
            "SELECT COUNT(*) FROM AgentNotifications WHERE LOWER(FeatureSlug) = " +
            $"'{issueId!.ToLowerInvariant()}' AND LOWER(Tab) = 'issues'");
        linked.ShouldBe(1);
    }

    [Fact]
    public async Task AgentDoor_CreateOnOwnBoard_PostsNoNotification()
    {
        var board = await CreateProjectAsync("board");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{board.Id}/issues",
            new { AuthorProjectId = board.Id, Title = "Crash on save", Body = "Repro inside." },
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await NotificationCountAsync(board.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Create_UnknownAuthorProject_Returns404()
    {
        var board = await CreateProjectAsync("board");

        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{board.Id}/issues",
            new { AuthorProjectId = Guid.NewGuid(), Title = "Crash on save", Body = "Repro inside." },
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AgentDoor_FullLifecycle_SourceIsAgent()
    {
        var board = await CreateProjectAsync("board");
        var author = await CreateProjectAsync("author");
        await CreateFeatureAsync(board.Id, "050-crash-fix");

        var created = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{board.Id}/issues",
            new { AuthorProjectId = author.Id, Title = "Crash on save", Body = "## Repro\nOpen the file." },
            JsonOptions);
        var issue = (await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString();

        var appended = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{board.Id}/issues/{issue}/append",
            new { AuthorProjectId = author.Id, Text = "still reproducible" }, JsonOptions);
        appended.StatusCode.ShouldBe(HttpStatusCode.OK);

        var triaged = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{board.Id}/issues/{issue}/status",
            new { Status = "triaged", Source = "Agent" }, JsonOptions);
        triaged.StatusCode.ShouldBe(HttpStatusCode.OK);
        var statusEcho = await triaged.Content.ReadFromJsonAsync<StatusJson>(JsonOptions);
        statusEcho!.Status.ShouldBe("triaged");
        statusEcho.Source.ShouldBe("Agent", StringCompareShould.IgnoreCase);

        var promoted = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{board.Id}/issues/{issue}/promote",
            new { FeatureSlug = "050-crash-fix", Source = "Agent" }, JsonOptions);
        promoted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var promoteEcho = await promoted.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        promoteEcho.GetProperty("status").GetString().ShouldBe("promoted");
        promoteEcho.GetProperty("artifactId").GetString().ShouldNotBeNullOrEmpty();

        (await ScalarAsync<long>(
            $"SELECT COUNT(*) FROM PlanningArtifacts WHERE Body LIKE '%{issue!.ToUpperInvariant()}%'"))
            .ShouldBe(1);
    }

    [Fact]
    public async Task ProjectIdLessAgentRoutes_FullLifecycle_Work()
    {
        var board = await CreateProjectAsync("board");
        var author = await CreateProjectAsync("author");
        await CreateFeatureAsync(board.Id, "050-crash-fix");

        var created = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{board.Id}/issues",
            new { AuthorProjectId = author.Id, Title = "Crash on save", Body = "## Repro\nOpen the file." },
            JsonOptions);
        var issue = (await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString();

        var appended = await _client.PostAsJsonAsync(
            $"/api/agent/issues/{issue}/append",
            new { AuthorProjectId = author.Id, Text = "still reproducible" }, JsonOptions);
        appended.StatusCode.ShouldBe(HttpStatusCode.OK);

        var triaged = await _client.PostAsJsonAsync(
            $"/api/agent/issues/{issue}/status",
            new { Status = "triaged", Source = "Agent" }, JsonOptions);
        triaged.StatusCode.ShouldBe(HttpStatusCode.OK);

        var card = await _client.GetAsync($"/api/agent/issues/{issue}");
        card.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cardBody = await card.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        cardBody.GetProperty("status").GetString().ShouldBe("triaged");
        cardBody.GetProperty("appends").EnumerateArray().ShouldHaveSingleItem();

        var promoted = await _client.PostAsJsonAsync(
            $"/api/agent/issues/{issue}/promote",
            new { FeatureSlug = "050-crash-fix", Source = "Agent" }, JsonOptions);
        promoted.StatusCode.ShouldBe(HttpStatusCode.OK);

        var unknown = await _client.GetAsync($"/api/agent/issues/{Guid.NewGuid()}");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HumanDoor_FullLifecycle_SourceIsUi()
    {
        var board = await CreateProjectAsync("board");
        await CreateFeatureAsync(board.Id, "050-crash-fix");

        var created = await _client.PostAsJsonAsync(
            $"/api/projects/{board.Id}/issues",
            new { AuthorProjectId = board.Id, Title = "Crash on save", Body = "## Repro\nOpen the file." },
            JsonOptions);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var issue = (await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString();

        var triaged = await _client.PostAsJsonAsync(
            $"/api/projects/{board.Id}/issues/{issue}/status",
            new { Status = "triaged" }, JsonOptions);
        triaged.StatusCode.ShouldBe(HttpStatusCode.OK);
        var statusEcho = await triaged.Content.ReadFromJsonAsync<StatusJson>(JsonOptions);
        statusEcho!.Source.ShouldBe("ui", StringCompareShould.IgnoreCase);

        var promoted = await _client.PostAsJsonAsync(
            $"/api/projects/{board.Id}/issues/{issue}/promote",
            new { FeatureSlug = "050-crash-fix" }, JsonOptions);
        promoted.StatusCode.ShouldBe(HttpStatusCode.OK);

        var closed = await _client.PostAsJsonAsync(
            $"/api/projects/{board.Id}/issues/{issue}/status",
            new { Status = "closed" }, JsonOptions);
        closed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var closedEcho = await closed.Content.ReadFromJsonAsync<StatusJson>(JsonOptions);
        closedEcho!.ClosedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task UnknownIssueId_Returns404OnBothDoors()
    {
        var board = await CreateProjectAsync("board");

        var agentGet = await _client.GetAsync($"/api/agent/projects/{board.Id}/issues/{Guid.NewGuid()}");
        agentGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var humanGet = await _client.GetAsync($"/api/projects/{board.Id}/issues/{Guid.NewGuid()}");
        humanGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var agentStatus = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{board.Id}/issues/{Guid.NewGuid()}/status",
            new { Status = "triaged", Source = "Agent" }, JsonOptions);
        agentStatus.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var humanAppend = await _client.PostAsJsonAsync(
            $"/api/projects/{board.Id}/issues/{Guid.NewGuid()}/append",
            new { AuthorProjectId = board.Id, Text = "x" }, JsonOptions);
        humanAppend.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var agentPromote = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{board.Id}/issues/{Guid.NewGuid()}/promote",
            new { FeatureSlug = "050-crash-fix", Source = "Agent" }, JsonOptions);
        agentPromote.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var humanPromote = await _client.PostAsJsonAsync(
            $"/api/projects/{board.Id}/issues/{Guid.NewGuid()}/promote",
            new { FeatureSlug = "050-crash-fix" }, JsonOptions);
        humanPromote.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HumanDoor_Provenance404_FlowsThrough()
    {
        var board = await CreateProjectAsync("board");

        var create = await _client.PostAsJsonAsync(
            $"/api/projects/{board.Id}/issues",
            new { AuthorProjectId = Guid.NewGuid(), Title = "Crash", Body = "body" }, JsonOptions);
        create.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var created = await _client.PostAsJsonAsync(
            $"/api/projects/{board.Id}/issues",
            new { AuthorProjectId = board.Id, Title = "Crash", Body = "body" }, JsonOptions);
        var issue = (await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString();

        var append = await _client.PostAsJsonAsync(
            $"/api/projects/{board.Id}/issues/{issue}/append",
            new { AuthorProjectId = Guid.NewGuid(), Text = "follow-up" }, JsonOptions);
        append.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_IsMetadataOnly()
    {
        var board = await CreateProjectAsync("board");
        var author = await CreateProjectAsync("author");

        var created = await _client.PostAsJsonAsync(
            $"/api/projects/{board.Id}/issues",
            new { AuthorProjectId = author.Id, Title = "Crash on save", Body = "secret body" }, JsonOptions);
        var issue = (await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString();
        await _client.PostAsJsonAsync(
            $"/api/projects/{board.Id}/issues/{issue}/append",
            new { AuthorProjectId = author.Id, Text = "secret follow-up" }, JsonOptions);

        var noQuery = await _client.GetAsync($"/api/projects/{board.Id}/issues");
        if (!noQuery.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"noQuery {noQuery.StatusCode}: {await noQuery.Content.ReadAsStringAsync()}");
        }

        var noQueryBody = await noQuery.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        noQueryBody.EnumerateArray().ShouldNotBeEmpty();
        noQueryBody.EnumerateArray().First().GetProperty("title").GetString().ShouldBe("Crash on save");

        var onlyStatus = await _client.GetAsync($"/api/projects/{board.Id}/issues?status=open");
        if (!onlyStatus.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"onlyStatus {onlyStatus.StatusCode}: {await onlyStatus.Content.ReadAsStringAsync()}");
        }

        var onlyStatusBody = await onlyStatus.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        onlyStatusBody.EnumerateArray().ShouldNotBeEmpty();

        var onlyAuthor = await _client.GetAsync(
            $"/api/projects/{board.Id}/issues?authorProjectId={author.Id}");
        if (!onlyAuthor.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"onlyAuthor {onlyAuthor.StatusCode}: {await onlyAuthor.Content.ReadAsStringAsync()}");
        }

        var filteredResponse = onlyAuthor;

        var filtered = await filteredResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var row = filtered.EnumerateArray().Single();
        row.EnumerateObject().Select(p => p.Name).ToArray().ShouldBe(
            ["id", "title", "status", "authorProjectId", "createdAt", "closedAt", "promotedToArtifactId", "appendCount"]);
        row.GetProperty("appendCount").GetInt32().ShouldBe(1);
        row.GetRawText().ShouldNotContain("secret");

        var byStatus = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{board.Id}/issues?status=closed", JsonOptions);
        byStatus.EnumerateArray().ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_ReturnsCardWithAppendTrail()
    {
        var board = await CreateProjectAsync("board");
        var author = await CreateProjectAsync("author");
        var created = await _client.PostAsJsonAsync(
            $"/api/projects/{board.Id}/issues",
            new { AuthorProjectId = author.Id, Title = "Crash on save", Body = "## Repro\nOpen." }, JsonOptions);
        var issue = (await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString();
        await _client.PostAsJsonAsync(
            $"/api/projects/{board.Id}/issues/{issue}/append",
            new { AuthorProjectId = author.Id, Text = "still reproducible" }, JsonOptions);

        var card = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{board.Id}/issues/{issue}", JsonOptions);

        card.GetProperty("title").GetString()!.ShouldBe("Crash on save");
        card.GetProperty("body").GetString()!.ShouldContain("Open.");
        card.GetProperty("appends").EnumerateArray().ShouldHaveSingleItem();
        card.GetProperty("appends")[0].GetProperty("text").GetString()!.ShouldBe("still reproducible");
    }

    [Fact]
    public async Task AgentDoor_CrossBoardIssueId_Returns404()
    {
        var board = await CreateProjectAsync("board");
        var otherBoard = await CreateProjectAsync("other-board");
        await CreateFeatureAsync(otherBoard.Id, "050-crash-fix");

        var created = await _client.PostAsJsonAsync(
            $"/api/projects/{otherBoard.Id}/issues",
            new { AuthorProjectId = otherBoard.Id, Title = "Crash", Body = "body" }, JsonOptions);
        var issue = (await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetString();

        var append = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{board.Id}/issues/{issue}/append",
            new { AuthorProjectId = board.Id, Text = "x" }, JsonOptions);
        append.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var status = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{board.Id}/issues/{issue}/status",
            new { Status = "triaged", Source = "Agent" }, JsonOptions);
        status.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var promote = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{board.Id}/issues/{issue}/promote",
            new { FeatureSlug = "050-crash-fix", Source = "Agent" }, JsonOptions);
        promote.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var get = await _client.GetAsync(
            $"/api/agent/projects/{board.Id}/issues/{issue}");
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    private sealed record StatusJson(Guid Id, string Status, string Source, DateTime? ClosedAt);
}
