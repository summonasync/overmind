using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// 246 T3. Entries are the one object family whose routes the web UI shares with agents: /api/entries/{id}
// is what web/src/features/entries/api.ts calls. So enforcement cannot key on the route — it keys on the
// agent marker, and the whole point of this file is that both halves hold at once: an embedded-agent call
// naming a foreign entry is refused, and a human call naming the same entry is served exactly as before.
public sealed class AgentEntryCallerScopeApiTests : IAsyncLifetime
{
    private readonly CountingFactory _factory = new();
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string ScopeCode = PlanningFailureCode.EntryOutsideCallerProject.ToString();
    private static readonly string UnverifiableCode = PlanningFailureCode.AgentCallerScopeUnverifiable.ToString();

    private Guid _own;
    private Guid _foreign;
    private Guid _ownEntry;
    private Guid _foreignEntry;
    private string _foreignMarker = string.Empty;

    public AgentEntryCallerScopeApiTests() => _client = _factory.CreateClient();

    public async Task InitializeAsync()
    {
        _own = await CreateProjectAsync("entry-scope-own");
        _foreign = await CreateProjectAsync("entry-scope-foreign");
        _foreignMarker = $"FOREIGN-ENTRY-BODY-{Guid.NewGuid():N}";

        _ownEntry = await CreateEntryAsync(_own, "Own note", "local detail");
        _foreignEntry = await CreateEntryAsync(_foreign, "Foreign note", _foreignMarker);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---- read_entry ----

    [Fact]
    public async Task ReadEntry_ForeignEntry_RefusedAndContentStaysHome() =>
        await AssertScopeRefusalAsync(await AsOwnAsync(HttpMethod.Get, EntryUrl(_foreignEntry)), "read_entry");

    [Fact]
    public async Task ReadEntry_OwnEntry_Accepted()
    {
        var response = await AsOwnAsync(HttpMethod.Get, EntryUrl(_ownEntry));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Own note");
    }

    // ---- update_entry ----

    [Fact]
    public async Task UpdateEntry_ForeignEntry_RefusedAndBodyUntouched()
    {
        var before = await (await AsOwnerOfForeignAsync(HttpMethod.Get, EntryUrl(_foreignEntry)))
            .Content.ReadAsStringAsync();

        var response = await AsOwnAsync(
            HttpMethod.Put, EntryUrl(_foreignEntry), UpdateBody("Foreign note", "injected rewrite"));

        await AssertScopeRefusalAsync(response, "update_entry");

        var after = await (await AsOwnerOfForeignAsync(HttpMethod.Get, EntryUrl(_foreignEntry)))
            .Content.ReadAsStringAsync();
        after.ShouldBe(before, "a refused update must leave the entry byte-for-byte as its owner knows it");
    }

    [Fact]
    public async Task UpdateEntry_OwnEntry_Accepted()
    {
        var response = await AsOwnAsync(
            HttpMethod.Put, EntryUrl(_ownEntry), UpdateBody("Own note", "local rewrite"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---- delete_entry ----

    // The destructive door is the one worth double-checking: a refused delete must leave both the entry
    // and no tombstone behind, because the handler writes the tombstone before it removes the row.
    [Fact]
    public async Task DeleteEntry_ForeignEntry_RefusedAndNoTombstoneBorn()
    {
        var response = await AsOwnAsync(
            HttpMethod.Delete, $"{EntryUrl(_foreignEntry)}?reason=hostile%20sweep");

        await AssertScopeRefusalAsync(response, "delete_entry");

        var still = await AsOwnerOfForeignAsync(HttpMethod.Get, EntryUrl(_foreignEntry));
        still.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await still.Content.ReadAsStringAsync()).ShouldContain(_foreignMarker);

        var graves = await AsOwnerOfForeignAsync(
            HttpMethod.Get, $"/api/projects/{_foreign}/entries?type=Tombstone");
        (await graves.Content.ReadAsStringAsync()).ShouldNotContain("hostile sweep");
    }

    [Fact]
    public async Task DeleteEntry_OwnEntry_Accepted()
    {
        var doomed = await CreateEntryAsync(_own, "Doomed note", "temporary");
        var response = await AsOwnAsync(
            HttpMethod.Delete, $"{EntryUrl(doomed)}?reason=cleanup");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The control for DeleteEntry_ForeignEntry_RefusedAndNoTombstoneBorn: the tombstone query does
        // return rows when a delete really happened, so an empty result there is evidence, not a filter
        // that silently matches nothing.
        var graves = await AsOwnAsync(HttpMethod.Get, $"/api/projects/{_own}/entries?type=Tombstone");
        (await graves.Content.ReadAsStringAsync()).ShouldContain("Doomed note");
    }

    // ---- entry_versions ----

    // This door never loaded the entry at all - it probed existence with AnyAsync and threw the answer
    // away - so it is the one place where "compare at the load point" had to be negotiated with the query.
    [Fact]
    public async Task EntryVersions_ForeignEntry_RefusedAndNoHistoryLeak()
    {
        await AsOwnerOfForeignAsync(
            HttpMethod.Put, EntryUrl(_foreignEntry), UpdateBody("Foreign note", "v2 of the note"));

        var response = await AsOwnAsync(HttpMethod.Get, VersionsUrl(_foreignEntry));

        await AssertScopeRefusalAsync(response, "entry_versions");
    }

    [Fact]
    public async Task EntryVersions_OwnEntry_Accepted()
    {
        await AsOwnAsync(HttpMethod.Put, EntryUrl(_ownEntry), UpdateBody("Own note", "second version"));

        var response = await AsOwnAsync(HttpMethod.Get, VersionsUrl(_ownEntry));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The version rows carry the superseded content, not the new body: after one update the history
        // holds what the entry used to be.
        var payload = await response.Content.ReadAsStringAsync();
        payload.ShouldContain("\"version\":1");
        payload.ShouldContain("local detail");
    }

    // ---- the human half of the contract ----

    [Fact]
    public async Task HumanCall_NoMarker_ReadsForeignEntry()
    {
        var response = await _client.GetAsync(EntryUrl(_foreignEntry));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain(_foreignMarker,
            customMessage: "the web UI shares this route; a header-less call is not an embedded-agent call");
    }

    [Fact]
    public async Task HumanCall_NoMarker_UpdatesAndDeletesForeignEntry()
    {
        var doomed = await CreateEntryAsync(_foreign, "Human cleanup target", "throwaway");

        var updated = await _client.PutAsJsonAsync(
            EntryUrl(doomed), UpdateBody("Human cleanup target", "rewritten by a human"), JsonOptions);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deleted = await _client.DeleteAsync($"{EntryUrl(doomed)}?reason=human%20cleanup");
        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HumanCall_NoMarker_ListsForeignEntryVersions()
    {
        var response = await _client.GetAsync(VersionsUrl(_foreignEntry));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---- fail-closed pair contract, re-proved on the shared route ----

    [Fact]
    public async Task HalfPair_OnEntryRoute_RefusedAsUnverifiable()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, EntryUrl(_foreignEntry));
        request.Headers.Add(AgentCallerHeaders.MarkerHeader, AgentCallerHeaders.MarkerValue);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, payload);
        JsonSerializer.Deserialize<ProblemJson>(payload, JsonOptions)!.Title
            .ShouldBe(UnverifiableCode, "a marker without a scope on a human-used route must not pass through");
    }

    [Fact]
    public async Task UnreadableScope_OnEntryRoute_RefusedWithoutEcho()
    {
        var response = await AgentCallAsync(HttpMethod.Get, EntryUrl(_foreignEntry), "not-a-guid");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, payload);
        JsonSerializer.Deserialize<ProblemJson>(payload, JsonOptions)!.Title.ShouldBe(UnverifiableCode);
        payload.ShouldNotContain("not-a-guid",
            customMessage: "the refusal must not echo the attacker-shaped value back");
    }

    [Fact]
    public async Task GlobalSentinel_ReadsForeignEntry_BecauseThereIsNothingToCompareAgainst()
    {
        var response = await AgentCallAsync(
            HttpMethod.Get, EntryUrl(_foreignEntry), AgentCallerHeaders.GlobalScopeValue);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---- the agent-prefixed mutation routes ----

    // Feature 184 moved update_entry and delete_entry onto /api/agent/entries while read_entry and
    // entry_versions stayed on the shared route. The guard lives in the handler, so both prefixes reach the
    // same code - but a test that only ever walked the shared path would not notice if a future change gave
    // the agent subgroup its own handler. These two pin the route the tools actually call.
    [Fact]
    public async Task UpdateEntry_AgentPrefixedRoute_ForeignEntry_Refused()
    {
        var response = await AsOwnAsync(
            HttpMethod.Put, AgentEntryUrl(_foreignEntry), UpdateBody("Foreign note", "injected via agent route"));

        await AssertScopeRefusalAsync(response, "update_entry on /api/agent/entries");
    }

    [Fact]
    public async Task DeleteEntry_AgentPrefixedRoute_ForeignEntry_Refused()
    {
        var response = await AsOwnAsync(
            HttpMethod.Delete, $"{AgentEntryUrl(_foreignEntry)}?reason=agent%20route%20probe");

        await AssertScopeRefusalAsync(response, "delete_entry on /api/agent/entries");
    }

    // ---- R6: the comparison must not cost a round-trip ----

    // read_entry's baseline is 2, not the 1 measured at T3: merging origin/main brought feature 184's
    // link projection into the same handler, so the door now runs one more query for everybody. The claim
    // under test is that the scope check adds nothing to whatever the door already costs, and the scoped
    // arm still has to equal the unscoped one.
    [Theory]
    [InlineData("read_entry", "get", 2)]
    [InlineData("entry_versions", "versions", 2)]
    public async Task CallerScopeComparison_AddsNoQueryToEntryReadDoors(
        string door, string verb, int baseline)
    {
        var unscoped = await CountAsync(verb, scoped: false);
        var scoped = await CountAsync(verb, scoped: true);

        // Pinned absolutely as well as compared: equality alone would pass if a query were added to both
        // shapes, and a baseline of zero would mean the counter never fired at all.
        unscoped.ShouldBe(baseline, $"{door}: baseline measured without a caller pair");
        scoped.ShouldBe(unscoped, $"{door}: the owning project must come off the query that already runs");
    }

    private async Task<int> CountAsync(string verb, bool scoped)
    {
        var url = verb == "get" ? EntryUrl(_foreignEntry) : VersionsUrl(_foreignEntry);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (scoped)
        {
            // The caller is the OWNING project here: the accepted shape, so the comparison really runs
            // and reaches the refusal branch's absence rather than short-circuiting on a 404.
            request.Headers.Add(AgentCallerHeaders.MarkerHeader, AgentCallerHeaders.MarkerValue);
            request.Headers.Add(AgentCallerHeaders.ScopeHeader, _foreign.ToString("D"));
        }

        _factory.Counter.Reset();
        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return _factory.Counter.Commands;
    }

    private async Task AssertScopeRefusalAsync(HttpResponseMessage response, string door)
    {
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(
            HttpStatusCode.Conflict, $"{door}: a foreign entry id must be refused by the scope guard ({payload})");
        var problem = JsonSerializer.Deserialize<ProblemJson>(payload, JsonOptions);
        problem!.Title.ShouldBe(ScopeCode, $"refusal code for {door}");
        payload.ShouldContain(_foreign.ToString("D"));
        payload.ShouldContain(_own.ToString("D"));
        payload.ShouldNotContain(_foreignMarker,
            customMessage: $"{door}: the refusal must not carry the entry body");
    }

    private static string EntryUrl(Guid entryId) => $"/api/entries/{entryId}";

    private static string AgentEntryUrl(Guid entryId) => $"/api/agent/entries/{entryId}";

    // The update contract requires a title even when only the body changes, so every PUT here states it.
    private static object UpdateBody(string title, string content) => new
    {
        Title = title,
        Content = content,
        Tags = (string[]?)null,
        SkillFiles = (object[]?)null,
    };

    private static string VersionsUrl(Guid entryId) => $"/api/entries/{entryId}/versions";

    private Task<HttpResponseMessage> AsOwnAsync(HttpMethod method, string url, object? body = null) =>
        AgentCallAsync(method, url, _own.ToString("D"), body);

    private Task<HttpResponseMessage> AsOwnerOfForeignAsync(HttpMethod method, string url, object? body = null) =>
        AgentCallAsync(method, url, _foreign.ToString("D"), body);

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

    private async Task<Guid> CreateEntryAsync(Guid projectId, string title, string content)
    {
        var created = await _client.PostAsJsonAsync(
            "/api/entries",
            new
            {
                ProjectId = projectId,
                Type = "Note",
                Title = title,
                Content = content,
                Tags = (string[]?)null,
                SkillFiles = (object[]?)null,
            }, JsonOptions);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await created.Content.ReadFromJsonAsync<EntryJson>(JsonOptions))!.Id;
    }

    private sealed record ProjectJson(Guid Id, string Name, string DiskPath);

    private sealed record EntryJson(Guid Id, string Title, string Content);

    private sealed record ProblemJson(string? Title, int? Status, string? Detail);

    // An ApiFactory that puts a command counter on the Host's own DbContext options, so the entry doors
    // - which have no service layer to instrument - can still be measured at the wire. The original
    // registration is replaced rather than wrapped: EF TryAdds its options, so a second AddDbContext
    // would be silently ignored, and a hand-written connection string would drift from the Host's.
    public sealed class CountingFactory : ApiFactory
    {
        public SixShapeCounter Counter { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                var registered = services.SingleOrDefault(
                    descriptor => descriptor.ServiceType == typeof(DbContextOptions<OvermindDbContext>));
                if (registered is not null)
                {
                    services.Remove(registered);
                }

                services.AddDbContext<OvermindDbContext>((provider, options) =>
                {
                    var databasePath = provider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(
                        )["Overmind:DatabasePath"];
                    options.UseSqlite($"Data Source={databasePath};Pooling=false");
                    options.AddInterceptors(Counter);
                });
            });
        }
    }

    public sealed class SixShapeCounter : DbCommandInterceptor
    {
        public int Commands { get; private set; }

        public void Reset() => Commands = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Commands++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands++;
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Commands++;
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Commands++;
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Commands++;
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Commands++;
            return ValueTask.FromResult(result);
        }
    }
}
