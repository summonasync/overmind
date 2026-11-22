using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Overmind.Host.Api;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// Spec R8's placement invariant, derived from the registered endpoint table. The sibling class
// JournalAgentWriteSurfaceApiTests attacks the gated /api/agent/entries subgroup; this one watches the rest of the
// agent prefix, because a verb registered there is invisible to that class's derivation and the boundary could move
// while its cases stayed green (review-237-03, forward duty 1).
//
// Two rules, both machine-checked against the table so nobody edits a verb list to move the boundary:
//  1. a journal-owned write (graph field, positions, triage promotion) is never reachable under /api/agent at all;
//  2. a write outside the gated subgroup cannot be an entry write — no vetted entry request contract, no route
//     value naming an entry — unless it is the one declared exemption whose write is additive and outside the body.
public sealed class AgentWriteSurfaceCoverageApiTests : IDisposable
{
    // Segments that name a journal-owned act. Deliberately not bare "promote": the issues board promotes cards into
    // tasks through the agent door by design, and that verb is not the owner's triage act.
    private static readonly string[] JournalOwnedMarkers = ["/graph", "/positions", "/triage", "/promote-to"];

    private static readonly IReadOnlyDictionary<string, string> EntryAddressingExemptions =
        new Dictionary<string, string>
        {
            ["/api/agent/projects/{projectId:guid}/entries/{entryId:guid}/ack"] =
                "An ack appends a readBy row to the entry's ack list and never writes the entry, so it sits " +
                "outside the gate on purpose — JournalAgentWriteSurfaceApiTests pins that it stays readable and writable.",
        };

    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void Derivation_SeesAgentWritesTheEntriesSubtreeFilterWouldHaveSkipped()
    {
        var writes = AgentWrites();
        var seenByTheSubtreeFilter = writes
            .Where(write => write.Pattern.StartsWith("/api/agent/entries", StringComparison.Ordinal))
            .ToList();

        // The denominator the gate's own class needs, so a prefix rename cannot empty this list silently.
        seenByTheSubtreeFilter.ShouldNotBeEmpty();
        seenByTheSubtreeFilter.ShouldContain(write => write.Method == "DELETE");

        List<AgentWrite> skippedByTheOldDerivation = [.. writes.Except(seenByTheSubtreeFilter)];
        skippedByTheOldDerivation.ShouldNotBeEmpty(
            "the whole-prefix derivation is vacuous if nothing is registered under /api/agent outside the " +
            "gated entries subgroup — if that ever becomes true, delete this rule rather than let it green on nothing");

        // A skipped case that could actually mutate something: a body-bound write, not an id-only route.
        skippedByTheOldDerivation.ShouldContain(write => write.BodyType != null);
    }

    [Theory]
    [InlineData("/graph")]
    [InlineData("/positions")]
    [InlineData("/triage")]
    [InlineData("/promote-to")]
    public void JournalOwnedWrites_AreNotReachableUnderTheAgentPrefix(string marker)
    {
        var offenders = AgentWrites()
            .Where(write => write.Pattern.Contains(marker, StringComparison.Ordinal))
            .Select(write => $"{write.Method} {write.Pattern}")
            .ToList();

        offenders.ShouldBeEmpty(
            $"spec R8 keeps the journal's own acts off the agent door; '{marker}' under /api/agent moves the " +
            "boundary and this class must name it. Mapping the verb onto the gated subgroup is not a fix either — " +
            "R8 says no agent-reachable route reaches them.");
    }

    [Fact]
    public void WritesOutsideTheGatedSubgroup_CannotBeEntryWrites()
    {
        var escapes = new List<string>();

        foreach (var write in AgentWrites())
        {
            if (write.Pattern.StartsWith("/api/agent/entries", StringComparison.Ordinal))
            {
                continue;
            }

            // The gate's own two reachability rules, checked where the gate is NOT hung: a body contract it knows
            // how to classify, or a route value that names an entry by id. A bare {id:guid} proves nothing on its
            // own — outside the entries subtree it names an artifact, a session, an issue, a conversation — so it
            // is deliberately not part of the rule, and the ack door is the only {entryId} write that stays open.
            var acceptsAnEntryBody = write.BodyType is not null &&
                JournalWriteLockFilter.VettedBodies.Contains(write.BodyType);
            var namesAnEntryRouteValue = write.NamesEntryRouteValue;

            if (!acceptsAnEntryBody && !namesAnEntryRouteValue)
            {
                continue;
            }

            if (!acceptsAnEntryBody && EntryAddressingExemptions.ContainsKey(write.Pattern))
            {
                continue;
            }

            escapes.Add(
                $"{write.Method} {write.Pattern} body {write.BodyType?.Name ?? "none"} " +
                $"(vetted entry body: {acceptsAnEntryBody}, route names entry id: {namesAnEntryRouteValue})");
        }

        // The shape this catches: an entry write registered on some other agent door, where the gate hangs on
        // nothing. It must move onto the gated subgroup or be declared above with the reason it is harmless.
        escapes.ShouldBeEmpty();
    }

    [Fact]
    public void DeclaredExemptions_AreStillRegistered()
    {
        List<string> patterns = [.. AllEndpoints().Select(endpoint => endpoint.RoutePattern.RawText!)];

        foreach (var exempt in EntryAddressingExemptions)
        {
            patterns.ShouldContain(exempt.Key,
                $"the exemption for '{exempt.Key}' is stale — the route is gone and the list must shrink");
        }
    }

    [Fact]
    public void PromotionVerbs_ExistOnTheHumanDoorAndNowhereElse()
    {
        List<string> patterns = [.. AllEndpoints().Select(endpoint => endpoint.RoutePattern.RawText!)];

        patterns.ShouldContain("/api/entries/{id:guid}/promote-to-feature");
        patterns.ShouldContain("/api/entries/{id:guid}/promote-to-task");

        patterns
            .Where(pattern => pattern.Contains("/promote-to", StringComparison.Ordinal))
            .ShouldBe(
                ["/api/entries/{id:guid}/promote-to-feature", "/api/entries/{id:guid}/promote-to-task"],
                "the promotion door is the owner's act; a second registration is a boundary move");
    }

    [Fact]
    public void PositionsVerb_IsMappedOnTheHumanDoorAndNowhereElse()
    {
        // T8 landed PUT /api/graph/positions on the human door. The conversion this row promised: the human route
        // is named once here, and the agent prefix carries no positions write at all — a twin appearing under
        // /api/agent redds JournalOwnedWrites_AreNotReachableUnderTheAgentPrefix above, and this assertion is what
        // catches a second, non-agent registration.
        var mapped = AllEndpoints()
            .Where(endpoint => endpoint.RoutePattern.RawText!.Contains("positions", StringComparison.Ordinal))
            .Select(endpoint => new
            {
                Pattern = endpoint.RoutePattern.RawText!,
                Methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.ToArray() ?? [],
            })
            .ToList();

        mapped.ShouldHaveSingleItem();
        mapped[0].Pattern.ShouldBe("/api/graph/positions");
        mapped[0].Methods.ShouldContain("PUT");

        AgentWrites()
            .Where(write => write.Pattern.Contains("positions", StringComparison.Ordinal))
            .ShouldBeEmpty("the agent door has no position verb and must never grow one");
    }

    [Fact]
    public async Task PositionsBody_ThroughTheRegisteredAgentEntryDoor_IsRefusedByShape_TheHumanDoorAcceptsTheSameBytes()
    {
        // The attack shape T5 pinned the duty for: the same PUT verb with a positions payload, against a route that
        // is really registered on the agent door (never a fabricated URL — a 404 on an unmapped twin proves a
        // missing route, not a guard). The gate classifies the target from the route's id before any body is read,
        // so a journal note answers the type rule's typed refusal and the positions payload is never interpreted.
        // The non-vacuity arm sends the very same bytes to the door they belong to, where they pin.
        using var client = _factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/projects", new
        {
            Name = "positions-attack",
            DiskPath = TestPaths.DiskPath("positions-attack"),
        });
        created.EnsureSuccessStatusCode();
        var project = await created.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = project.GetProperty("id").GetString()!;

        var note = await client.PostAsJsonAsync("/api/entries", new
        {
            projectId = Guid.Parse(projectId),
            type = "journal",
            title = "Owned dot",
            content = "the agent tried to move me",
        });
        note.EnsureSuccessStatusCode();
        var entry = await note.Content.ReadFromJsonAsync<JsonElement>();
        var noteId = entry.GetProperty("id").GetString()!;

        var positionsBody =
            $$"""{"positions":[{"kind":"entry","id":"{{noteId}}","x":11,"y":22,"pinned":true}]}""";
        var forged = await client.PutAsync(
            $"/api/agent/entries/{noteId}",
            new StringContent(positionsBody, Encoding.UTF8, "application/json"));

        var refusalBody = await forged.Content.ReadAsStringAsync();
        forged.StatusCode.ShouldBe(HttpStatusCode.BadRequest, refusalBody);
        var refusal = await forged.Content.ReadFromJsonAsync<JsonElement>();
        refusal.GetProperty("title").GetString().ShouldBe("JournalEntryWriteRefused");
        refusalBody.ShouldContain("Journal entries are the human surface");

        var untouched = await client.GetAsync($"/api/entries/{noteId}");
        untouched.EnsureSuccessStatusCode();
        (await untouched.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("content").GetString()
            .ShouldBe("the agent tried to move me", "the refusal arrives before any handler runs");

        var pinned = await client.PutAsync(
            "/api/graph/positions",
            new StringContent(positionsBody, Encoding.UTF8, "application/json"));
        pinned.StatusCode.ShouldBe(HttpStatusCode.NoContent,
            await pinned.Content.ReadAsStringAsync());

        var field = await client.GetFromJsonAsync<JsonElement>("/api/graph");
        field.GetProperty("positions").EnumerateArray().ShouldHaveSingleItem();
    }

    private sealed record AgentWrite(
        string Method, string Pattern, Type? BodyType, bool NamesEntryRouteValue);

    private List<AgentWrite> AgentWrites()
    {
        var writes = new List<AgentWrite>();

        foreach (var endpoint in AllEndpoints())
        {
            var pattern = endpoint.RoutePattern.RawText!;
            if (!pattern.StartsWith("/api/agent", StringComparison.Ordinal))
            {
                continue;
            }

            var verbs = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();
            if (verbs is null)
            {
                continue;
            }

            var body = endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType;

            foreach (var method in verbs.HttpMethods)
            {
                if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
                {
                    continue;
                }

                writes.Add(new AgentWrite(method, pattern, body, NamesEntryRouteValue(pattern)));
            }
        }

        return writes;
    }

    private IEnumerable<RouteEndpoint> AllEndpoints() =>
        _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

    // Only the explicit entry route value names an entry outside the entries subtree; a bare {id:guid} is whatever
    // its own slice says it is, and the gated subgroup is where {id:guid} means the entry being written.
    private static bool NamesEntryRouteValue(string pattern) =>
        pattern.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "{entryId}" or "{entryId:guid}");
}
