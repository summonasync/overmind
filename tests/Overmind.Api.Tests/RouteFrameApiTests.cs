using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class RouteFrameApiTests : IDisposable
{
    private readonly ApiFactory _baseFactory = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public RouteFrameApiTests()
    {
        _factory = _baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IConversationStreamSource>();
            services.AddSingleton<IConversationStreamSource>(new RoutedStubStreamSource());
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
    public async Task Stream_EmitsRouteFrameBeforeTheReply()
    {
        var created = await _client.PostAsJsonAsync("/api/agent/conversations", new { Title = "route frame" });
        created.EnsureSuccessStatusCode();
        var conversation = await created.Content.ReadFromJsonAsync<JsonElement>(SseTestClient.JsonOptions);
        var conversationId = conversation.GetProperty("id").GetGuid();

        var response = await SseTestClient.PostStreamAsync(_client, conversationId, "hello there");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var frames = await SseTestClient.ReadAllFramesAsync(response.Content);

        frames[0].Event.ShouldBe("route");
        using var payload = JsonDocument.Parse(frames[0].DataText);
        payload.RootElement.GetProperty("target").GetString().ShouldBe("worker");
        payload.RootElement.GetProperty("source").GetString().ShouldBe("learned");

        frames.First(frame => frame.Event == "delta").DataText.ShouldContain("stub delta one");
        frames.ShouldContain(frame => frame.Event == "completed");
    }

    private sealed class RoutedStubStreamSource : IConversationStreamSource
    {
        public async IAsyncEnumerable<ConversationStreamEvent> StreamAsync(
            Conversation conversation,
            IReadOnlyList<ConversationMessage> history,
            string userText,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default,
            RoutingMissContext? missContext = null)
        {
            await Task.Yield();
            yield return ConversationStreamEvent.Routed("worker", RoutingSource.Learned);
            yield return ConversationStreamEvent.Delta("stub delta one ");
            yield return ConversationStreamEvent.Completed(321);
        }
    }
}
