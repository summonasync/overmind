using Overmind.Infrastructure.Agent;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests.Agent;

public sealed class AgentBudgetStoreTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    [Fact]
    public async Task AddUsageAsync_FirstWrite_CreatesTheRow()
    {
        var store = new AgentAppSettingsBudgetStore(_temp.CreateContext());
        var day = DateOnly.FromDateTime(DateTime.UtcNow);

        var used = await store.AddUsageAsync(day, 300, CancellationToken.None);

        used.ShouldBe(300);
        (await store.GetUsedAsync(day, CancellationToken.None)).ShouldBe(300);
    }

    [Fact]
    public async Task AddUsageAsync_ConcurrentWriters_BothDeltasLand()
    {
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        var first = new AgentAppSettingsBudgetStore(_temp.CreateContext());
        var second = new AgentAppSettingsBudgetStore(_temp.CreateContext());

        await Task.WhenAll(
            first.AddUsageAsync(day, 300, CancellationToken.None),
            second.AddUsageAsync(day, 500, CancellationToken.None));

        var after = new AgentAppSettingsBudgetStore(_temp.CreateContext());
        (await after.GetUsedAsync(day, CancellationToken.None)).ShouldBe(800);
    }

    [Fact]
    public async Task AddUsageAsync_UnknownDay_ReadsAsZero()
    {
        var store = new AgentAppSettingsBudgetStore(_temp.CreateContext());

        (await store.GetUsedAsync(new DateOnly(2031, 1, 2), CancellationToken.None)).ShouldBe(0);
    }

    public void Dispose() => _temp.Dispose();
}
