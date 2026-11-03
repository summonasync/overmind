using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class AgentRouteStoreTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly AgentRouteStore _store;

    public AgentRouteStoreTests()
    {
        _store = new AgentRouteStore(_db.CreateContext());
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task Record_LearnedRow_StartsProvisionalWithZeroHits()
    {
        var route = await RecordWaveRouteAsync();

        route.Status.ShouldBe(AgentRouteStatus.Provisional);
        route.Source.ShouldBe(AgentRouteSource.Learned);
        route.HitCount.ShouldBe(0);
        route.Pattern.ShouldBe("show wave chat unread");
        route.Domain.ShouldBe("wave");
        route.TargetAgent.ShouldBe("worker");
    }

    [Fact]
    public async Task Record_ManualRow_StartsConfirmed()
    {
        var route = await _store.RecordAsync(
            "deploy checklist", "release", "worker", "manual ops entry", AgentRouteSource.Manual,
            CancellationToken.None);

        route.Status.ShouldBe(AgentRouteStatus.Confirmed);
        route.Source.ShouldBe(AgentRouteSource.Manual);
    }

    [Fact]
    public async Task Record_SameNormalizedPattern_DeduplicatesAndRefreshesAnalysis()
    {
        var first = await RecordWaveRouteAsync();
        await _store.RecordAsync(
            "  Show WAVE-CHAT unread!!! ", "wave", "worker", "keyword rows missed the phrasing", AgentRouteSource.Learned,
            CancellationToken.None);

        var list = await _store.ListAsync(CancellationToken.None);

        list.Count.ShouldBe(1);
        var deduped = list.Single();
        deduped.Id.ShouldBe(first.Id);
        deduped.HitCount.ShouldBe(1);
        deduped.MissReason.ShouldBe("keyword rows missed the phrasing");
        deduped.LastHitAt.ShouldBeGreaterThanOrEqualTo(first.LastHitAt);
    }

    [Fact]
    public async Task Record_ThirdHit_PromotesToConfirmed()
    {
        await RecordWaveRouteAsync();
        await _store.RecordAsync("show wave chat unread", "wave", "worker", "miss one", AgentRouteSource.Learned, CancellationToken.None);
        var third = await _store.RecordAsync("show wave chat unread", "wave", "worker", "miss two", AgentRouteSource.Learned, CancellationToken.None);
        var fourth = await _store.RecordAsync("show wave chat unread", "wave", "worker", "miss three", AgentRouteSource.Learned, CancellationToken.None);

        fourth.Status.ShouldBe(AgentRouteStatus.Confirmed);
        fourth.HitCount.ShouldBe(AgentRoute.ConfirmationHits);
        fourth.Source.ShouldBe(AgentRouteSource.Learned);
    }

    [Fact]
    public async Task HotHead_ReturnsTopNByHitCountThenRecency()
    {
        await _store.RecordAsync("route alpha", "planning", "worker", "miss a", AgentRouteSource.Learned, CancellationToken.None);
        await _store.RecordAsync("route beta", "wave", "worker", "miss b", AgentRouteSource.Learned, CancellationToken.None);
        await _store.RecordAsync("route beta", "wave", "worker", "miss b2", AgentRouteSource.Learned, CancellationToken.None);
        await _store.RecordAsync("route gamma", "kits", "worker", "miss c", AgentRouteSource.Learned, CancellationToken.None);
        await _store.RecordAsync("route beta again", "wave", "worker", "miss b1", AgentRouteSource.Learned, CancellationToken.None);
        await _store.RecordAsync("route beta again", "wave", "worker", "miss b2", AgentRouteSource.Learned, CancellationToken.None);
        await _store.RecordAsync("route beta again", "wave", "worker", "miss b3", AgentRouteSource.Learned, CancellationToken.None);

        var head = await _store.HotHeadAsync(2, CancellationToken.None);

        head.Count.ShouldBe(2);
        head[0].Pattern.ShouldBe("route beta again");
        head[0].HitCount.ShouldBe(2);
        head[1].Pattern.ShouldBe("route beta");
        head[1].HitCount.ShouldBe(1);
    }

    [Fact]
    public async Task HotHead_ClampsLimitToStoreCap()
    {
        var head = await _store.HotHeadAsync(10_000, CancellationToken.None);

        head.ShouldBeEmpty();
    }

    [Fact]
    public async Task Promote_And_Demote_FlipStatus()
    {
        var route = await RecordWaveRouteAsync();

        (await _store.PromoteAsync(route.Id, CancellationToken.None)).ShouldBeTrue();
        (await _store.ListAsync(CancellationToken.None)).Single().Status.ShouldBe(AgentRouteStatus.Confirmed);

        (await _store.DemoteAsync(route.Id, CancellationToken.None)).ShouldBeTrue();
        (await _store.ListAsync(CancellationToken.None)).Single().Status.ShouldBe(AgentRouteStatus.Provisional);
    }

    [Fact]
    public async Task Promote_UnknownRoute_ReturnsFalse()
    {
        (await _store.PromoteAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeFalse();
        (await _store.DemoteAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task Clear_RemovesEveryRow()
    {
        await RecordWaveRouteAsync();
        await _store.RecordAsync("another learned ask", "planning", "worker", "miss", AgentRouteSource.Learned, CancellationToken.None);

        var deleted = await _store.ClearAsync(CancellationToken.None);

        deleted.ShouldBe(2);
        (await _store.ListAsync(CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Record_OverlongPattern_IsTruncatedToCap()
    {
        var overflow = new string('x', AgentRoute.MaxPatternLength + 50);

        var route = await _store.RecordAsync(overflow, "planning", "worker", "miss", AgentRouteSource.Learned, CancellationToken.None);

        route.Pattern.Length.ShouldBe(AgentRoute.MaxPatternLength);
    }

    private async Task<AgentRoute> RecordWaveRouteAsync() =>
        await _store.RecordAsync(
            "show wave chat unread", "wave", "worker", "no wave rows in hot head", AgentRouteSource.Learned,
            CancellationToken.None);
}
