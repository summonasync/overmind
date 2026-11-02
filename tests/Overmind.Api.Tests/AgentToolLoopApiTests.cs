using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentToolLoopApiTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly string _root;

    public AgentToolLoopApiTests()
    {
        _client = _factory.CreateClient();
        _root = Path.Combine(Path.GetTempPath(), $"overmind-agent-tools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Registrar_RegistersEmbeddedSession_ThroughLoopback()
    {
        var project = await CreateProjectAsync();

        using var scope = _factory.Services.CreateAsyncScope();
        var registrar = scope.ServiceProvider.GetRequiredService<EmbeddedAgentSessionRegistrar>();
        var sessionId = await registrar.EnsureRegisteredAsync(CancellationToken.None);

        sessionId.ShouldNotBe(Guid.Empty);
        scope.ServiceProvider.GetRequiredService<EmbeddedAgentSessionState>().SessionId.ShouldBe(sessionId);

        await using var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        var session = await db.AgentSessions.SingleAsync(s => s.Id == sessionId);
        session.Alias.ShouldBe("overmind-agent");
        session.Vendor.ShouldBe("overmind-host");
        session.Role.ShouldBe(AgentSession.EmbeddedRole);
        project.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task PlanningCreateFeatureThroughTools_MaterializesToBacklog()
    {
        var project = await CreateProjectAsync();
        await EnsureSessionAsync();

        using var scope = _factory.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<AgentToolRegistry>();

        var created = await registry.InvokeAsync(
            "planning_create_feature",
            JsonSerializer.SerializeToElement(new
            {
                projectId = project.Id.ToString(),
                title = "Agent tool demo",
                body = "<goal>\nDispatched through the embedded loop.\n</goal>"
            }),
            default,
            CancellationToken.None);

        created.GetProperty("slug").GetString().ShouldNotBeNull();

        var materialized = await registry.InvokeAsync(
            "planning_materialize",
            JsonSerializer.SerializeToElement(new { projectId = project.Id.ToString(), force = false }),
            default,
            CancellationToken.None);

        materialized.TryGetProperty("error", out _).ShouldBeFalse(
            materialized.GetRawText());

        var backlogFolder = Path.Combine(_root, "BACKLOG");
        Directory.Exists(backlogFolder).ShouldBeTrue("materialization should create BACKLOG/ under the project root");
        var featureFile = Directory
            .EnumerateFiles(backlogFolder, "feature.md", SearchOption.AllDirectories)
            .ShouldHaveSingleItem();
        (await File.ReadAllTextAsync(featureFile)).ShouldContain("Agent tool demo");
    }

    [Fact]
    public async Task StreamFrames_CarryToolCallsAndTokenAccounting()
    {
        var created = await _client.PostAsJsonAsync(
            "/api/agent/conversations", new { Title = "frames" }, JsonOptions);
        var conversation = await created.Content.ReadFromJsonAsync<ConversationJson>(JsonOptions);

        var frames = await StreamAsync(conversation!.Id, "hello agent");

        var tool = frames.Single(frame => frame.Event == "tool");
        JsonDocument.Parse(tool.DataText).RootElement.GetProperty("tool").GetString()
            .ShouldBe("planning_task_queue");

        var completed = frames.Last(frame => frame.Event == "completed");
        var completedData = JsonDocument.Parse(completed.DataText).RootElement;
        completedData.GetProperty("tokensUsed").GetInt64().ShouldBe(321);
        completedData.GetProperty("messageId").GetString().ShouldNotBeNull();
    }

    [Fact]
    public async Task Settings_AgentAlias_RoundTrips()
    {
        var updated = await _client.PutAsJsonAsync("/api/settings", new { AgentAlias = "board-boss" });
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);

        var settings = await _client.GetFromJsonAsync<SettingsJson>("/api/settings", JsonOptions);
        settings!.AgentAlias.ShouldBe("board-boss");

        var rejected = await _client.PutAsJsonAsync("/api/settings", new { AgentAlias = new string('x', 33) });
        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await rejected.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Errors.Keys.ShouldContain("AgentAlias");
    }

    [Fact]
    public async Task AgentSessions_EmbeddedRole_IsAcceptedAndInvalidRoleRejected()
    {
        var registered = await _client.PostAsJsonAsync(
            "/api/agent/sessions",
            new { Alias = "overmind-agent", Vendor = "overmind-host", Role = "embedded" },
            JsonOptions);
        registered.StatusCode.ShouldBe(HttpStatusCode.Created);
        var session = await registered.Content.ReadFromJsonAsync<SessionJson>(JsonOptions);
        session!.Id.ShouldNotBe(Guid.Empty);

        using var scope = _factory.Services.CreateAsyncScope();
        await using var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();
        (await db.AgentSessions.SingleAsync(s => s.Id == session.Id)).Role.ShouldBe("embedded");

        var switched = await _client.PostAsJsonAsync(
            $"/api/agent/sessions/{session.Id}/role", new { Role = "embedded" }, JsonOptions);
        switched.StatusCode.ShouldBe(HttpStatusCode.OK);

        var rejected = await _client.PostAsJsonAsync(
            $"/api/agent/sessions/{session.Id}/role", new { Role = "emperor" }, JsonOptions);
        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task WaveTools_PostPeekUnreadHistory_CrossProjectRooms()
    {
        var projectA = await CreateProjectAsync();
        var projectB = await CreateProjectAsync();
        await EnsureSessionAsync();

        using var scope = _factory.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<AgentToolRegistry>();

        var postA = await registry.InvokeAsync("wave_chat_post", Args(new
        {
            projectId = projectA.Id.ToString(),
            room = "all",
            text = "lane signal for project A",
        }), default, CancellationToken.None);
        postA.GetProperty("authorAlias").GetString().ShouldBe("overmind-agent");
        postA.GetProperty("authorRole").GetString().ShouldBe(AgentSession.EmbeddedRole);

        await registry.InvokeAsync("wave_chat_post", Args(new
        {
            projectId = projectB.Id.ToString(),
            room = "all",
            text = "lane signal for project B",
        }), default, CancellationToken.None);

        var peekB = await registry.InvokeAsync("wave_chat_peek", Args(new
        {
            projectId = projectB.Id.ToString(),
            room = "all",
        }), default, CancellationToken.None);
        peekB.GetProperty("room").GetString().ShouldBe("all");
        peekB.GetProperty("count").GetInt32().ShouldBe(1);

        var unreadB = await registry.InvokeAsync("wave_chat_unread", Args(new
        {
            projectId = projectB.Id.ToString(),
            room = "all",
        }), default, CancellationToken.None);
        var bMessage = unreadB.GetProperty("items").EnumerateArray().Single();
        bMessage.GetProperty("text").GetString().ShouldBe("lane signal for project B");

        var historyA = await registry.InvokeAsync("wave_chat_history", Args(new
        {
            projectId = projectA.Id.ToString(),
            room = "all",
        }), default, CancellationToken.None);
        historyA.GetProperty("items").EnumerateArray().Single()
            .GetProperty("text").GetString().ShouldBe("lane signal for project A");
    }

    private static JsonElement Args(object arguments) =>
        JsonSerializer.SerializeToElement(arguments, JsonOptions);

    private async Task<ProjectJson> CreateProjectAsync()
    {
        var name = $"agent-tools-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            Name = name,
            DiskPath = _root,
            Description = (string?)null,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions))!;
    }

    private async Task EnsureSessionAsync()
    {
        using var scope = _factory.Services.CreateAsyncScope();
        var registrar = scope.ServiceProvider.GetRequiredService<EmbeddedAgentSessionRegistrar>();
        await registrar.EnsureRegisteredAsync(CancellationToken.None);
    }

    private async Task<List<SseFrame>> StreamAsync(Guid conversationId, string text)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/agent/conversations/{conversationId}/messages/stream")
        {
            Content = JsonContent.Create(new { Text = text }, options: JsonOptions)
        };
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var frames = new List<SseFrame>();
        foreach (var chunk in body.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            string? name = null;
            var data = new StringBuilder();
            foreach (var line in chunk.Split('\n'))
            {
                if (line.StartsWith("event: ", StringComparison.Ordinal))
                {
                    name = line["event: ".Length..];
                }
                else if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    data.Append(line["data: ".Length..]);
                }
            }

            if (name is not null)
            {
                frames.Add(new SseFrame(name, data.ToString()));
            }
        }

        return frames;
    }

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath, string Description);

    private sealed record ConversationJson(Guid Id, Guid? ProjectId, string Title);

    private sealed record SessionJson(Guid Id, string Alias, string Vendor);

    private sealed record SettingsJson(string AgentAlias);

    private sealed record ProblemJson(string Title, string Detail, IDictionary<string, string[]> Errors);

    private sealed record SseFrame(string Event, string DataText);
}
