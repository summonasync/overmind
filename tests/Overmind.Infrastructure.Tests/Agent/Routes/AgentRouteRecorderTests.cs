using Overmind.Infrastructure.Agent.Routes;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent.Routes;

public sealed class AgentRouteRecorderTests
{
    private static readonly Guid Conversation = Guid.NewGuid();

    private readonly AgentRouteMemoryStore _store = new();
    private readonly AgentRouteRecorder _recorder;

    public AgentRouteRecorderTests() => _recorder = new AgentRouteRecorder(_store);

    [Fact]
    public async Task RecordUnanalyzedAsync_WithoutAnalysis_WritesTheNormalizedRow()
    {
        _recorder.BeginTurn(Conversation);

        await _recorder.RecordUnanalyzedAsync(Conversation, "  Sort   the BOARD!  ");

        var row = (await _store.HotHeadAsync(10)).Single();
        row.Pattern.ShouldBe("sort the board");
        row.Domain.ShouldBe(AgentRouteRecorder.UnknownDomain);
        row.MissReason.ShouldBe(AgentRouteRecorder.NotAnalyzedReason);
    }

    [Fact]
    public async Task RecordUnanalyzedAsync_AfterAnalysis_IsSkipped()
    {
        _recorder.BeginTurn(Conversation);

        await _recorder.RecordAnalysisAsync(Conversation, "board planning tasks", "planning", "miss");
        await _recorder.RecordUnanalyzedAsync(Conversation, "sort the board");

        var row = (await _store.HotHeadAsync(10)).Single();
        row.Pattern.ShouldBe("board planning tasks");
    }

    [Fact]
    public async Task RecordUnanalyzedAsync_RepeatedCalls_RecordTheRowOnce()
    {
        _recorder.BeginTurn(Conversation);

        await _recorder.RecordUnanalyzedAsync(Conversation, "sort the board");
        await _recorder.RecordUnanalyzedAsync(Conversation, "sort the board");

        (await _store.HotHeadAsync(10)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task BeginTurn_ResetsTheAnalysisMark()
    {
        _recorder.BeginTurn(Conversation);
        await _recorder.RecordAnalysisAsync(Conversation, "board planning tasks", "planning", "miss");

        _recorder.BeginTurn(Conversation);
        await _recorder.RecordUnanalyzedAsync(Conversation, "sort the board");

        var rows = await _store.HotHeadAsync(10);
        rows.Count.ShouldBe(2);
        rows.ShouldContain(row => row.MissReason == AgentRouteRecorder.NotAnalyzedReason);
    }

}
