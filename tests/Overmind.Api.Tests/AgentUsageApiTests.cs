using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentUsageApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Usage_WithBudgetOff_RemainingTodayIsNull()
    {
        using var factory = new ApiFactory();
        await SeedAsync(factory, dailyBudget: 0, spent: 0);
        using var client = factory.CreateClient();

        var usage = await client.GetFromJsonAsync<AgentUsageJson>("/api/agent/usage", JsonOptions);

        usage.ShouldNotBeNull();
        usage.DailyBudget.ShouldBe(0);
        usage.SpentToday.ShouldBe(0);
        usage.RemainingToday.ShouldBeNull();
        usage.PerTurnMaxTokens.ShouldBe(8192);
    }

    [Fact]
    public async Task Usage_WithPartialOutputSpend_ReportsRemainingBudget()
    {
        using var factory = new ApiFactory();
        await SeedAsync(factory, dailyBudget: 1000, spent: 400);
        using var client = factory.CreateClient();

        var usage = await client.GetFromJsonAsync<AgentUsageJson>("/api/agent/usage", JsonOptions);

        usage.ShouldNotBeNull();
        usage.DailyBudget.ShouldBe(1000);
        usage.SpentToday.ShouldBe(400);
        usage.RemainingToday.ShouldBe(600);
    }

    [Fact]
    public async Task Usage_WhenOutputBudgetExhausted_RemainingClampsToZero()
    {
        using var factory = new ApiFactory();
        await SeedAsync(factory, dailyBudget: 1000, spent: 1250);
        using var client = factory.CreateClient();

        var usage = await client.GetFromJsonAsync<AgentUsageJson>("/api/agent/usage", JsonOptions);

        usage.ShouldNotBeNull();
        usage.DailyBudget.ShouldBe(1000);
        usage.SpentToday.ShouldBe(1250);
        usage.RemainingToday.ShouldBe(0);
    }

    private static async Task SeedAsync(ApiFactory factory, int dailyBudget, long spent)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await using var scope = factory.Services.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<AppSettingsService>();
        await settings.SetAsync(
            SettingKeys.AgentDailyBudget,
            dailyBudget.ToString(CultureInfo.InvariantCulture),
            CancellationToken.None);
        if (spent > 0)
        {
            var budget = scope.ServiceProvider.GetRequiredService<IAgentBudgetStore>();
            await budget.AddUsageAsync(today, spent, CancellationToken.None);
        }
    }

    public sealed record AgentUsageJson(
        int DailyBudget,
        long SpentToday,
        long? RemainingToday,
        int PerTurnMaxTokens);
}
