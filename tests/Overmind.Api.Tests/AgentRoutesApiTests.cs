using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Overmind.Infrastructure.Agent.Routes;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class AgentRoutesApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public AgentRoutesApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task List_ReturnsRoutesOrderedByHitsWithoutAuth()
    {
        await SeedAsync("route plain", 1);
        await SeedAsync("route popular", 3);

        var response = await _client.GetAsync("/api/agent/routes");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AgentRoutesResponsePayload>();
        payload.ShouldNotBeNull();
        payload.Routes.Count.ShouldBe(2);
        payload.Routes[0].Pattern.ShouldBe("route popular");
        payload.Routes[0].HitCount.ShouldBe(3);
        payload.Routes[1].Status.ShouldBe("provisional");
        payload.Routes[1].Source.ShouldBe("learned");
        payload.Routes[1].TargetAgent.ShouldBeNull();
    }

    [Fact]
    public async Task Promote_ExistingRoute_ConfirmsIt()
    {
        var route = await SeedAsync("route promotable", 1);

        var response = await _client.PostAsync($"/api/agent/routes/{route.Id}/promote", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PromoteResponsePayload>();
        payload!.Status.ShouldBe("confirmed");

        var list = await ListAsync();
        list.Single().Status.ShouldBe("confirmed");
    }

    [Fact]
    public async Task Promote_UnknownRoute_ReturnsProblem()
    {
        var response = await _client.PostAsync($"/api/agent/routes/{Guid.NewGuid()}/promote", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Clear_RemovesEveryRoute()
    {
        await SeedAsync("route one", 1);
        await SeedAsync("route two", 2);

        var response = await _client.DeleteAsync("/api/agent/routes");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ClearResponsePayload>();
        payload!.Deleted.ShouldBe(2);
        (await ListAsync()).ShouldBeEmpty();
    }

    private async Task<AgentRouteRecord> SeedAsync(string pattern, int records)
    {
        using var scope = _factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAgentRouteStore>();
        AgentRouteRecord route = null!;
        for (var record = 0; record < records; record++)
        {
            route = await store.RecordAsync(
                new AgentRouteRecording(pattern, "wave", "seeded by the api test"), CancellationToken.None);
        }

        return route;
    }

    private async Task<List<RoutePayload>> ListAsync()
    {
        var response = await _client.GetAsync("/api/agent/routes");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AgentRoutesResponsePayload>();
        return payload!.Routes;
    }

    private sealed record AgentRoutesResponsePayload(List<RoutePayload> Routes);

    private sealed record RoutePayload(
        Guid Id,
        string Pattern,
        string Domain,
        string? TargetAgent,
        string MissReason,
        string Status,
        string Source,
        int HitCount,
        DateTimeOffset LastHitAt,
        DateTimeOffset CreatedAt);

    private sealed record PromoteResponsePayload(Guid Id, string Status);

    private sealed record ClearResponsePayload(int Deleted);
}
