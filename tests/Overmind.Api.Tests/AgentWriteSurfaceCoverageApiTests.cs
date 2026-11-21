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
    public void PositionsVerb_IsNotMappedYet_AgentOrHuman()
    {
        // T8 owns the position write. Until it lands there is nothing to attack, and a deny-status test against an
        // unmapped route would read as a guard while measuring a 404. When T8 maps PUT /api/graph/positions it
        // converts into the attack shape: same verb, registered route, typed refusal from the agent door.
        var mapped = AllEndpoints()
            .Where(endpoint => endpoint.RoutePattern.RawText!.Contains("positions", StringComparison.Ordinal))
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .Distinct()
            .ToList();

        mapped.ShouldBeEmpty();
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
