using System.ClientModel;
using System.Net;
using System.Text;
using Microsoft.Extensions.AI;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Llm;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class LlmChatClientFactoryTests : IDisposable
{
    private readonly TempDatabase _temp = new();
    private readonly LlmChatClientCache _cache = new();

    [Fact]
    public async Task CreateAsync_WithoutApiKey_ReturnsNullAndReportsUnconfigured()
    {
        await using var context = _temp.CreateContext();
        var factory = new LlmChatClientFactory(new Configuration.AppSettingsService(context), _cache);

        var client = await factory.CreateAsync(CancellationToken.None);

        client.ShouldBeNull();
        (await factory.IsConfiguredAsync(CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task IsConfiguredAsync_BlankApiKey_StaysUnconfigured()
    {
        await using var context = _temp.CreateContext();
        var settings = new Configuration.AppSettingsService(context);
        await settings.SetAsync(SettingKeys.AgentApiKey, "   ", CancellationToken.None);
        var factory = new LlmChatClientFactory(settings, _cache);

        (await factory.IsConfiguredAsync(CancellationToken.None)).ShouldBeFalse();
        (await factory.CreateAsync(CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_WithApiKey_ReturnsChatClientAndReportsConfigured()
    {
        await using var context = _temp.CreateContext();
        var settings = new Configuration.AppSettingsService(context);
        await settings.SetAsync(SettingKeys.AgentApiKey, "glm-secret", CancellationToken.None);
        var factory = new LlmChatClientFactory(settings, _cache);

        var client = await factory.CreateAsync(CancellationToken.None);

        client.ShouldNotBeNull();
        (await factory.IsConfiguredAsync(CancellationToken.None)).ShouldBeTrue();
    }

    [Fact]
    public async Task GetStreamingResponseAsync_OpenAiCompatibleStub_StreamsCompletion()
    {
        var handler = new OllamaEmbeddingProviderTests.RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SseBody, Encoding.UTF8, "text/event-stream"),
        });
        var client = LlmChatClientFactory.CreateChatClient(
            new HttpClient(handler),
            "http://127.0.0.1:19101/v4",
            "glm-secret",
            "glm-4.6");

        var parts = new List<string>();
        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            cancellationToken: CancellationToken.None))
        {
            parts.Add(update.Text);
        }

        string.Concat(parts).ShouldBe("Hello there");

        handler.Method.ShouldBe(HttpMethod.Post);
        handler.Uri.ShouldBe(new Uri("http://127.0.0.1:19101/v4/chat/completions"));
        handler.Authorization.ShouldBe("Bearer glm-secret");
        handler.Body.ShouldContain("\"model\":\"glm-4.6\"");
        handler.Body.ShouldContain("\"stream\":true");
    }

    [Fact]
    public async Task GetStreamingResponseAsync_ErrorStatus_Throws()
    {
        var handler = new OllamaEmbeddingProviderTests.RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"error":{"message":"bad key"}}""", Encoding.UTF8, "application/json"),
        });
        var client = LlmChatClientFactory.CreateChatClient(
            new HttpClient(handler), "http://127.0.0.1:19101/v4", "glm-secret", "glm-4.6");

        await Should.ThrowAsync<ClientResultException>(() => StreamAllAsync(client));
    }

    private static async Task StreamAllAsync(IChatClient client)
    {
        await foreach (var _ in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")],
            cancellationToken: CancellationToken.None))
        {
        }
    }

    private const string SseBody = """
        data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1700000000,"model":"glm-4.6","choices":[{"index":0,"delta":{"role":"assistant"},"finish_reason":null}]}

        data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1700000000,"model":"glm-4.6","choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}]}

        data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1700000000,"model":"glm-4.6","choices":[{"index":0,"delta":{"content":" there"},"finish_reason":null}]}

        data: {"id":"chatcmpl-1","object":"chat.completion.chunk","created":1700000000,"model":"glm-4.6","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

        data: [DONE]

        """;

    public void Dispose()
    {
        _cache.Dispose();
        _temp.Dispose();
    }
}
