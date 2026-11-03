using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Overmind.Infrastructure.Agent.Routes;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent.Routes;

public sealed class RouteVectorIndexingStoreTests
{
    private readonly AgentRouteMemoryStore _inner = new();
    private readonly IRouteVectorIndex _index = Substitute.For<IRouteVectorIndex>();
    private readonly RouteVectorIndexingStore _store;

    public RouteVectorIndexingStoreTests() =>
        _store = new RouteVectorIndexingStore(_inner, _index, NullLogger<RouteVectorIndexingStore>.Instance);

    [Fact]
    public async Task RecordAsync_NewRow_WritesLedgerAndUpsertsThePatternPoint()
    {
        var row = await _store.RecordAsync(
            new AgentRouteRecording("sort board, backlog order", "planning", "keywords too specific"));

        await _index.Received(1).UpsertAsync(
            row.Id,
            "sort board, backlog order",
            new RouteVectorPayload("planning", null, "provisional", 1),
            Arg.Any<CancellationToken>());
        (await _inner.HotHeadAsync(10)).ShouldContain(r => r.Id == row.Id);
    }

    [Fact]
    public async Task RecordAsync_AnalysisRefresh_UpsertsTheSameRouteIdWithNewPayload()
    {
        var first = await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"));
        var second = await _store.RecordAsync(new AgentRouteRecording("sort board", "board ordering", "classification failed"));

        second.Id.ShouldBe(first.Id);
        await _index.Received(1).UpsertAsync(
            first.Id,
            "sort board",
            new RouteVectorPayload("planning", null, "provisional", 1),
            Arg.Any<CancellationToken>());
        await _index.Received(1).UpsertAsync(
            second.Id,
            "sort board",
            new RouteVectorPayload("board ordering", null, "provisional", 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_IndexFailure_LedgerRowSurvives()
    {
        _index
            .UpsertAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<RouteVectorPayload>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("qdrant is down"));

        var row = await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"));

        row.Pattern.ShouldBe("sort board");
        (await _inner.HotHeadAsync(10)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DemoteAsync_ExistingRow_DeletesThePointFromTheIndex()
    {
        var row = await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"));
        await _store.PromoteAsync(row.Id);

        (await _store.DemoteAsync(row.Id)).ShouldBeTrue();

        await _index.Received(1).DeleteAsync(row.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DemoteAsync_UnknownRow_DoesNotTouchTheIndex() =>
        (await _store.DemoteAsync(Guid.NewGuid())).ShouldBeFalse();

    [Fact]
    public async Task DemoteAsync_IndexFailure_LedgerDemoteSurvives()
    {
        var row = await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"));
        _index
            .DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("qdrant is down"));

        (await _store.DemoteAsync(row.Id)).ShouldBeTrue();
        (await _inner.HotHeadAsync(10)).Single().Status.ShouldBe(AgentRouteStatus.Provisional);
    }

    [Fact]
    public async Task PromoteAsync_DoesNotMirrorAnythingBeyondTheRecordUpsert()
    {
        var row = await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"));

        (await _store.PromoteAsync(row.Id)).ShouldBeTrue();

        await _index.Received(1).UpsertAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<RouteVectorPayload>(), Arg.Any<CancellationToken>());
        await _index.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearAsync_WithRows_ClearsTheIndex()
    {
        await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"));
        await _store.RecordAsync(new AgentRouteRecording("grimoire facts", "grimoire", "miss"));

        (await _store.ClearAsync()).ShouldBe(2);

        await _index.Received(1).ClearAsync(Arg.Any<CancellationToken>());
        (await _inner.HotHeadAsync(10)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ClearAsync_EmptyLedger_DoesNotTouchTheIndex()
    {
        (await _store.ClearAsync()).ShouldBe(0);

        await _index.DidNotReceive().ClearAsync(Arg.Any<CancellationToken>());
    }
}
