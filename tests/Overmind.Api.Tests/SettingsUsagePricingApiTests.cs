using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Overmind.Host.Api.Contracts;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class SettingsUsagePricingApiTests : IDisposable
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public SettingsUsagePricingApiTests()
    {
        _factory = new ApiFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetUsagePricing_ReturnsClaudeDefaults()
    {
        var response = await _client.GetAsync("/api/settings/usage-pricing");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var pricing = await response.Content.ReadFromJsonAsync<UsagePricingResponse>();
        pricing!.Pricing.Count.ShouldBe(3);
        var opus = pricing.Pricing["claude-opus-5"];
        opus.Input.ShouldBe(5.00m);
        opus.Output.ShouldBe(25.00m);
        opus.CacheRead.ShouldBe(0.50m);
        opus.CacheWrite.ShouldBe(1.25m);
    }

    [Fact]
    public async Task PutUsagePricing_StoresAndReturnsTheCustomTable()
    {
        var response = await _client.PutAsJsonAsync("/api/settings/usage-pricing", new
        {
            pricing = new Dictionary<string, object>
            {
                ["glm-5"] = new { input = 1.5, output = 7.0, cacheRead = 0.15, cacheWrite = 0.3 }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stored = await _client.GetFromJsonAsync<UsagePricingResponse>("/api/settings/usage-pricing");
        stored!.Pricing.ShouldNotContainKey("claude-opus-5");
        var glm = stored.Pricing["glm-5"];
        glm.Input.ShouldBe(1.5m);
        glm.Output.ShouldBe(7.0m);
        glm.CacheRead.ShouldBe(0.15m);
        glm.CacheWrite.ShouldBe(0.3m);
    }

    [Fact]
    public async Task PutUsagePricing_EmptyTable_IsAllowed()
    {
        var response = await _client.PutAsJsonAsync("/api/settings/usage-pricing", new
        {
            pricing = new Dictionary<string, object>()
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stored = await _client.GetFromJsonAsync<UsagePricingResponse>("/api/settings/usage-pricing");
        stored!.Pricing.ShouldBeEmpty();
    }

    [Fact]
    public async Task PutUsagePricing_NegativePrice_ReturnsValidationProblemNamingTheField()
    {
        var response = await _client.PutAsJsonAsync("/api/settings/usage-pricing", new
        {
            pricing = new Dictionary<string, object>
            {
                ["glm-5"] = new { input = -1.0, output = 7.0, cacheRead = 0.15, cacheWrite = 0.3 }
            }
        });

        await AssertPricingErrorAsync(response, "must not be negative");
    }

    [Fact]
    public async Task PutUsagePricing_BlankModelName_ReturnsValidationProblemNamingTheField()
    {
        var response = await _client.PutAsJsonAsync("/api/settings/usage-pricing", new
        {
            pricing = new Dictionary<string, object>
            {
                ["   "] = new { input = 1.0, output = 7.0, cacheRead = 0.15, cacheWrite = 0.3 }
            }
        });

        await AssertPricingErrorAsync(response, "between 1 and");
    }

    private static async Task AssertPricingErrorAsync(HttpResponseMessage response, string fragment)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["errors"]!;
        errors["Pricing"].ShouldNotBeNull();
        errors["Pricing"]!.AsArray().First()!.GetValue<string>().ShouldContain(fragment);
    }
}
