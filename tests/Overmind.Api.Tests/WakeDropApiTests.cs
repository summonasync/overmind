using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Overmind.Infrastructure.Wake;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class WakeDropApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"overmind-wake-drop-{Guid.NewGuid():N}.db");
    private readonly string _wakeRoot = Path.Combine(Path.GetTempPath(), $"wake-drop-api-{Guid.NewGuid():N}");
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public WakeDropApiTests()
    {
        Environment.SetEnvironmentVariable(WakeDropWriter.DirectoryVariable, _wakeRoot);
        _factory = new ApiFactory(_databasePath);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        Environment.SetEnvironmentVariable(WakeDropWriter.DirectoryVariable, null);
        if (Directory.Exists(_wakeRoot))
        {
            Directory.Delete(_wakeRoot, true);
        }
    }

    [Fact]
    public async Task Reply_AndMention_LandInTheSessionAliasDropFileWithCursorProgression()
    {
        var alias = "Dan";
        await StartSessionAsync(alias);
        var projectId = await CreateProjectAsync();
        var file = Path.Combine(_wakeRoot, $"{alias.ToLowerInvariant()}.json");
        var pending = await SeedPendingReplyAsync(alias);

        var first = await NotifyAgentAsync(alias, "approve the wake gate");
        await ReplyAsync(first, "approved");

        var pushed = await WaitForDropAsync(file, d => d.Signals.Count == 1);
        pushed.Alias.ShouldBe(alias.ToLowerInvariant());
        pushed.Signals.Single().Kind.ShouldBe(AgentWakeService.ReplyKind);
        pushed.Signals.Single().RefId.ShouldBe(pending);

        await Task.Delay(5);
        await PostHumanAsync(projectId, "erin", $"ping @{alias} from the wave");

        var drop = await WaitForDropAsync(file, d => d.Signals.Count == 2);
        drop.Signals.Select(s => s.Kind).ShouldBe([AgentWakeService.ReplyKind, AgentWakeService.MentionKind]);
        drop.Signals.Single(s => s.Kind == AgentWakeService.ReplyKind).From.ShouldBe("human");
        var mention = drop.Signals.Single(s => s.Kind == AgentWakeService.MentionKind);
        mention.RoomId.ShouldBe("all");
        mention.From.ShouldBe("erin");
        drop.Cursor.ShouldBe($"{mention.CreatedAt:O}|{mention.RefId}");

        var reread = await WaitForDropAsync(file, d => d.Signals.Count == 2);
        reread.Cursor.ShouldBe(drop.Cursor);
        reread.Signals.Select(s => s.RefId).ShouldBe(drop.Signals.Select(s => s.RefId));

        var second = await NotifyAgentAsync(alias, "second gate");
        await Task.Delay(5);
        await ReplyAsync(second, "approved again");
        await PostHumanAsync(projectId, "erin", $"second ping @{alias} from the wave");

        var advanced = await WaitForDropAsync(file, d => d.Signals.Count == 3);
        advanced.Cursor.ShouldNotBe(drop.Cursor);
        advanced.Signals.ShouldNotContain(s => s.RefId == first,
            "the reply flow stamps ReadAt at birth, so live replies never enter the default drop window");
        advanced.Signals.ShouldNotContain(s => s.RefId == second);
        advanced.Signals.Last().Kind.ShouldBe(AgentWakeService.MentionKind);
        advanced.Cursor.ShouldBe(
            $"{advanced.Signals[^1].CreatedAt:O}|{advanced.Signals[^1].RefId}");
        Directory.EnumerateFiles(_wakeRoot, "*.json")
            .Select(Path.GetFileName)
            .ShouldBe([$"{alias.ToLowerInvariant()}.json"]);
    }

    private async Task<Guid> SeedPendingReplyAsync(string agent)
    {
        var id = Guid.NewGuid();
        var repliedAt = DateTime.UtcNow.AddMinutes(-4);
        await using var connection = new SqliteConnection($"Data Source={_databasePath};Pooling=false");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO AgentNotifications (Id, ProjectId, Kind, FeatureSlug, Tab, Text, Agent, CreatedAt, ReadAt, ReplyText, RepliedAt, HiddenAt)
            VALUES ('{id.ToString().ToUpperInvariant()}', NULL, 1, NULL, NULL, 'earlier gate', '{agent}', '{Timestamp(repliedAt)}', NULL, 'approved earlier', '{Timestamp(repliedAt)}', NULL)
            """;
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private static string Timestamp(DateTime at) => at.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF");

    private async Task<WakeDrop> WaitForDropAsync(string file, Func<WakeDrop, bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(file))
            {
                var drop = JsonSerializer.Deserialize<WakeDrop>(
                    await File.ReadAllTextAsync(file), JsonOptions);
                if (drop is not null && done(drop))
                {
                    return drop;
                }
            }

            await Task.Delay(25);
        }

        var found = Directory.Exists(_wakeRoot)
            ? string.Join(", ", Directory.EnumerateFiles(_wakeRoot))
            : "<no wake dir>";
        throw new TimeoutException($"Wake drop file {file} never reached the expected state. On disk: {found}");
    }

    private async Task StartSessionAsync(string alias)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/agent/sessions", new { Alias = alias, Vendor = "claude" }, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<Guid> NotifyAgentAsync(string agent, string text)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/agent/notifications",
            new { Kind = "question", Text = text, From = agent }, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetGuid();
    }

    private async Task ReplyAsync(Guid notificationId, string text)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/notifications/{notificationId}/reply", new { Text = text }, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task PostHumanAsync(Guid projectId, string authorAlias, string text)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/projects/{projectId}/wave/rooms/all/messages",
            new { AuthorAlias = authorAlias, Text = text }, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var name = $"wakedrop-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = TestPaths.DiskPath(name),
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetGuid();
    }
}
