using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Overmind.Core;
using Overmind.Infrastructure.Agent;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class RoutingWiringApiTests : IDisposable
{
    private readonly ApiFactory _baseFactory = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RoutingWiringApiTests()
    {
        _factory = _baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IConversationStreamSource>();
            services.AddKeyedScoped<IConversationStreamSource, LlmConversationStreamSource>(
                RouterConversationStreamSource.WorkerStreamSourceKey);
            services.AddScoped<IConversationStreamSource>(sp =>
                sp.GetRequiredService<RouterConversationStreamSource>());
        }));
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _baseFactory.Dispose();
    }

    [Fact]
    public void Resolve_LiveConversationPath_ReturnsTheRouterSource()
    {
        using var scope = _factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IConversationStreamSource>()
            .ShouldBeOfType<RouterConversationStreamSource>();
    }

    [Fact]
    public void Resolve_WorkerLeg_ReturnsTheLlmSource()
    {
        _factory.Services.GetRequiredKeyedService<IConversationStreamSource>(
            RouterConversationStreamSource.WorkerStreamSourceKey).ShouldBeOfType<LlmConversationStreamSource>();
    }

    [Fact]
    public async Task FreeFormTurn_RoutesThroughTheRouterToEndToEnd()
    {
        var created = await _client.PostAsJsonAsync("/api/agent/conversations", new { Title = "router wiring" });
        created.EnsureSuccessStatusCode();
        var conversation = await created.Content.ReadFromJsonAsync<JsonElement>(SseTestClient.JsonOptions);
        var conversationId = conversation.GetProperty("id").GetGuid();

        var response = await SseTestClient.PostStreamAsync(_client, conversationId, "sort my board please");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var frames = await SseTestClient.ReadAllFramesAsync(response.Content);

        frames.ShouldNotBeEmpty();
        frames[0].Event.ShouldBe("route");
        using var payload = JsonDocument.Parse(frames[0].DataText);
        payload.RootElement.GetProperty("target").GetString().ShouldBe(RoutingTargets.Worker);
        frames.Last(frame => frame.Event is "completed" or "failed").Event.ShouldBe("completed");
    }
}
