using Overmind.Core;
using Overmind.Infrastructure.Entries;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Entries;

public sealed class GraphNodePositionWriterTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    [Fact]
    public async Task Pin_CreatesTheRow_AndRePinMovesIt()
    {
        await using var db = _temp.CreateContext();
        var writer = new GraphNodePositionWriter(db);
        var nodeId = Guid.NewGuid();

        await writer.SaveAsync([new(EntryLinkKind.Entry, nodeId, 10, 20, Pinned: true)], CancellationToken.None);

        var row = db.GraphNodePositions.Single();
        row.NodeKind.ShouldBe(EntryLinkKind.Entry);
        row.NodeId.ShouldBe(nodeId);
        row.X.ShouldBe(10);
        row.Y.ShouldBe(20);
        row.Pinned.ShouldBeTrue();

        var firstWrite = row.UpdatedAt;
        await writer.SaveAsync([new(EntryLinkKind.Entry, nodeId, 30, 40, Pinned: true)], CancellationToken.None);

        db.GraphNodePositions.Count().ShouldBe(1);
        db.GraphNodePositions.Single().X.ShouldBe(30);
        db.GraphNodePositions.Single().UpdatedAt.ShouldBeGreaterThan(firstWrite);
    }

    [Fact]
    public async Task Unpin_KeepsTheRow_DropsItOutOfThePinnedRead_AndKeepsTheCoordinates()
    {
        await using var db = _temp.CreateContext();
        var writer = new GraphNodePositionWriter(db);
        var nodeId = Guid.NewGuid();

        await writer.SaveAsync([new(EntryLinkKind.Project, nodeId, 10, 20, Pinned: true)], CancellationToken.None);
        await writer.SaveAsync([new(EntryLinkKind.Project, nodeId, 30, 40, Pinned: false)], CancellationToken.None);

        var row = db.GraphNodePositions.Single();
        row.Pinned.ShouldBeFalse("an unpin is a state on the row, not a delete — the spot survives with it");
        row.X.ShouldBe(30);
        row.Y.ShouldBe(40);

        var reader = new GraphReader(db, new EntryLinkTriage(db));
        var field = await reader.LoadAsync(new GraphReadRequest(), CancellationToken.None);
        field.Positions.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnpinOfANeverWrittenNode_StoresTheRowUnpinned()
    {
        await using var db = _temp.CreateContext();
        var writer = new GraphNodePositionWriter(db);
        var nodeId = Guid.NewGuid();

        await writer.SaveAsync([new(EntryLinkKind.Feature, nodeId, 5, 6, Pinned: false)], CancellationToken.None);

        var row = db.GraphNodePositions.Single();
        row.Pinned.ShouldBeFalse();
        row.X.ShouldBe(5);
    }

    [Fact]
    public async Task DuplicateWritesOverOneNode_InOneBatch_CollapseLastWins()
    {
        await using var db = _temp.CreateContext();
        var writer = new GraphNodePositionWriter(db);
        var nodeId = Guid.NewGuid();

        await writer.SaveAsync(
        [
            new(EntryLinkKind.Entry, nodeId, 1, 2, Pinned: true),
            new(EntryLinkKind.Entry, nodeId, 3, 4, Pinned: true),
        ], CancellationToken.None);

        var row = db.GraphNodePositions.Single();
        row.X.ShouldBe(3);
        row.Y.ShouldBe(4);
    }

    [Fact]
    public async Task MixedBatch_LandsWhole_InOneTransaction()
    {
        await using var db = _temp.CreateContext();
        var writer = new GraphNodePositionWriter(db);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await writer.SaveAsync(
        [
            new(EntryLinkKind.Entry, first, 1, 2, Pinned: true),
            new(EntryLinkKind.Entry, second, 3, 4, Pinned: false),
        ], CancellationToken.None);

        var rows = db.GraphNodePositions.ToList();
        rows.Count.ShouldBe(2);
        rows.Single(row => row.NodeId == first).Pinned.ShouldBeTrue();
        rows.Single(row => row.NodeId == second).Pinned.ShouldBeFalse();
        rows.Select(row => row.UpdatedAt).Distinct().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task TaskKindPosition_IsRefusedByTheNodeUnion()
    {
        await using var db = _temp.CreateContext();
        var writer = new GraphNodePositionWriter(db);

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => writer.SaveAsync(
            [new(EntryLinkKind.Task, Guid.NewGuid(), 1, 2, Pinned: true)], CancellationToken.None));
        db.GraphNodePositions.ShouldBeEmpty();
    }

    [Fact]
    public async Task EmptyBatch_IsANoOp()
    {
        await using var db = _temp.CreateContext();
        var writer = new GraphNodePositionWriter(db);

        await writer.SaveAsync([], CancellationToken.None);

        db.GraphNodePositions.ShouldBeEmpty();
    }

    public void Dispose() => _temp.Dispose();
}
