using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// 246 T2. The four issue doors address a stored issue by id, so the caller pair the embedded-agent
// transport sends is compared against the project that OWNS the issue. issues_append is the interesting
// one: it carries authorProjectId, which answers "who is speaking" — and that must not be mistaken for
// "whose issue this is", which is exactly the confusion 241's review had to untangle.
public sealed class AgentIssueCallerScopeApiTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string ScopeCode = PlanningFailureCode.IssueOutsideCallerProject.ToString();
    private static readonly string UnverifiableCode = PlanningFailureCode.AgentCallerScopeUnverifiable.ToString();

    private Guid _own;
    private Guid _foreign;
    private Guid _ownIssue;
    private Guid _foreignIssue;
    private string _foreignMarker = string.Empty;

    public AgentIssueCallerScopeApiTests() => _client = _factory.CreateClient();

    public async Task InitializeAsync()
    {
        _own = await CreateProjectAsync("issue-scope-own");
        _foreign = await CreateProjectAsync("issue-scope-foreign");
        _foreignMarker = $"FOREIGN-ISSUE-BODY-{Guid.NewGuid():N}";

        await CreateFeatureAsync(_own, "feat");
        await CreateFeatureAsync(_foreign, "feat");

        _ownIssue = await CreateIssueAsync(_own, "Own board card", "local detail");
        _foreignIssue = await CreateIssueAsync(_foreign, "Foreign board card", _foreignMarker);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetIssue_ForeignIssue_RefusedAndBodyStaysHome() =>
        await AssertScopeRefusalAsync(await AsOwnAsync(HttpMethod.Get, GetUrl(_foreignIssue)), "issues_get");

    [Fact]
    public async Task GetIssue_OwnIssue_Accepted()
    {
        var response = await AsOwnAsync(HttpMethod.Get, GetUrl(_ownIssue));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Own board card");
    }

    // The author is the conversation's OWN project and perfectly legitimate; the card still belongs to
    // somebody else. If this test ever passes, provenance was accepted in place of ownership.
    [Fact]
    public async Task AppendById_OwnAuthorOnForeignIssue_StillRefused()
    {
        var response = await AsOwnAsync(
            HttpMethod.Post, AppendUrl(_foreignIssue), new { AuthorProjectId = _own, Text = "injected follow-up" });

        await AssertScopeRefusalAsync(response, "issues_append");
        var after = await AsOwnerOfForeignAsync();
        (await after.Content.ReadAsStringAsync()).ShouldNotContain("injected follow-up");
    }

    [Fact]
    public async Task AppendById_OwnAuthorOnOwnIssue_Accepted()
    {
        var response = await AsOwnAsync(
            HttpMethod.Post, AppendUrl(_ownIssue), new { AuthorProjectId = _own, Text = "local follow-up" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetStatusById_ForeignIssue_RefusedAndUntouched()
    {
        // Compared byte-for-byte against the owner's own read, so the assertion does not depend on how
        // an enum happens to be serialized on this wire.
        var before = await (await AsOwnerOfForeignAsync()).Content.ReadAsStringAsync();

        var response = await AsOwnAsync(
            HttpMethod.Post, StatusUrl(_foreignIssue), new { Status = "Triaged", Source = "Agent" });

        await AssertScopeRefusalAsync(response, "issues_set_status");

        var after = await (await AsOwnerOfForeignAsync()).Content.ReadAsStringAsync();
        after.ShouldBe(before);
    }

    [Fact]
    public async Task SetStatusById_OwnIssue_Accepted()
    {
        var response = await AsOwnAsync(
            HttpMethod.Post, StatusUrl(_ownIssue), new { Status = "Triaged", Source = "Agent" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PromoteById_ForeignIssue_RefusedAndIssueStillNotPromoted()
    {
        var before = await (await AsOwnerOfForeignAsync()).Content.ReadAsStringAsync();

        var response = await AsOwnAsync(
            HttpMethod.Post, PromoteUrl(_foreignIssue), new { FeatureSlug = "feat", Source = "Agent" });

        await AssertScopeRefusalAsync(response, "issues_promote");

        var after = await (await AsOwnerOfForeignAsync()).Content.ReadAsStringAsync();
        after.ShouldBe(before,
            "a refused promotion must not have moved the issue or composed anything on the foreign board");
    }

    [Fact]
    public async Task PromoteById_OwnIssue_Accepted()
    {
        var response = await AsOwnAsync(
            HttpMethod.Post, PromoteUrl(_ownIssue), new { FeatureSlug = "feat", Source = "Agent" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // R2 as amended: the external MCP adapter addresses issues by id and sends no pair at all.
    [Fact]
    public async Task AdapterStyleCall_NoCallerPair_ReadsForeignIssue()
    {
        var response = await _client.GetAsync(GetUrl(_foreignIssue));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain(_foreignMarker,
            customMessage: "header-less calls are not embedded-agent calls; refusing them would break the MCP adapter");
    }

    // R4: a conversation with no project presents the pair with the `global` sentinel, and nothing is compared.
    [Fact]
    public async Task GlobalSentinel_ReadsForeignIssue_BecauseThereIsNothingToCompareAgainst()
    {
        var response = await AgentCallAsync(
            HttpMethod.Get, GetUrl(_foreignIssue), AgentCallerHeaders.GlobalScopeValue);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HalfPair_OnIssueRoute_RefusedAsUnverifiable()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, GetUrl(_foreignIssue));
        request.Headers.Add(AgentCallerHeaders.MarkerHeader, AgentCallerHeaders.MarkerValue);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, payload);
        JsonSerializer.Deserialize<ProblemJson>(payload, JsonOptions)!.Title
            .ShouldBe(UnverifiableCode, "a marker without a scope is a bug or a forgery, never a pass-through");
    }

    [Fact]
    public async Task UnreadableScope_OnIssueRoute_RefusedAsUnverifiable()
    {
        var response = await AgentCallAsync(HttpMethod.Get, GetUrl(_foreignIssue), "not-a-guid");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, payload);
        JsonSerializer.Deserialize<ProblemJson>(payload, JsonOptions)!.Title.ShouldBe(UnverifiableCode);
        payload.ShouldNotContain("not-a-guid",
            customMessage: "the refusal must not echo the attacker-shaped value back");
    }

    private async Task AssertScopeRefusalAsync(HttpResponseMessage response, string door)
    {
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(
            HttpStatusCode.Conflict, $"{door}: a foreign issue id must be refused by the scope guard ({payload})");
        var problem = JsonSerializer.Deserialize<ProblemJson>(payload, JsonOptions);
        problem!.Title.ShouldBe(ScopeCode, $"refusal code for {door}");
        payload.ShouldContain(_foreign.ToString("D"));
        payload.ShouldContain(_own.ToString("D"));
        payload.ShouldNotContain(_foreignMarker);
    }

    private static string GetUrl(Guid issueId) => $"/api/agent/issues/{issueId}";

    private static string AppendUrl(Guid issueId) => $"/api/agent/issues/{issueId}/append";

    private static string StatusUrl(Guid issueId) => $"/api/agent/issues/{issueId}/status";

    private static string PromoteUrl(Guid issueId) => $"/api/agent/issues/{issueId}/promote";

    private Task<HttpResponseMessage> AsOwnAsync(HttpMethod method, string url, object? body = null) =>
        AgentCallAsync(method, url, _own.ToString("D"), body);

    private async Task<HttpResponseMessage> AgentCallAsync(
        HttpMethod method, string url, string scopeValue, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add(AgentCallerHeaders.MarkerHeader, AgentCallerHeaders.MarkerValue);
        request.Headers.Add(AgentCallerHeaders.ScopeHeader, scopeValue);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return await _client.SendAsync(request);
    }

    private async Task<Guid> CreateProjectAsync(string name)
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
        return (await response.Content.ReadFromJsonAsync<ProjectJson>(JsonOptions))!.Id;
    }

    private async Task CreateFeatureAsync(Guid projectId, string slug)
    {
        var created = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/planning/features",
            new { Slug = slug, Title = "Issue scope fixture", Body = "" }, JsonOptions);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<Guid> CreateIssueAsync(Guid projectId, string title, string body)
    {
        var created = await _client.PostAsJsonAsync(
            $"/api/agent/projects/{projectId}/issues",
            new { AuthorProjectId = projectId, Title = title, Body = body }, JsonOptions);
        // The issue door answers 200 with the card id, not 201 — asserted rather than assumed.
        created.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await created.Content.ReadFromJsonAsync<IssueJson>(JsonOptions))!.Id;
    }

    // Reading as the owning project's caller is how these tests observe state without depending on
    // the mechanism under test: it is the same route, but the pair says "this is my board".
    private Task<HttpResponseMessage> AsOwnerOfForeignAsync() =>
        AgentCallAsync(HttpMethod.Get, GetUrl(_foreignIssue), _foreign.ToString("D"));

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath);

    private sealed record IssueJson(Guid Id, string Title, string Status);

    private sealed record ProblemJson(string? Title, int? Status, string? Detail);
}
