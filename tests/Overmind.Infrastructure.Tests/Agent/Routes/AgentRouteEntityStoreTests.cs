using Overmind.Infrastructure.Agent.Routes;
using Overmind.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent.Routes;

public sealed class AgentRouteEntityStoreTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly AgentRouteEntityStore _store;

    public AgentRouteEntityStoreTests()
    {
        _store = new AgentRouteEntityStore(_db.CreateContext());
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task RecordAsync_CreatesLearnedProvisionalRowWithOneHit()
    {
        var row = await _store.RecordAsync(
            new AgentRouteRecording("sort board", "planning", "no keywords in hot head"), CancellationToken.None);

        row.Pattern.ShouldBe("sort board");
        row.Domain.ShouldBe("planning");
        row.MissReason.ShouldBe("no keywords in hot head");
        row.TargetAgent.ShouldBeNull();
        row.Status.ShouldBe(AgentRouteStatus.Provisional);
        row.Source.ShouldBe(AgentRouteSource.Learned);
        row.HitCount.ShouldBe(1);
        row.LastHitAt.ShouldBe(row.CreatedAt);
    }

    [Fact]
    public async Task RecordAsync_SamePatternDedupsAndRefreshesTheAnalysis()
    {
        var first = await _store.RecordAsync(
            new AgentRouteRecording("sort board", "planning", "no keywords in hot head"), CancellationToken.None);

        var second = await _store.RecordAsync(
            new AgentRouteRecording("sort board", "board ordering", "classification failed"), CancellationToken.None);

        second.Id.ShouldBe(first.Id);
        second.HitCount.ShouldBe(2);
        second.Domain.ShouldBe("board ordering");
        second.MissReason.ShouldBe("classification failed");
        second.LastHitAt.ShouldBeGreaterThanOrEqualTo(first.LastHitAt);
        (await _store.HotHeadAsync(10, CancellationToken.None)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RecordAsync_EmptyAnalysisKeepsThePreviousValues()
    {
        await _store.RecordAsync(
            new AgentRouteRecording("sort board", "planning", "first miss"), CancellationToken.None);

        var deduped = await _store.RecordAsync(
            new AgentRouteRecording("sort board", "", ""), CancellationToken.None);

        deduped.Domain.ShouldBe("planning");
        deduped.MissReason.ShouldBe("first miss");
    }

    [Fact]
    public async Task RecordAsync_ThirdHitPromotesTheRowToConfirmed()
    {
        var first = await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"), CancellationToken.None);

        await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"), CancellationToken.None);
        var third = await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"), CancellationToken.None);

        first.Status.ShouldBe(AgentRouteStatus.Provisional);
        third.Status.ShouldBe(AgentRouteStatus.Confirmed);
        third.HitCount.ShouldBe(AgentRouteMemoryStore.PromotionHits);
    }

    [Fact]
    public async Task RecordAsync_BlankPattern_IsRejected()
    {
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _store.RecordAsync(new AgentRouteRecording("   ", "planning", "miss"), CancellationToken.None));
    }

    [Fact]
    public async Task RecordAsync_OverlongPattern_IsTruncatedToTheCap()
    {
        var row = await _store.RecordAsync(
            new AgentRouteRecording(new string('x', AgentRouteText.MaxPatternLength + 30), "planning", "miss"),
            CancellationToken.None);

        row.Pattern.Length.ShouldBe(AgentRouteText.MaxPatternLength);
    }

    [Fact]
    public async Task HotHeadAsync_OrdersByHitCountAndSlices()
    {
        var fewHits = await SeedWithHits("few hits", 1);
        var manyHits = await SeedWithHits("many hits", 4);
        var midHits = await SeedWithHits("mid hits", 2);

        var head = await _store.HotHeadAsync(2, CancellationToken.None);

        head.Count.ShouldBe(2);
        head[0].Id.ShouldBe(manyHits.Id);
        head[0].HitCount.ShouldBe(4);
        head[1].Id.ShouldBe(midHits.Id);
        head.ShouldNotContain(row => row.Id == fewHits.Id);
    }

    [Fact]
    public async Task PromoteAndDemote_FlipTheStatus()
    {
        var row = await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"), CancellationToken.None);

        (await _store.PromoteAsync(row.Id, CancellationToken.None)).ShouldBeTrue();
        (await _store.HotHeadAsync(10, CancellationToken.None)).Single().Status.ShouldBe(AgentRouteStatus.Confirmed);

        (await _store.DemoteAsync(row.Id, CancellationToken.None)).ShouldBeTrue();
        (await _store.HotHeadAsync(10, CancellationToken.None)).Single().Status.ShouldBe(AgentRouteStatus.Provisional);
    }

    [Fact]
    public async Task PromoteAsync_UnknownId_ReturnsFalse()
    {
        (await _store.PromoteAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeFalse();
        (await _store.DemoteAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task ClearAsync_RemovesEveryRow()
    {
        await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"), CancellationToken.None);
        await _store.RecordAsync(new AgentRouteRecording("grimoire facts", "grimoire", "miss"), CancellationToken.None);

        (await _store.ClearAsync(CancellationToken.None)).ShouldBe(2);
        (await _store.HotHeadAsync(10, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Rows_SurviveContextReopen()
    {
        var created = await _store.RecordAsync(
            new AgentRouteRecording("sort board", "planning", "miss"), CancellationToken.None);

        using var fresh = _db.CreateContext();
        var reopened = await new AgentRouteEntityStore(fresh)
            .HotHeadAsync(10, CancellationToken.None);

        reopened.Single().Id.ShouldBe(created.Id);
        reopened.Single().Status.ShouldBe(AgentRouteStatus.Provisional);
    }

    private async Task<AgentRouteRecord> SeedWithHits(string pattern, int records)
    {
        AgentRouteRecord row = null!;
        for (var record = 0; record < records; record++)
        {
            row = await _store.RecordAsync(new AgentRouteRecording(pattern, "planning", "miss"), CancellationToken.None);
        }

        return row;
    }
}
