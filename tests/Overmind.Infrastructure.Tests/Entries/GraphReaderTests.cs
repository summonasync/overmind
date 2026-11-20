using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Entries;

// A graph read must never invent a second link or orphan model. The fixtures below lean on that single source:
// the orphan set the projection returns is asserted equal to EntryLinkTriage.WhereUnlinked over the same
// population, so when T4 teaches CountingEdges about a new kind both consumers move together and this test moves
// with them. No fixture here creates a Task-kind edge, so none of these numbers shift under that edit.
public sealed class GraphReaderTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly OvermindDbContext _db;

    private readonly Guid _journalProject;
    private readonly Guid _alphaProject;
    private readonly Guid _betaProject;

    private readonly Guid _j1, _j2, _j3, _j4, _j5, _j6, _j7;
    private readonly Guid _d1, _d2;
    private readonly Guid _i1, _i2;
    private readonly Guid _f1, _f2, _f3, _f4, _f5;

    public GraphReaderTests()
    {
        _db = _temp.CreateContext();

        var journal = Project.Create("Journal", "/tmp/graph/journal");
        var alpha = Project.Create("alpha", "/tmp/graph/alpha");
        var beta = Project.Create("beta", "/tmp/graph/beta");
        _db.Projects.AddRange(journal, alpha, beta);
        _journalProject = journal.Id;
        _alphaProject = alpha.Id;
        _betaProject = beta.Id;

        _j1 = Add(_journalProject, EntryType.Journal, "Links feature", "see [[feature:F1]] twice [[feature:F1]]");
        _j2 = Add(_journalProject, EntryType.Journal, "Backlinked", "no marker");
        _j3 = Add(_journalProject, EntryType.Journal, "Backlink source", "reads [[Backlinked]]");
        _j4 = Add(_journalProject, EntryType.Journal, "Lonely note", "nothing to hold");
        _j5 = Add(_journalProject, EntryType.Journal, "Links buried note", "names a lifted decision");
        _j6 = Add(_journalProject, EntryType.Journal, "Links deleted feature", "names a feature that is gone");
        _j7 = Add(_journalProject, EntryType.Journal, "Unresolved marker", "points at [[Ghost Note]] which is nothing");
        _d1 = Add(_journalProject, EntryType.Decision, "Orphan decision", "standalone ruling");
        _d2 = Add(_alphaProject, EntryType.Decision, "Project decision", "owns [[project:alpha]]");
        _i1 = Add(_journalProject, EntryType.Idea, "Orphan idea", "half a thought");
        _i2 = Add(_alphaProject, EntryType.Idea, "Idea on done feature", "promoted to [[feature:F5]]");
        _db.SaveChanges();

        var f1 = Feature(alpha.Id, "F1", PlanningFeatureStatus.InProgress);
        var f2 = Feature(alpha.Id, "F2", PlanningFeatureStatus.Ready);
        var f3 = Feature(beta.Id, "F3", PlanningFeatureStatus.Draft);
        var f4 = Feature(alpha.Id, "F4", PlanningFeatureStatus.Done);
        var f5 = Feature(beta.Id, "F5", PlanningFeatureStatus.Done);
        var f6 = Feature(beta.Id, "F6", PlanningFeatureStatus.Draft);
        _db.PlanningFeatures.AddRange(f1, f2, f3, f4, f5, f6);
        _db.SaveChanges();
        _f1 = f1.Id;
        _f2 = f2.Id;
        _f3 = f3.Id;
        _f4 = f4.Id;
        _f5 = f5.Id;

        // The unique index on (FromEntryId, ToKind, ToId) forbids two rows for one directed pair — so the
        // "same pair, manual and derived" R2 collapses against is the legal one: two notes naming each other
        // back, one edge derived (J3→J2) and one manual (J2→J3). The buried/superseded/tombstone sources each
        // carry an outgoing edge that must vanish with them; the deleted feature loses its edge's target.
        var buried = Add(_alphaProject, EntryType.Decision, "Lifted decision", "the edge it carries is dead");
        var superseded = Add(_journalProject, EntryType.Journal, "Superseded note", "replaced but still on disk");
        _db.SaveChanges();
        var doomed = f6.Id;

        _db.EntryLinks.AddRange(
            EntryLink.CreateDerived(_j1, EntryLinkKind.Feature, f1.Id),
            EntryLink.CreateDerived(_j3, EntryLinkKind.Entry, _j2),
            EntryLink.CreateManual(_j2, EntryLinkKind.Entry, _j3),
            EntryLink.CreateDerived(_d2, EntryLinkKind.Project, alpha.Id),
            EntryLink.CreateDerived(_i2, EntryLinkKind.Feature, f5.Id),
            EntryLink.CreateDerived(_j5, EntryLinkKind.Entry, buried),
            EntryLink.CreateDerived(_j6, EntryLinkKind.Feature, doomed),
            EntryLink.CreateDerived(superseded, EntryLinkKind.Feature, f2.Id),
            EntryLink.CreateDerived(buried, EntryLinkKind.Feature, f3.Id));
        _db.SaveChanges();

        var tombstone = ContextEntry.CreateTombstone(f3TitleHolder(), PlanningLogSource.Ui, "ui", "f3 link author");
        _db.ContextEntries.Add(tombstone);
        _db.EntryLinks.Add(EntryLink.CreateDerived(tombstone.Id, EntryLinkKind.Feature, f3.Id));
        _db.SaveChanges();

        _db.ContextEntries.Single(e => e.Id == buried).Lift("replaced", "ui", null, null);
        _db.ContextEntries.Single(e => e.Id == superseded).Lift("replaced", "ui", null, _j2);
        _db.SaveChanges();

        _db.PlanningFeatures.Remove(_db.PlanningFeatures.Single(feature => feature.Id == doomed));
        _db.SaveChanges();

        ContextEntry f3TitleHolder() => _db.ContextEntries.Single(e => e.Id == _i1);
    }

    public void Dispose()
    {
        _db.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public async Task EntryOrphans_AreExactlyTheTriageUnlinkedSet_OverTheSamePopulation()
    {
        var field = await Reader().LoadAsync(new GraphReadRequest());

        var graphOrphans = field.Nodes
            .Where(node => node.Kind == EntryLinkKind.Entry && node.IsOrphan)
            .Select(node => node.Id)
            .OrderBy(id => id)
            .ToList();

        var population = _db.ContextEntries.Where(e =>
            (e.Type == EntryType.Journal || e.Type == EntryType.Decision || e.Type == EntryType.Idea)
            && e.State == ContextEntry.ActiveState
            && e.Type != EntryType.Tombstone);
        var triageUnlinked = await new EntryLinkTriage(_db).WhereUnlinked(population)
            .Select(e => e.Id)
            .OrderBy(id => id)
            .ToListAsync();

        graphOrphans.ShouldBe(triageUnlinked);

        // Non-vacuity: the split is real — six orphans against five linked nodes, neither side empty.
        graphOrphans.Count.ShouldBe(6);
        field.Nodes.Count(node => node.Kind == EntryLinkKind.Entry).ShouldBe(11);
        field.Nodes.Count(node => node.Kind == EntryLinkKind.Entry && !node.IsOrphan).ShouldBe(5);
    }

    [Fact]
    public async Task BuriedCounterparties_ContributeNeitherNodesNorEdges()
    {
        var field = await Reader().LoadAsync(new GraphReadRequest());
        var nodeIds = field.Nodes.Select(node => node.Id).ToHashSet();
        var edgeEndpoints = field.Edges.SelectMany(edge => new[] { edge.From.Id, edge.To.Id }).ToHashSet();

        // A lifted, superseded or tombstone-typed entry is not a node; a live one is, even when it stands alone.
        field.Nodes.Any(node => node.Label == "Lifted decision").ShouldBeFalse();
        field.Nodes.Any(node => node.Label == "Superseded note").ShouldBeFalse();
        nodeIds.ShouldContain(_i1);
        field.Nodes.Single(node => node.Id == _i1).IsOrphan.ShouldBeTrue();

        // An edge into a lifted note, a deleted feature, or one whose source is buried/superseded/tombstone all
        // drop out — none of them renders, and none rescues its dead endpoint into the field.
        edgeEndpoints.ShouldNotContain(_f2, "the only edge into F2 rides on a superseded source");
        edgeEndpoints.ShouldNotContain(_f3, "F3's edges ride on a lifted source and a tombstone");
        field.Nodes.Single(node => node.Id == _j5).IsOrphan.ShouldBeTrue();
        field.Nodes.Single(node => node.Id == _j6).IsOrphan.ShouldBeTrue();
    }

    [Fact]
    public async Task ManualAndDerivedBetweenOnePair_CollapseToOneManualBackedEdge()
    {
        var field = await Reader().LoadAsync(new GraphReadRequest());

        // J2→J3 (manual) and J3→J2 (derived) are two stored rows over one pair; the canvas gets a single link.
        var pair = field.Edges
            .Where(edge =>
                (edge.From.Id == _j2 && edge.To.Id == _j3)
                || (edge.From.Id == _j3 && edge.To.Id == _j2))
            .ToList();

        pair.ShouldHaveSingleItem();
        pair[0].HasManual.ShouldBeTrue();

        // No pair is ever rendered twice in opposite directions.
        field.Edges.Count.ShouldBe(field.Edges
            .Select(edge => edge.From.Id < edge.To.Id ? (edge.From.Id, edge.To.Id) : (edge.To.Id, edge.From.Id))
            .Distinct()
            .Count());
    }

    [Fact]
    public async Task FeatureNodes_DefaultToOpenAndWidenOnFlag_ByTheNumbers()
    {
        var open = await Reader().LoadAsync(new GraphReadRequest());
        var widened = await Reader().LoadAsync(new GraphReadRequest { IncludeDoneFeatures = true });

        open.Counts.FeatureNodes.ShouldBe(3);
        open.Counts.Nodes.ShouldBe(17);
        open.Counts.Edges.ShouldBe(3);
        open.Counts.Orphans.ShouldBe(10);
        open.Nodes.ShouldNotContain(node => node.Id == _f4);
        open.Nodes.ShouldNotContain(node => node.Id == _f5);

        widened.Counts.FeatureNodes.ShouldBe(5);
        widened.Counts.Nodes.ShouldBe(19);
        widened.Nodes.Count(node => node.FeatureStatus == PlanningFeatureStatus.Done).ShouldBe(2);

        // The widening edge is the one that hides the mechanism: a note linked only to a Done feature is a node
        // and NOT an orphan by default (the link is real, merely off-view), and the edge appears once widened.
        open.Nodes.Single(node => node.Id == _i2).IsOrphan.ShouldBeFalse();
        open.Edges.ShouldNotContain(edge => edge.To == Node(EntryLinkKind.Feature, _f5));
        widened.Nodes.Single(node => node.Id == _f5).IsOrphan.ShouldBeFalse();
        widened.Edges.ShouldContain(edge =>
            edge.From == Node(EntryLinkKind.Entry, _i2) && edge.To == Node(EntryLinkKind.Feature, _f5));
    }

    [Fact]
    public async Task UnresolvedMarker_NeverBecomesANodeAndLinksNothing()
    {
        var field = await Reader().LoadAsync(new GraphReadRequest());

        field.Nodes.ShouldNotContain(node => node.Label.Contains("Ghost", StringComparison.Ordinal));
        field.Nodes.ShouldNotContain(node => node.Label == "Ghost Note");

        // The note carrying the marker is a real node but an orphan: an unsaved [[..]] is not an edge anywhere.
        field.Nodes.Single(node => node.Id == _j7).IsOrphan.ShouldBeTrue();
    }

    [Fact]
    public async Task OrphansOnly_ReturnsEveryOrphanNodeAndNoEdges()
    {
        var field = await Reader().LoadAsync(new GraphReadRequest { OrphansOnly = true });

        field.Edges.ShouldBeEmpty();
        field.Positions.ShouldBeEmpty();
        field.Counts.Orphans.ShouldBe(field.Counts.Nodes);
        field.Nodes.ShouldAllBe(node => node.IsOrphan);
        field.Nodes.Count.ShouldBe(10);
        field.Nodes.ShouldContain(node => node.Id == _journalProject && node.Kind == EntryLinkKind.Project,
            "the Journal container holds notes but no edge names it, so it is an orphan — home is not link");
    }

    [Fact]
    public async Task Positions_AreEmptyUntilTheEntityLands()
    {
        var field = await Reader().LoadAsync(new GraphReadRequest());

        field.Positions.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProjectFilter_ScopesNodesToOneProject()
    {
        var field = await Reader().LoadAsync(new GraphReadRequest { ProjectId = _alphaProject });

        field.Nodes.ShouldContain(node => node.Kind == EntryLinkKind.Project && node.Id == _alphaProject);
        field.Nodes.ShouldNotContain(node => node.Kind == EntryLinkKind.Project && node.Id == _betaProject);
        field.Nodes.ShouldContain(node => node.Id == _d2);
        field.Nodes.ShouldNotContain(node => node.Id == _j4);
        field.Nodes.ShouldContain(node => node.Id == _f1);
        field.Nodes.ShouldNotContain(node => node.Id == _f3);

        // J1 (the journal container) is filtered out, so its outgoing edge to F1 loses a node endpoint and drops.
        field.Edges.ShouldNotContain(edge => edge.To == Node(EntryLinkKind.Feature, _f1));
    }

    [Fact]
    public async Task DepthFilter_KeepsNodesWithinHopsOfAFocus()
    {
        // J1's only live edge is J1 → F1 (the collapsed manual+derived pair). One hop from J1 reaches exactly
        // that pair; the J3 → J2 component and every orphan stay out of the window.
        var focus = Node(EntryLinkKind.Entry, _j1);
        var field = await Reader().LoadAsync(new GraphReadRequest { Focus = focus, Depth = 1 });

        field.Nodes.Select(node => node.Id).OrderBy(id => id)
            .ShouldBe(new[] { _j1, _f1 }.OrderBy(id => id).ToList());
        field.Nodes.ShouldNotContain(node => node.Id == _j3);
        field.Edges.ShouldHaveSingleItem();
        field.Edges[0].From.ShouldBe(focus);
        field.Edges[0].To.ShouldBe(Node(EntryLinkKind.Feature, _f1));
    }

    [Fact]
    public async Task UnknownFocus_YieldsAnEmptyField()
    {
        var field = await Reader().LoadAsync(new GraphReadRequest
        {
            Focus = Node(EntryLinkKind.Entry, Guid.NewGuid()),
            Depth = 2
        });

        field.Nodes.ShouldBeEmpty();
        field.Edges.ShouldBeEmpty();
    }

    private GraphReader Reader() => new(_db, new EntryLinkTriage(_db));

    private static GraphNodeId Node(EntryLinkKind kind, Guid id) => new(kind, id);

    private Guid Add(Guid projectId, EntryType type, string title, string content)
    {
        var entry = ContextEntry.Create(projectId, type, title, content);
        _db.ContextEntries.Add(entry);
        return entry.Id;
    }

    private static PlanningFeature Feature(Guid projectId, string slug, PlanningFeatureStatus status) =>
        PlanningFeature.Restore(projectId, slug, slug, "body", status, 1, DateTime.UtcNow, DateTime.UtcNow);
}
