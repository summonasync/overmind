using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// GET /api/graph as the human door hands it out: one payload, URL-backed filters that fail loudly on a value the
// projection cannot honour, and counts that describe the rows actually returned. The numbers below are pinned twice
// over — once as the generic "counts equal rows" invariant on every response, once as the fixture's exact split —
// so a widened feature set that quietly loses its Done edge cannot hide behind a matching-but-wrong count.
public sealed class GraphApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    // Projects P1/P2/P3, open features F1 (InProgress) and F3 (Draft), Done features F2 and F5, four entry nodes.
    // E1 links the open F1. E4 links BOTH Done features — the discriminating node: it carries two links that leave
    // the default view, so a link count would read 2 where the view-isolated count reads 1 (one node, two links).
    private Guid _p1, _p2, _p3;
    private Guid _f1, _f2, _f3, _f5;
    private Guid _e1, _e2, _e3, _e4;

    public GraphApiTests() => _client = _factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();

        var alpha = Project.Create("alpha", "/tmp/graph/alpha");
        var beta = Project.Create("beta", "/tmp/graph/beta");
        var gamma = Project.Create("gamma", "/tmp/graph/gamma");
        db.Projects.AddRange(alpha, beta, gamma);
        _p1 = alpha.Id;
        _p2 = beta.Id;
        _p3 = gamma.Id;

        _e1 = Entry(db, _p1, EntryType.Journal, "Alpha note", "names [[feature:F1]]");
        _e2 = Entry(db, _p2, EntryType.Decision, "Beta decision", "a ruling, nothing linked");
        _e3 = Entry(db, _p1, EntryType.Idea, "Gamma idea", "half a thought");
        _e4 = Entry(db, _p1, EntryType.Journal, "Done link", "points at a finished feature");
        await db.SaveChangesAsync();

        var feature1 = Feature(_p1, "F1", "Alpha feature", PlanningFeatureStatus.InProgress);
        var feature2 = Feature(_p1, "F2", "Alpha archive", PlanningFeatureStatus.Done);
        var feature3 = Feature(_p2, "F3", "Beta feature", PlanningFeatureStatus.Draft);
        var feature5 = Feature(_p1, "F5", "Alpha archive two", PlanningFeatureStatus.Done);
        db.PlanningFeatures.AddRange(feature1, feature2, feature3, feature5);
        _f1 = feature1.Id;
        _f2 = feature2.Id;
        _f3 = feature3.Id;
        _f5 = feature5.Id;
        await db.SaveChangesAsync();

        // E4 carries two derived links, both to Done features the default filter hides — the node whose link count
        // (2) and whose isolated-in-view count (1) the review showed a name like OffViewLinks silently confuses.
        db.EntryLinks.AddRange(
            EntryLink.CreateDerived(_e1, EntryLinkKind.Feature, _f1),
            EntryLink.CreateDerived(_e4, EntryLinkKind.Feature, _f2),
            EntryLink.CreateDerived(_e4, EntryLinkKind.Feature, _f5));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task DefaultField_ReturnsNodesEdgesPositionsCounts_InOneRequest()
    {
        var payload = await Graph("?");

        payload.Nodes.ShouldNotBeEmpty();
        payload.Positions.ShouldBeEmpty();
        payload.Counts.Nodes.ShouldBe(payload.Nodes.Count);

        // Self-contained: every link the canvas must draw names two nodes that are already in this same payload,
        // so the view needs no per-node follow-up to render an edge.
        var nodeIds = payload.Nodes.Select(node => (node.Kind, node.Id)).ToHashSet();
        foreach (var edge in payload.Edges)
        {
            nodeIds.ShouldContain((edge.FromKind, edge.FromId));
            nodeIds.ShouldContain((edge.ToKind, edge.ToId));
        }

        AssertCountsMatchRows(payload);
        AssertSplit(payload, nodes: 9, edges: 1, orphans: 6, entryNodes: 4, projectNodes: 3, featureNodes: 2, isolatedInView: 1);
    }

    [Fact]
    public async Task WideningFilter_AddsTheDoneFeature_AndSurfacesItsHiddenEdge()
    {
        var widened = await Graph("?includeDone=true");

        AssertCountsMatchRows(widened);
        AssertSplit(widened, nodes: 11, edges: 3, orphans: 6, entryNodes: 4, projectNodes: 3, featureNodes: 4, isolatedInView: 0);

        // Both halves of the mechanism, asserted as numbers, not "it works": the Done features join only widened,
        // and E4's two edges to them are dropped from the default view and rendered once widened.
        widened.Nodes.ShouldContain(node => node.Id == _f2);
        widened.Nodes.ShouldContain(node => node.Id == _f5);
        var baseline = await Graph("?");
        baseline.Nodes.ShouldNotContain(node => node.Id == _f2);
        widened.Edges.ShouldContain(edge =>
            edge.FromId == _e4 && edge.ToKind == "feature" && edge.ToId == _f2);
        baseline.Edges.ShouldNotContain(edge => edge.FromId == _e4 || edge.ToId == _e4);
    }

    [Fact]
    public async Task Counts_AreTheRowsReturned_ForDefaultAndWidenedSets()
    {
        foreach (var query in new[] { "?", "?includeDone=true" })
        {
            var payload = await Graph(query);
            AssertCountsMatchRows(payload);
        }
    }

    [Fact]
    public async Task OrphansOnly_ReturnsEveryUnlinkedNode_AndNothingElse()
    {
        var payload = await Graph("?orphansOnly=true");

        AssertCountsMatchRows(payload);
        payload.Edges.ShouldBeEmpty();
        payload.Nodes.ShouldAllBe(node => node.Orphan);
        payload.Counts.Nodes.ShouldBe(payload.Counts.Orphans);

        // The same shared definition the full field marks: the orphan pile the door hands over is exactly the set
        // the projection flagged unlinked — P1/P2/P3, the decision, the idea, and the open feature nothing names.
        var orphans = payload.Nodes.Select(node => node.Id).OrderBy(id => id).ToList();
        orphans.ShouldBe(new[] { _p1, _p2, _p3, _e2, _e3, _f3 }.OrderBy(id => id).ToList());

        // E4 is not here: its edge to the Done feature is real, merely off-view, so it stays out of the orphan set.
        orphans.ShouldNotContain(_e4);
    }

    [Fact]
    public async Task TypeFilter_NarrowsEntryNodes_LeavingProjectsAndFeatures()
    {
        var payload = await Graph("?type=decision");

        AssertCountsMatchRows(payload);
        payload.Nodes.Where(node => node.Kind == "entry").Select(node => node.Id).ShouldBe(new[] { _e2 });
        payload.Nodes.ShouldContain(node => node.Kind == "project");
        payload.Counts.EntryNodes.ShouldBe(1);
        payload.Counts.ProjectNodes.ShouldBe(3);
    }

    [Fact]
    public async Task ProjectFilter_ByName_ScopesTheWholeField()
    {
        var payload = await Graph("?project=alpha");

        AssertCountsMatchRows(payload);
        payload.Nodes.ShouldAllBe(node => node.ProjectId == _p1);
        payload.Nodes.ShouldContain(node => node.Kind == "project" && node.Id == _p1);
        payload.Nodes.ShouldNotContain(node => node.Id == _p2);
        AssertSplit(payload, nodes: 5, edges: 1, orphans: 2, entryNodes: 3, projectNodes: 1, featureNodes: 1, isolatedInView: 1);
    }

    [Fact]
    public async Task ProjectFilter_UnknownValue_IsRefusedNotSilentlyEmpty()
    {
        var response = await _client.GetAsync("/api/graph?project=nowhere-project");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadProblem(response)).Title.ShouldBe("UnresolvedProjectFilter");
    }

    [Fact]
    public async Task Search_ReturnsMatchingNodes()
    {
        var payload = await Graph("?search=Alpha%20note");

        AssertCountsMatchRows(payload);
        payload.Nodes.Where(node => node.Kind == "entry").Select(node => node.Id).ShouldBe(new[] { _e1 });
    }

    [Fact]
    public async Task Search_NoMatch_IsAnHonestEmptyField()
    {
        var payload = await Graph("?search=zzzznothing");

        payload.Nodes.ShouldBeEmpty();
        payload.Edges.ShouldBeEmpty();
        payload.Counts.Nodes.ShouldBe(0);
        AssertCountsMatchRows(payload);
    }

    [Fact]
    public async Task FocusFromALinkedNode_WindowIsTheNeighbourhood()
    {
        var payload = await Graph($"?focus=entry:{_e1}&depth=1");

        AssertCountsMatchRows(payload);
        payload.Nodes.Select(node => node.Id).OrderBy(id => id).ToList()
            .ShouldBe(new[] { _e1, _f1 }.OrderBy(id => id).ToList());
        payload.Edges.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Focus_UnknownNode_IsRefused()
    {
        var response = await _client.GetAsync($"/api/graph?focus=entry:{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadProblem(response)).Title.ShouldBe("UnknownGraphFocus");
    }

    [Fact]
    public async Task FocusCoFilteredOutOfTheView_NamesTheCoFilter_NotTheNode()
    {
        // Review-237-03 m5, measured as ProbeC: E1 is a real node and 'search=zzzz' is what hid it, but the door
        // answered "focus names no node" — the owner gets blamed for the dot they clicked. The two 404s share a
        // code and must not share a sentence.
        var hidden = await _client.GetAsync($"/api/graph?focus=entry:{_e1}&search=zzzz");
        var hiddenProblem = await ReadProblem(hidden);

        hidden.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        hiddenProblem.Title.ShouldBe("UnknownGraphFocus");
        hiddenProblem.Detail.ShouldContain("names a node that exists");
        hiddenProblem.Detail.ShouldContain("'search'");

        var unknown = await _client.GetAsync($"/api/graph?focus=entry:{Guid.NewGuid()}");
        var unknownProblem = await ReadProblem(unknown);

        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownProblem.Detail.ShouldContain("names no node");
        unknownProblem.Detail.ShouldNotBe(hiddenProblem.Detail);
    }

    [Fact]
    public async Task FocusOnADoneFeatureInTheDefaultView_NamesTheWideningFilter()
    {
        // The second combination of the same defect: F2 exists and is a node, the default hides Done features.
        // Widening is the answer, so the detail has to say so rather than call the node unknown.
        var hidden = await _client.GetAsync($"/api/graph?focus=feature:{_f2}");
        var problem = await ReadProblem(hidden);

        hidden.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        problem.Title.ShouldBe("UnknownGraphFocus");
        problem.Detail.ShouldContain("names a node that exists");
        problem.Detail.ShouldContain("'includeDone'");

        var widened = await _client.GetAsync($"/api/graph?focus=feature:{_f2}&includeDone=true");
        widened.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("?type=bogus", "UnknownGraphTypeFilter")]
    [InlineData("?orphansOnly=maybe", "InvalidGraphFlag")]
    [InlineData("?includeDone=maybe", "InvalidGraphFlag")]
    [InlineData("?focus=not-a-handle", "InvalidGraphFocus")]
    [InlineData("?focus=task:11111111-1111-1111-1111-111111111111", "InvalidGraphFocus")]
    [InlineData("?depth=2", "DepthWithoutFocus")]
    public async Task UnknownFilterValue_NamesItself(string query, string expectedTitle)
    {
        var response = await _client.GetAsync($"/api/graph{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadProblem(response)).Title.ShouldBe(expectedTitle);
    }

    [Fact]
    public async Task FocusDepthZero_IsRefusedNotSilentlyFullField()
    {
        // Minor #4: the projection only windows when depth is above zero, so a raw depth=0 used to mean "ignore
        // the focus and hand the whole field" — the opposite of what zero hops reads as. The door refuses it.
        var response = await _client.GetAsync($"/api/graph?focus=entry:{_e1}&depth=0");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadProblem(response)).Title.ShouldBe("InvalidGraphDepth");
    }

    [Fact]
    public async Task OrphansOnlyWithFocus_IsRefusedAsContradictory()
    {
        // Minor #4, second half: orphans-only short-circuits before the focus window, so the two together would
        // silently drop the focus. Orphans have no edges by definition; a walk from one is not a thing the view
        // can answer, so it says so instead of quietly returning the pile.
        var response = await _client.GetAsync($"/api/graph?orphansOnly=true&focus=entry:{_e1}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadProblem(response)).Title.ShouldBe("OrphansWithFocus");
    }

    [Fact]
    public async Task IsolatedInView_CountsNodesNotLinks_OneNodeCarryingTwoOffViewLinksIsStillOne()
    {
        // B1's unit pin. E4 links two Done features the default view hides: a LINK count would report 2 here, the
        // isolated-in-view NODE count reports 1. Widening proves E4 really carries both links (they surface as two
        // rendered edges) yet once they render the node is no longer isolated — so the metric moves 1 -> 0 on the
        // NODE, never on the link tally. Without this case the 1/0 split is ambiguous and the name could lie.
        var defaultView = await Graph("?");
        defaultView.Counts.IsolatedInView.ShouldBe(1, "one node, even though it carries two off-view links");

        var widened = await Graph("?includeDone=true");
        var e4Links = widened.Edges.Count(edge => edge.FromId == _e4 && edge.ToKind == "feature");
        e4Links.ShouldBe(2, "the same node has two rendered links once widened — so a link count would be 2");
        widened.Counts.IsolatedInView.ShouldBe(0, "the node is no longer isolated in the widened view");

        // No node with a single off-view link can be mistaken for this metric: the count is per isolated node.
        defaultView.Nodes.Single(node => node.Id == _e4).Orphan.ShouldBeFalse();
    }

    [Fact]
    public async Task Payload_IsStable_AcrossRepeatedCalls()
    {
        // Minor #3: the projection's edge collapse is a Dictionary, so without ordering at the boundary the same
        // field could serialize links in a different order run to run — and a response-shape test would flake. The
        // widened view carries two edges, so the sorted-order assertion below is a real invariant, not a 1-edge
        // sequence that reads sorted in any order.
        var first = await _client.GetStringAsync("/api/graph?includeDone=true");
        var second = await _client.GetStringAsync("/api/graph?includeDone=true");
        first.ShouldBe(second);

        var edges = (await Graph("?includeDone=true")).Edges;
        edges.Count.ShouldBeGreaterThan(1);
        edges.ShouldBe(
            [.. edges.OrderBy(edge => (edge.FromKind, edge.FromId, edge.ToKind, edge.ToId))]);
    }

    [Fact]
    public async Task OneRequest_CostsNoFollowUp_AsNodesGrow()
    {
        // Widening adds a node and an edge but must add no command: the read is set-based, not per-node. Equal
        // statement counts across the two field sizes is the N+1 sentinel.
        var capture = new WriteCapture();
        using var measured = new ApiFactory(interceptor: capture);
        using var client = measured.CreateClient();
        await SeedInto(measured);

        capture.Clear();
        await client.GetAsync("/api/graph");
        var baselineCommands = capture.Statements.Count(statement =>
            statement.Sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase));

        capture.Clear();
        await client.GetAsync("/api/graph?includeDone=true");
        var widenedCommands = capture.Statements.Count(statement =>
            statement.Sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase));

        baselineCommands.ShouldNotBe(0);
        widenedCommands.ShouldBe(baselineCommands);
    }

    [Fact]
    public void GraphRoute_IsOnTheHumanDoor_NotTheAgentPrefix()
    {
        // The route-table invariant enumerates /api/agent/entries mutations; a read on /api/graph must not join it.
        // This names the placement so a future move of the verb under the agent prefix is caught here, not only by
        // the gate test.
        var endpoints = _factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .ToList();

        endpoints.ShouldContain(pattern => pattern == "/api/graph");
        endpoints.ShouldNotContain(pattern =>
            pattern.StartsWith("/api/agent", StringComparison.Ordinal) && pattern.Contains("graph"));
    }

    private async Task<GraphEnvelope> Graph(string query)
    {
        var response = await _client.GetAsync($"/api/graph{query}");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<GraphEnvelope>(JsonOptions);
        payload.ShouldNotBeNull();
        return payload!;
    }

    private static void AssertCountsMatchRows(GraphEnvelope payload)
    {
        payload.Counts.Nodes.ShouldBe(payload.Nodes.Count);
        payload.Counts.Edges.ShouldBe(payload.Edges.Count);
        payload.Counts.Orphans.ShouldBe(payload.Nodes.Count(node => node.Orphan));
        payload.Counts.EntryNodes.ShouldBe(payload.Nodes.Count(node => node.Kind == "entry"));
        payload.Counts.ProjectNodes.ShouldBe(payload.Nodes.Count(node => node.Kind == "project"));
        payload.Counts.FeatureNodes.ShouldBe(payload.Nodes.Count(node => node.Kind == "feature"));
    }

    private static void AssertSplit(
        GraphEnvelope payload, int nodes, int edges, int orphans,
        int entryNodes, int projectNodes, int featureNodes, int isolatedInView)
    {
        payload.Counts.Nodes.ShouldBe(nodes);
        payload.Counts.Edges.ShouldBe(edges);
        payload.Counts.Orphans.ShouldBe(orphans);
        payload.Counts.EntryNodes.ShouldBe(entryNodes);
        payload.Counts.ProjectNodes.ShouldBe(projectNodes);
        payload.Counts.FeatureNodes.ShouldBe(featureNodes);
        payload.Counts.IsolatedInView.ShouldBe(isolatedInView);
    }

    private static Guid Entry(OvermindDbContext db, Guid projectId, EntryType type, string title, string content)
    {
        var entry = ContextEntry.Create(projectId, type, title, content);
        db.ContextEntries.Add(entry);
        return entry.Id;
    }

    private static PlanningFeature Feature(Guid projectId, string slug, string title, PlanningFeatureStatus status) =>
        PlanningFeature.Restore(projectId, slug, title, "body", status, 1, DateTime.UtcNow, DateTime.UtcNow);

    private static async Task SeedInto(ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();

        var alpha = Project.Create("seeded", "/tmp/graph/seeded");
        db.Projects.Add(alpha);
        var entry = ContextEntry.Create(alpha.Id, EntryType.Journal, "Seeded note", "plain body");
        db.ContextEntries.Add(entry);
        var feature = PlanningFeature.Restore(
            alpha.Id, "SF", "Seeded feature", "body", PlanningFeatureStatus.Done, 1, DateTime.UtcNow, DateTime.UtcNow);
        db.PlanningFeatures.Add(feature);
        await db.SaveChangesAsync();
        db.EntryLinks.Add(EntryLink.CreateDerived(entry.Id, EntryLinkKind.Feature, feature.Id));
        await db.SaveChangesAsync();
    }

    private static async Task<ProblemDto> ReadProblem(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDto>(JsonOptions);
        problem.ShouldNotBeNull();
        return problem!;
    }

    public sealed record GraphNodeDto(string Kind, Guid Id, string Label, Guid ProjectId, bool Orphan);

    public sealed record GraphEdgeDto(string FromKind, Guid FromId, string ToKind, Guid ToId, bool HasManual);

    public sealed record GraphCountsDto(
        int Nodes, int Edges, int Orphans, int EntryNodes, int ProjectNodes, int FeatureNodes, int IsolatedInView);

    public sealed record GraphEnvelope(
        List<GraphNodeDto> Nodes,
        List<GraphEdgeDto> Edges,
        List<object> Positions,
        GraphCountsDto Counts);

    public sealed record ProblemDto(string? Title, string? Detail);
}
