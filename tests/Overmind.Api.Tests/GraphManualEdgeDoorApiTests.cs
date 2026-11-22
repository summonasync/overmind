using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

// R2's collapse, asserted where the owner actually reads it. GraphReaderTests pins that a manual row and a derived
// row over one pair render as one link with HasManual set; review-237-03 m7 noticed the wire field was never proven
// past the projection — it is mapped straight through (GraphMapper), so a broken mapping or a lost converter setting
// would read green at the service and lie on the canvas. The third note keeps the flag honest: it renders the same
// edge shape without a manual row, so hasManual cannot pass by being always true.
public sealed class GraphManualEdgeDoorApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    private Guid _projectId;
    private Guid _featureId;
    private Guid _manualSide;
    private Guid _derivedSide;
    private Guid _derivedOnly;

    public GraphManualEdgeDoorApiTests() => _client = _factory.CreateClient();

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OvermindDbContext>();

        var project = Project.Create("manual-edge", "/tmp/graph/manual-edge");
        db.Projects.Add(project);
        _projectId = project.Id;

        _manualSide = Note(db, project.Id, "Manual side", "notes the other one, and is named back");
        _derivedSide = Note(db, project.Id, "Derived side", "names [[Manual side]] in its body");
        _derivedOnly = Note(db, project.Id, "Derived only", "points at the feature");

        var feature = PlanningFeature.Restore(
            project.Id, "MF", "Manual edge feature", "body",
            PlanningFeatureStatus.InProgress, 1, DateTime.UtcNow, DateTime.UtcNow);
        db.PlanningFeatures.Add(feature);
        _featureId = feature.Id;

        await db.SaveChangesAsync();

        db.EntryLinks.Add(EntryLink.CreateManual(_manualSide, EntryLinkKind.Entry, _derivedSide));
        db.EntryLinks.Add(EntryLink.CreateDerived(_derivedSide, EntryLinkKind.Entry, _manualSide));
        db.EntryLinks.Add(EntryLink.CreateDerived(_derivedOnly, EntryLinkKind.Feature, _featureId));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ManualAndDerivedOverOnePair_ReachTheDoorAsOneEdgeFlaggedManual()
    {
        var payload = await Graph();

        var pair = payload.Edges
            .Where(edge =>
                (edge.FromId == _manualSide && edge.ToId == _derivedSide)
                || (edge.FromId == _derivedSide && edge.ToId == _manualSide))
            .ToList();

        pair.ShouldHaveSingleItem("R2: two stored rows over one pair render as one link");
        pair[0].HasManual.ShouldBeTrue();

        var derivedArm = payload.Edges.Single(edge =>
            (edge.FromId == _derivedOnly && edge.ToId == _featureId)
            || (edge.FromId == _featureId && edge.ToId == _derivedOnly));
        derivedArm.HasManual.ShouldBeFalse(
            "the flag is carried per edge — a blanket true would pass the assertion above and lie here");

        payload.Counts.Edges.ShouldBe(payload.Edges.Count);
    }

    private async Task<GraphEnvelope> Graph()
    {
        var response = await _client.GetAsync("/api/graph");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

        var payload = await response.Content.ReadFromJsonAsync<GraphEnvelope>(JsonOptions);
        payload.ShouldNotBeNull();
        return payload!;
    }

    private static Guid Note(OvermindDbContext db, Guid projectId, string title, string content)
    {
        var entry = ContextEntry.Create(projectId, EntryType.Journal, title, content);
        db.ContextEntries.Add(entry);
        return entry.Id;
    }

    private sealed record GraphEdgeDto(string FromKind, Guid FromId, string ToKind, Guid ToId, bool HasManual);

    private sealed record GraphEnvelope(List<GraphEdgeDto> Edges, GraphCountsDto Counts);

    private sealed record GraphCountsDto(
        int Nodes, int Edges, int Orphans, int EntryNodes, int ProjectNodes, int FeatureNodes, int IsolatedInView);
}
