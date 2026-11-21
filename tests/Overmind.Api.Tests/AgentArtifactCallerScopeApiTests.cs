using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// 246 T1. The eight artifact doors address a stored object by id, so the conversation's project — the
// pair the embedded-agent transport sends — is compared against the project that owns the artifact.
// The four doors that also take a projectId argument are exercised with the conversation's OWN id, so a
// green here cannot be borrowed from 241's argument guard (which lives in the registry, not on this wire).
public sealed class AgentArtifactCallerScopeApiTests : IAsyncLifetime, IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string ScopeCode = PlanningFailureCode.ArtifactOutsideCallerProject.ToString();

    private Guid _own;
    private Guid _foreign;
    private ArtifactJson _ownSpec = null!;
    private ArtifactJson _foreignSpec = null!;
    private ArtifactJson _ownTask = null!;
    private ArtifactJson _foreignTask = null!;
    private ArtifactJson _ownPlan = null!;
    private ArtifactJson _foreignPlan = null!;
    private string _foreignMarker = string.Empty;

    public AgentArtifactCallerScopeApiTests() => _client = _factory.CreateClient();

    public async Task InitializeAsync()
    {
        _own = (await CreateProjectAsync("caller-scope-own")).Id;
        _foreign = (await CreateProjectAsync("caller-scope-foreign")).Id;
        _foreignMarker = $"FOREIGN-CONTENT-{Guid.NewGuid():N}";

        await CreateFeatureAsync(_own, "feat");
        await CreateFeatureAsync(_foreign, "feat");

        _ownSpec = await CreateTwoVersionArtifactAsync(_own, "spec");
        _foreignSpec = await CreateTwoVersionArtifactAsync(_foreign, "spec", _foreignMarker);
        _ownTask = await CreateArtifactAsync(_own, new { Kind = "task", Body = "<goal>\nOwn task.\n</goal>" });
        _foreignTask = await CreateArtifactAsync(
            _foreign, new { Kind = "task", Body = $"<goal>\n{_foreignMarker}\n</goal>" });
        _ownPlan = await CreateArtifactAsync(_own, new { Kind = "plan", Body = "<goal>\nOwn plan.\n</goal>" });
        _foreignPlan = await CreateArtifactAsync(
            _foreign, new { Kind = "plan", Body = $"<goal>\n{_foreignMarker}\n</goal>" });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task ReadArtifact_ForeignArtifact_RefusedAndContentStaysHome()
    {
        var response = await AsOwnAsync(HttpMethod.Get, AgentReadUrl(_foreignSpec.Id));

        await AssertScopeRefusalAsync(response, "planning_read_artifact");
    }

    [Fact]
    public async Task ReadArtifact_OwnArtifact_AcceptedAndBodyRead()
    {
        var response = await AsOwnAsync(HttpMethod.Get, AgentReadUrl(_ownSpec.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, "the object-scope check must not touch own-project reads");
        var artifact = await response.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions);
        artifact!.Id.ShouldBe(_ownSpec.Id);
        artifact.Body.ShouldContain("Own goal.");
    }

    [Fact]
    public async Task ReadArtifactDiff_ForeignArtifact_Refused()
    {
        var response = await AsOwnAsync(
            HttpMethod.Get, $"{AgentReadUrl(_foreignSpec.Id)}/diff?fromVersion=1&toVersion=2");

        await AssertScopeRefusalAsync(response, "planning_read_artifact_diff");
    }

    [Fact]
    public async Task ReadArtifactDiff_OwnArtifact_Accepted()
    {
        var response = await AsOwnAsync(
            HttpMethod.Get, $"{AgentReadUrl(_ownSpec.Id)}/diff?fromVersion=1&toVersion=2");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, "the object-scope check must not touch own-project diffs");
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!["artifactId"]!.GetValue<Guid>()
            .ShouldBe(_ownSpec.Id);
    }

    [Fact]
    public async Task SetArtifactStatus_ForeignArtifact_RefusedAndStatusUntouched()
    {
        var response = await AsOwnAsync(
            HttpMethod.Post, AgentStatusUrl(_foreignTask.Id),
            new { Status = "inProgress", LogNote = "cross-board attempt" });

        await AssertScopeRefusalAsync(response, "planning_set_status");

        var untouched = await _client.GetFromJsonAsync<ArtifactJson>(HumanReadUrl(_foreignTask.Id), JsonOptions);
        untouched!.Status.ShouldBe("todo", "a refused status move must not have moved anything");
    }

    [Fact]
    public async Task SetArtifactStatus_OwnArtifact_Accepted()
    {
        var response = await AsOwnAsync(
            HttpMethod.Post, AgentStatusUrl(_ownTask.Id),
            new { Status = "inProgress", LogNote = "taking the task" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, "own-project status moves keep working through the guard");
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!["status"]!.GetValue<string>()
            .ShouldBe("inProgress");
    }

    [Fact]
    public async Task Validate_ForeignArtifact_RefusedBeforeTheValidationGate()
    {
        var response = await AsOwnAsync(
            HttpMethod.Post, AgentValidateUrl(_foreignSpec.Id),
            new { AsMigrated = true, LogNote = "pre-approved on disk" });

        // The gate's own refusal names the foreign feature's slug, so the scope check has to fire first:
        // the slug is a fact about another board and must not reach this conversation.
        await AssertScopeRefusalAsync(response, "planning_validate");
        var detail = (await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions))!.Detail!;
        detail.ShouldNotContain("not found", customMessage: "a refused object is not hidden behind a 404");
    }

    [Fact]
    public async Task Validate_OwnArtifact_Accepted()
    {
        var response = await AsOwnAsync(
            HttpMethod.Post, AgentValidateUrl(_ownPlan.Id),
            new { AsMigrated = true, LogNote = "frozen v1 on disk, user-approved" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, "the migrated escape hatch stays open for own objects");
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!["status"]!.GetValue<string>()
            .ShouldBe("validated");
    }

    [Fact]
    public async Task Validate_OwnArtifact_StillRequiresTheHumanApprovalProof()
    {
        var draft = await CreateDraftSpecAsync(_own, "gated");

        var response = await AsOwnAsync(HttpMethod.Post, AgentValidateUrl(draft.Id), new { });

        response.StatusCode.ShouldBe(
            HttpStatusCode.Conflict,
            "the scope check composes with the approval gate instead of replacing it");
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe(nameof(PlanningFailureCode.ValidationProofRequired),
            "an own-project artifact must be refused by the proof gate, never by the object-scope guard");
    }

    [Fact]
    public async Task Validate_OwnArtifact_WithFreshApprovalProof_Validates()
    {
        var draft = await CreateDraftSpecAsync(_own, "proof");
        await NotifyAndReplyAsync(_own, "proof");

        var response = await AsOwnAsync(
            HttpMethod.Post, AgentValidateUrl(draft.Id), new { LogNote = "approved by the owner" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!["status"]!.GetValue<string>()
            .ShouldBe("validated");
    }

    [Fact]
    public async Task DelegatedValidate_ForeignArtifact_RefusedOnTheArmedBranch()
    {
        var sessionId = await RegisterEmbeddedSessionAsync();
        await EnableDelegationAsync(_own);
        var instruction = await SendInstructionAsync(_own, "delegated-approve the foreign spec");

        using var request = new HttpRequestMessage(HttpMethod.Post, AgentValidateUrl(_foreignSpec.Id));
        request.Headers.Add("X-Overmind-Session", sessionId.ToString());
        request.Headers.Add(AgentLoopbackClient.DelegatedByHeader, instruction.ToString());
        AddCaller(request, _own.ToString("D"));

        await AssertScopeRefusalAsync(
            await _client.SendAsync(request), "planning_validate on the delegated branch");
    }

    [Fact]
    public async Task DelegatedValidate_OwnArtifact_StillCarriesTheDelegatedProvenance()
    {
        var sessionId = await RegisterEmbeddedSessionAsync();
        await EnableDelegationAsync(_own);
        var draft = await CreateDraftSpecAsync(_own, "delegated");
        var instruction = await SendInstructionAsync(_own, "delegated-approve the own draft spec");

        using var request = new HttpRequestMessage(HttpMethod.Post, AgentValidateUrl(draft.Id));
        request.Headers.Add("X-Overmind-Session", sessionId.ToString());
        request.Headers.Add(AgentLoopbackClient.DelegatedByHeader, instruction.ToString());
        AddCaller(request, _own.ToString("D"));

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "the scope guard must not break the armed branch it sits on");
        var artifact = await _client.GetFromJsonAsync<ArtifactJson>(HumanReadUrl(draft.Id), JsonOptions);
        artifact!.Body.ShouldContain($"owner message {instruction}");
    }

    [Fact]
    public async Task AppendLog_ForeignArtifact_RefusedAndLineNotWritten()
    {
        var response = await AsOwnAsync(
            HttpMethod.Post, AgentLogUrl(_foreignSpec.Id), new { Note = "cross-board note" });

        await AssertScopeRefusalAsync(response, "planning_append_log");

        var read = await _client.GetAsync($"{HumanReadUrl(_foreignSpec.Id)}?sections=log");
        (await read.Content.ReadAsStringAsync()).ShouldNotContain("cross-board note");
    }

    [Fact]
    public async Task AppendLog_OwnArtifact_Accepted()
    {
        var response = await AsOwnAsync(
            HttpMethod.Post, AgentLogUrl(_ownSpec.Id), new { Note = "progress on my own board" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, "own-project log appends keep working through the guard");
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!["lastLogLine"]!.GetValue<string>()
            .ShouldContain("progress on my own board");
    }

    [Fact]
    public async Task EditLog_ForeignArtifact_RefusedAndLineUntouched()
    {
        var response = await AsOwnAsync(
            HttpMethod.Put, AgentLogUrl(_foreignSpec.Id), new { Text = "rewritten line" });

        await AssertScopeRefusalAsync(response, "planning_edit_log");

        var read = await _client.GetAsync($"{HumanReadUrl(_foreignSpec.Id)}?sections=log");
        (await read.Content.ReadAsStringAsync()).ShouldNotContain("rewritten line");
    }

    [Fact]
    public async Task EditLog_OwnArtifact_Accepted()
    {
        await AsOwnAsync(HttpMethod.Post, AgentLogUrl(_ownSpec.Id), new { Note = "first line" });

        var response = await AsOwnAsync(
            HttpMethod.Put, AgentLogUrl(_ownSpec.Id), new { Text = "corrected line" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, "own-project log edits keep working through the guard");
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!["lastLogLine"]!.GetValue<string>()
            .ShouldContain("corrected line");
    }

    [Fact]
    public async Task PatchArtifact_ForeignArtifact_RefusedAndBodyUntouched()
    {
        var response = await AsOwnAsync(
            HttpMethod.Post, AgentPatchUrl(_foreignSpec.Id),
            new
            {
                Op = "replace_section",
                Section = "goal",
                Content = "<goal>\nRewritten by a foreign conversation.\n</goal>",
                LogNote = "cross-board patch",
            });

        await AssertScopeRefusalAsync(response, "planning_patch_artifact");

        var read = await _client.GetAsync(HumanReadUrl(_foreignSpec.Id));
        (await read.Content.ReadAsStringAsync()).ShouldNotContain("Rewritten by a foreign conversation");
    }

    [Fact]
    public async Task PatchArtifact_OwnArtifact_Accepted()
    {
        var response = await AsOwnAsync(
            HttpMethod.Post, AgentPatchUrl(_ownSpec.Id),
            new
            {
                Op = "replace_section",
                Section = "goal",
                Content = "<goal>\nRewritten on my own board.\n</goal>",
                LogNote = "own patch",
            });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, "own-project patches keep working through the guard");
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!["artifactId"]!.GetValue<Guid>()
            .ShouldBe(_ownSpec.Id);
    }

    [Fact]
    public async Task DeleteArtifact_ForeignArtifact_RefusedAndStillOnTheBoard()
    {
        var response = await AsOwnAsync(
            HttpMethod.Delete, AgentReadUrl(_foreignPlan.Id), new { LogNote = "cross-board delete" });

        await AssertScopeRefusalAsync(response, "planning_delete_artifact");

        var read = await _client.GetAsync(HumanReadUrl(_foreignPlan.Id));
        read.StatusCode.ShouldBe(HttpStatusCode.OK, "a refused deletion must leave the foreign artifact alone");
    }

    [Fact]
    public async Task DeleteArtifact_OwnArtifact_Accepted()
    {
        var doomed = await CreateArtifactAsync(_own, new { Kind = "result", Body = "<goal>\nDoomed.\n</goal>" });

        var response = await AsOwnAsync(
            HttpMethod.Delete, AgentReadUrl(doomed.Id), new { LogNote = "cleaning my own board" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, "own-project deletions keep working through the guard");
    }

    [Fact]
    public async Task ConversationWithoutAProject_ReadsForeignArtifact_Unchecked()
    {
        var response = await AgentCallAsync(
            HttpMethod.Get, AgentReadUrl(_foreignSpec.Id), AgentCallerHeaders.GlobalScopeValue);

        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "a project-less conversation carries the global sentinel and is the honest exception (R4)");
    }

    [Theory]
    [InlineData("marker without scope", true, false)]
    [InlineData("scope without marker", false, true)]
    public async Task HalfFormedCallerIdentity_RefusedFailClosed(string caseName, bool marker, bool scope)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AgentReadUrl(_ownSpec.Id));
        if (marker)
        {
            request.Headers.Add(AgentCallerHeaders.MarkerHeader, AgentCallerHeaders.MarkerValue);
        }

        if (scope)
        {
            request.Headers.Add(AgentCallerHeaders.ScopeHeader, _own.ToString("D"));
        }

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(
            HttpStatusCode.BadRequest, $"{caseName}: one half of the pair cannot be read as an agent call");
        var problem = await response.Content.ReadFromJsonAsync<ProblemJson>(JsonOptions);
        problem!.Title.ShouldBe(nameof(PlanningFailureCode.AgentCallerScopeUnverifiable));
    }

    [Fact]
    public async Task UnreadableCallerScope_RefusedWithoutEchoingTheValue()
    {
        var injected = "guessed-scope-value-75a0c7ff";

        var response = await AgentCallAsync(HttpMethod.Get, AgentReadUrl(_ownSpec.Id), injected);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await response.Content.ReadAsStringAsync();
        payload.ShouldNotContain(injected, customMessage: "the refusal echoes no attacker-shaped value");
        JsonNode.Parse(payload)!["title"]!.GetValue<string>()
            .ShouldBe(nameof(PlanningFailureCode.AgentCallerScopeUnverifiable));
    }

    [Fact]
    public async Task HumanPlanningRoutes_KeepCrossProjectFreedom()
    {
        var doors = new (string Name, HttpMethod Method, string Url, object? Body)[]
        {
            ("human read", HttpMethod.Get, HumanReadUrl(_foreignSpec.Id), null),
            ("human diff", HttpMethod.Get, $"{HumanReadUrl(_foreignSpec.Id)}/diff?fromVersion=1&toVersion=2", null),
            ("human versions", HttpMethod.Get, $"{HumanReadUrl(_foreignSpec.Id)}/versions", null),
            ("human status", HttpMethod.Post, $"{HumanReadUrl(_foreignTask.Id)}/status",
                new { Status = "inProgress", LogNote = "the UI works across boards" }),
            ("human append", HttpMethod.Post, $"{HumanReadUrl(_foreignSpec.Id)}/log",
                new { Note = "ui line on another board" }),
        };

        foreach (var door in doors)
        {
            using var request = new HttpRequestMessage(door.Method, door.Url);
            if (door.Body is not null)
            {
                request.Content = JsonContent.Create(door.Body, options: JsonOptions);
            }

            var response = await _client.SendAsync(request);
            var payload = await response.Content.ReadAsStringAsync();

            payload.ShouldNotContain(ScopeCode,
                customMessage: $"{door.Name} must stay free of the agent object-scope check");
            response.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                $"{door.Name} addresses another project with no caller pair — the human UI browses across " +
                $"boards (payload: {Truncate(payload)})");
        }
    }

    [Fact]
    public async Task SharedHumanRoute_WithCallerPair_IsScoped()
    {
        var response = await AgentCallAsync(
            HttpMethod.Get, $"{HumanReadUrl(_foreignSpec.Id)}/diff?fromVersion=1&toVersion=2", _own.ToString("D"));

        await AssertScopeRefusalAsync(response, "the shared diff handler");
    }

    [Fact]
    public async Task AdapterStyleCall_NoCallerPair_ReadsForeignArtifact()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AgentReadUrl(_foreignSpec.Id));

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "the external MCP adapter addresses boards by its own session project and sends no caller pair");
    }

    private async Task AssertScopeRefusalAsync(HttpResponseMessage response, string door)
    {
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(
            HttpStatusCode.Conflict, $"{door}: a foreign object id must be refused by the scope guard ({payload})");
        var problem = JsonSerializer.Deserialize<ProblemJson>(payload, JsonOptions);
        problem!.Title.ShouldBe(ScopeCode, door);
        problem.Detail!.ShouldContain(_foreign.ToString("D"),
            customMessage: $"{door}: the refusal names the owning project");
        problem.Detail!.ShouldContain(_own.ToString("D"),
            customMessage: $"{door}: the refusal names the conversation's project");
        problem.Detail!.ShouldNotContain(_foreignMarker,
            customMessage: $"{door}: a refusal never carries the foreign object's content");
        payload.ShouldNotContain(_foreignMarker, customMessage: door);
    }

    private static string Truncate(string payload) => payload.Length <= 200 ? payload : payload[..200];

    private void AddCaller(HttpRequestMessage request, string scopeValue)
    {
        request.Headers.Add(AgentCallerHeaders.MarkerHeader, AgentCallerHeaders.MarkerValue);
        request.Headers.Add(AgentCallerHeaders.ScopeHeader, scopeValue);
    }

    private Task<HttpResponseMessage> AsOwnAsync(HttpMethod method, string url, object? body = null) =>
        AgentCallAsync(method, url, _own.ToString("D"), body);

    private async Task<HttpResponseMessage> AgentCallAsync(
        HttpMethod method, string url, string scopeValue, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        AddCaller(request, scopeValue);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return await _client.SendAsync(request);
    }

    private async Task<Guid> RegisterEmbeddedSessionAsync()
    {
        using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<EmbeddedAgentSessionRegistrar>()
            .EnsureRegisteredAsync(CancellationToken.None);
    }

    private async Task EnableDelegationAsync(Guid projectId)
    {
        var updated = await _client.PutAsJsonAsync(
            $"/api/projects/{projectId}/approval-settings", new { DelegateApprovals = "On" }, JsonOptions);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<Guid> SendInstructionAsync(Guid projectId, string text)
    {
        var created = await _client.PostAsJsonAsync(
            "/api/agent/conversations", new { ProjectId = projectId, Title = "caller scope" }, JsonOptions);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var conversationId = (await created.Content.ReadFromJsonAsync<ConversationJson>(JsonOptions))!.Id;

        var streamed = await _client.PostAsJsonAsync(
            $"/api/agent/conversations/{conversationId}/messages/stream", new { Text = text }, JsonOptions);
        streamed.StatusCode.ShouldBe(HttpStatusCode.OK);

        var history = await _client.GetFromJsonAsync<HistoryJson>(
            $"/api/agent/conversations/{conversationId}/messages", JsonOptions);
        return history!.Items.Where(message => message.Role == "user").Select(message => message.Id).Last();
    }

    private async Task NotifyAndReplyAsync(Guid projectId, string featureSlug)
    {
        var create = await _client.PostAsJsonAsync(
            "/api/agent/notifications",
            new
            {
                Kind = "approval",
                Text = $"please approve {featureSlug}",
                ProjectId = projectId,
                FeatureSlug = featureSlug,
            }, JsonOptions);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var notificationId = JsonNode.Parse(await create.Content.ReadAsStringAsync())!["id"]!.GetValue<string>();

        var reply = await _client.PostAsJsonAsync(
            $"/api/notifications/{notificationId}/reply", new { Text = "lgtm" }, JsonOptions);
        reply.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // One spec per feature: a second spec of the same feature would upsert over the first.
    private async Task<ArtifactJson> CreateDraftSpecAsync(Guid projectId, string slug)
    {
        await CreateFeatureAsync(projectId, slug);
        return await CreateArtifactAsync(
            projectId, slug, new { Kind = "spec", Body = $"<goal>\nDraft spec of {slug}.\n</goal>" });
    }

    private async Task<ProjectJson> CreateProjectAsync(string name)
    {
        var tagged = $"{name}-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync(
            "/api/projects",
            new
            {
                Name = tagged,
                DiskPath = TestPaths.DiskPath(tagged),
                Description = (string?)null,
            }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions))!;
    }

    private async Task CreateFeatureAsync(Guid projectId, string slug)
    {
        var created = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            new { Slug = slug, Title = "Caller scope fixture", Body = "" }, JsonOptions);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<ArtifactJson> CreateArtifactAsync(Guid projectId, string slug, object body)
    {
        var created = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features/{slug}/artifacts", body, JsonOptions);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await created.Content.ReadFromJsonAsync<ArtifactJson>(JsonOptions))!;
    }

    private Task<ArtifactJson> CreateArtifactAsync(Guid projectId, object body) =>
        CreateArtifactAsync(projectId, "feat", body);

    // The second upsert of the same kind is an update, so it answers 200 and carries no id back.
    private async Task<ArtifactJson> CreateTwoVersionArtifactAsync(
        Guid projectId, string kind, string? goal = null)
    {
        var first = await CreateArtifactAsync(projectId, new { Kind = kind, Body = "first version" });
        await UpsertArtifactAsync(
            projectId,
            new { Kind = kind, Body = $"<goal>\n{goal ?? "Own goal."}\n</goal>\n<log>\n(none)\n</log>" });
        return await _client.GetFromJsonAsync<ArtifactJson>(AgentReadUrl(first.Id), JsonOptions)
            ?? throw new InvalidOperationException("the fixture artifact vanished");
    }

    private async Task UpsertArtifactAsync(Guid projectId, object body)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features/feat/artifacts", body, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static string AgentReadUrl(Guid artifactId) => $"/api/agent/planning/artifacts/{artifactId}";

    private static string AgentStatusUrl(Guid artifactId) => $"/api/agent/planning/artifacts/{artifactId}/status";

    private static string AgentValidateUrl(Guid artifactId) =>
        $"/api/agent/planning/artifacts/{artifactId}/validate";

    private static string AgentLogUrl(Guid artifactId) => $"/api/agent/planning/artifacts/{artifactId}/log";

    private static string AgentPatchUrl(Guid artifactId) => $"/api/agent/planning/artifacts/{artifactId}/patch";

    private static string HumanReadUrl(Guid artifactId) => $"/api/planning/artifacts/{artifactId}";

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath);

    private sealed record ArtifactJson(Guid Id, string Kind, string Status, string Body);

    private sealed record ProblemJson(string? Title, int? Status, string? Detail);

    private sealed record ConversationJson(Guid Id, Guid? ProjectId, string? Title);

    private sealed record HistoryJson(MessageJson[] Items);

    private sealed record MessageJson(Guid Id, string Role);
}
