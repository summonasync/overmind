using Overmind.Infrastructure.Agent.Routes;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent.Routes;

public sealed class AgentRouteMemoryStoreTests
{
    private readonly AgentRouteMemoryStore _store = new();

    [Fact]
    public async Task RecordAsync_CreatesLearnedProvisionalRow()
    {
        var row = await _store.RecordAsync(
            new AgentRouteRecording("sort board", "planning", "no keywords in hot head"));

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
            new AgentRouteRecording("sort board", "planning", "no keywords in hot head"));

        var second = await _store.RecordAsync(
            new AgentRouteRecording("sort board", "board ordering", "classification failed"));

        second.Id.ShouldBe(first.Id);
        second.HitCount.ShouldBe(2);
        second.Domain.ShouldBe("board ordering");
        second.MissReason.ShouldBe("classification failed");
        second.LastHitAt.ShouldBeGreaterThan(first.LastHitAt);
        (await _store.HotHeadAsync(10)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RecordAsync_ThirdHitPromotesTheRowToConfirmed()
    {
        var first = await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"));

        await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"));
        var third = await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"));

        first.Status.ShouldBe(AgentRouteStatus.Provisional);
        third.Status.ShouldBe(AgentRouteStatus.Confirmed);
        third.HitCount.ShouldBe(AgentRouteMemoryStore.PromotionHits);
    }

    [Fact]
    public async Task RecordAsync_DifferentPattern_CreatesSeparateRows()
    {
        await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"));
        await _store.RecordAsync(new AgentRouteRecording("grimoire facts", "grimoire", "miss"));

        var head = await _store.HotHeadAsync(10);

        head.Count.ShouldBe(2);
        head.Select(row => row.Pattern).ShouldBe(["grimoire facts", "sort board"], ignoreOrder: true);
    }

    [Fact]
    public async Task HotHeadAsync_OrdersByHitCountAndReturnsTheRequestedSlice()
    {
        var fewHits = await _store.RecordAsync(new AgentRouteRecording("few hits", "planning", "miss"));
        var manyHits = await _store.RecordAsync(new AgentRouteRecording("many hits", "planning", "miss"));
        await _store.RecordAsync(new AgentRouteRecording("many hits", "planning", "miss"));
        var midHits = await _store.RecordAsync(new AgentRouteRecording("mid hits", "planning", "miss"));
        await _store.RecordAsync(new AgentRouteRecording("many hits", "planning", "miss"));
        await _store.RecordAsync(new AgentRouteRecording("mid hits", "planning", "miss"));
        await _store.RecordAsync(new AgentRouteRecording("many hits", "planning", "miss"));

        var head = await _store.HotHeadAsync(2);

        head.Count.ShouldBe(2);
        head[0].Id.ShouldBe(manyHits.Id);
        head[1].Id.ShouldBe(midHits.Id);
        head.ShouldNotContain(row => row.Id == fewHits.Id);
    }

    [Fact]
    public async Task PromoteAndDemote_FlipTheStatus()
    {
        var row = await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"));

        (await _store.PromoteAsync(row.Id)).ShouldBeTrue();
        (await _store.HotHeadAsync(10)).Single().Status.ShouldBe(AgentRouteStatus.Confirmed);

        (await _store.DemoteAsync(row.Id)).ShouldBeTrue();
        (await _store.HotHeadAsync(10)).Single().Status.ShouldBe(AgentRouteStatus.Provisional);
    }

    [Fact]
    public async Task PromoteAsync_UnknownId_ReturnsFalse() =>
        (await _store.PromoteAsync(Guid.NewGuid())).ShouldBeFalse();

    [Fact]
    public async Task ClearAsync_RemovesEveryRow()
    {
        await _store.RecordAsync(new AgentRouteRecording("sort board", "planning", "miss"));
        await _store.RecordAsync(new AgentRouteRecording("grimoire facts", "grimoire", "miss"));

        (await _store.ClearAsync()).ShouldBe(2);
        (await _store.HotHeadAsync(10)).ShouldBeEmpty();
    }

    [Fact]
    public async Task RecordAsync_BlankPattern_IsRejected() =>
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _store.RecordAsync(new AgentRouteRecording("   ", "planning", "miss")));

    [Fact]
    public async Task SuggestedKeywords_CatchAPhrasingVariant()
    {
        await _store.RecordAsync(
            new AgentRouteRecording("backlog, board, order", "planning", "keywords too specific"));

        var head = await _store.HotHeadAsync(10);
        var row = head.Single();

        AgentRouteText.Matches("please order my backlog rows by priority", row.Pattern).ShouldBeTrue();
        AgentRouteText.Matches("what is the weather tomorrow", row.Pattern).ShouldBeFalse();
    }
}
