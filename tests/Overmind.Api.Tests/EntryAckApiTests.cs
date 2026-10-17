using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Overmind.Host.Api;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class EntryAckApiTests : IDisposable
{
    private static readonly Guid AgentSessionId = Guid.NewGuid();

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;
    private readonly SqliteConnection _db;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public EntryAckApiTests()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"overmind-entry-acks-{Guid.NewGuid():N}.db");
        _factory = new ApiFactory(databasePath);
        _client = _factory.CreateClient();
        _db = new SqliteConnection($"Data Source={databasePath};Pooling=false");
        _db.Open();
        SeedAgentSessionAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Ack_AgentDoor_AttributesFromTheSessionRow_AndReadByShowsOnRead()
    {
        var projectId = await CreateProjectAsync();
        var entry = await CreateEntryAsync(projectId);

        var response = await SendAgentAckAsync(projectId, entry.Id, AgentSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ack = await response.Content.ReadFromJsonAsync<AckJson>(JsonOptions);
        ack!.EntryId.ShouldBe(entry.Id);
        ack.Ack.ShouldBeTrue();
        ack.Alias.ShouldBe("Dan");
        ack.Vendor.ShouldBe("qwen");

        var read = (await _client.GetFromJsonAsync<EntryJson>($"/api/entries/{entry.Id}", JsonOptions))!;
        read.ReadBy.Select(r => r.Alias).ShouldBe(["Dan"]);
        read.ReadBy.Single().Vendor.ShouldBe("qwen");
    }

    [Fact]
    public async Task Ack_AgentDoor_WithoutHeader_Returns400()
    {
        var projectId = await CreateProjectAsync();
        var entry = await CreateEntryAsync(projectId);

        var response = await _client.PostAsync(
            $"/api/agent/projects/{projectId}/entries/{entry.Id}/ack", null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RowCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Ack_AgentDoor_UnknownSession_Returns400()
    {
        var projectId = await CreateProjectAsync();
        var entry = await CreateEntryAsync(projectId);

        var response = await SendAgentAckAsync(projectId, entry.Id, Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RowCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Ack_AgentDoor_SecondAck_IsIdempotentNoOp()
    {
        var projectId = await CreateProjectAsync();
        var entry = await CreateEntryAsync(projectId);
        var first = await SendAgentAckAsync(projectId, entry.Id, AgentSessionId);
        var second = await SendAgentAckAsync(projectId, entry.Id, AgentSessionId);

        (await first.Content.ReadFromJsonAsync<AckJson>(JsonOptions))!.Ack.ShouldBeTrue();
        var secondAck = await second.Content.ReadFromJsonAsync<AckJson>(JsonOptions);
        secondAck!.Ack.ShouldBeFalse();
        secondAck.Alias.ShouldBe("Dan");
        (await RowCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Ack_HumanDoor_UsesTheBodyAttribution()
    {
        var projectId = await CreateProjectAsync();
        var entry = await CreateEntryAsync(projectId);

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/entries/{entry.Id}/ack",
            new { Alias = "Johan", Vendor = "human" }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ack = await response.Content.ReadFromJsonAsync<AckJson>(JsonOptions);
        ack!.Ack.ShouldBeTrue();
        ack.Alias.ShouldBe("Johan");
        ack.Vendor.ShouldBe("human");

        var read = (await _client.GetFromJsonAsync<EntryJson>($"/api/entries/{entry.Id}", JsonOptions))!;
        read.ReadBy.Single().Alias.ShouldBe("Johan");
    }

    [Fact]
    public async Task Ack_HumanDoor_IdentityOverCap_Returns400ValidationProblemNot500()
    {
        var projectId = await CreateProjectAsync();
        var entry = await CreateEntryAsync(projectId);

        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/entries/{entry.Id}/ack",
            new { Alias = new string('x', 40), Vendor = "human" }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemJson>(JsonOptions);
        problem!.Errors.Count.ShouldBe(1);
        (await RowCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Ack_HumanDoor_WithoutBody_AcksAnonymously()
    {
        var projectId = await CreateProjectAsync();
        var entry = await CreateEntryAsync(projectId);

        var response = await _client.PostAsync(
            $"/api/projects/{projectId}/entries/{entry.Id}/ack", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ack = await response.Content.ReadFromJsonAsync<AckJson>(JsonOptions);
        ack!.Ack.ShouldBeTrue();
        ack.Alias.ShouldBeNull();
        ack.Vendor.ShouldBeNull();
        (await RowCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Ack_UnknownEntry_Returns404()
    {
        var projectId = await CreateProjectAsync();

        var response = await SendAgentAckAsync(projectId, Guid.NewGuid(), AgentSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ack_EntryOfAnotherProject_Returns404()
    {
        var ownerProject = await CreateProjectAsync();
        var otherProject = await CreateProjectAsync();
        var entry = await CreateEntryAsync(ownerProject);

        var response = await SendAgentAckAsync(otherProject, entry.Id, AgentSessionId);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ack_LeavesTheBodyVersionAndUpdatedAtUntouched()
    {
        var projectId = await CreateProjectAsync();
        var entry = await CreateEntryAsync(projectId);

        await SendAgentAckAsync(projectId, entry.Id, AgentSessionId);

        var read = (await _client.GetFromJsonAsync<EntryJson>($"/api/entries/{entry.Id}", JsonOptions))!;
        read.Content.ShouldBe(entry.Content);
        read.CurrentVersion.ShouldBe(0);
        read.UpdatedAt.ShouldBe(entry.UpdatedAt);
        read.ReadBy.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Read_Twice_CreatesNoAckRows()
    {
        var projectId = await CreateProjectAsync();
        var entry = await CreateEntryAsync(projectId);

        await _client.GetAsync($"/api/entries/{entry.Id}");
        await _client.GetAsync($"/api/entries/{entry.Id}");
        await _client.GetAsync($"/api/projects/{projectId}/entries");
        await _client.GetAsync($"/api/projects/{projectId}/entries");

        var listed = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{projectId}/entries", JsonOptions);
        listed!.Single().ReadBy.ShouldBeEmpty();
        (await RowCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task List_CarriesReadByPerEntry_OldestFirst()
    {
        var projectId = await CreateProjectAsync();
        var acked = await CreateEntryAsync(projectId);
        var untouched = await CreateEntryAsync(projectId);

        await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/entries/{acked.Id}/ack",
            new { Alias = "Johan", Vendor = "human" }, JsonOptions);
        await Task.Delay(5);
        await SendAgentAckAsync(projectId, acked.Id, AgentSessionId);

        var listed = await _client.GetFromJsonAsync<List<EntryJson>>(
            $"/api/projects/{projectId}/entries", JsonOptions);

        var ackedRow = listed!.Single(e => e.Id == acked.Id);
        ackedRow.ReadBy.Select(r => r.Alias).ShouldBe(["Johan", "Dan"]);
        listed.Single(e => e.Id == untouched.Id).ReadBy.ShouldBeEmpty();
    }

    [Fact]
    public async Task OwnerWrites_StillWorkAfterAck()
    {
        var projectId = await CreateProjectAsync();
        var entry = await CreateEntryAsync(projectId);
        await SendAgentAckAsync(projectId, entry.Id, AgentSessionId);

        var putResponse = await _client.PutAsJsonAsync($"/api/entries/{entry.Id}", new
        {
            Title = "marshalling v2",
            Content = "updated by the owner only",
        });

        putResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await putResponse.Content.ReadFromJsonAsync<EntryJson>(JsonOptions);
        updated!.CurrentVersion.ShouldBe(1);
        updated.ReadBy.Select(r => r.Alias).ShouldBe(["Dan"]);
    }

    private async Task<HttpResponseMessage> SendAgentAckAsync(Guid projectId, Guid entryId, Guid sessionId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/agent/projects/{projectId}/entries/{entryId}/ack");
        request.Headers.Add(SessionHeader.Name, sessionId.ToString());
        return await _client.SendAsync(request);
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var name = $"entry-acks-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions);
        return project!.Id;
    }

    private async Task<EntryJson> CreateEntryAsync(Guid projectId)
    {
        var response = await _client.PostAsJsonAsync("/api/entries", new
        {
            ProjectId = projectId,
            Type = "Note",
            Title = "lane marshalling",
            Content = "read before picking work",
            Tags = (string[]?)null,
            SkillFiles = (object[]?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EntryJson>(JsonOptions))!;
    }

    private async Task SeedAgentSessionAsync()
    {
        await ExecuteAsync($"""
            INSERT OR REPLACE INTO AgentSessions (Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt)
            VALUES ('{AgentSessionId.ToString().ToUpperInvariant()}', 'Dan', 'qwen', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}', NULL)
            """);
    }

    private async Task<int> RowCountAsync()
    {
        using var command = _db.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM EntryAcks";
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private async Task ExecuteAsync(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private sealed record ProjectJson(Guid Id);

    private sealed record ReadAckJson(string? Alias, string? Vendor, DateTime At);

    private sealed record AckJson(Guid EntryId, bool Ack, string? Alias, string? Vendor, DateTime At);

    private sealed record EntryJson(
        Guid Id,
        Guid ProjectId,
        string Type,
        string Title,
        string Content,
        int CurrentVersion,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        List<string> Tags,
        List<SkillFileJson> SkillFiles,
        List<ReadAckJson> ReadBy);

    private sealed record SkillFileJson(string Platform, string RelativePath, string Content);

    private sealed record ValidationProblemJson(Dictionary<string, string[]> Errors);
}
