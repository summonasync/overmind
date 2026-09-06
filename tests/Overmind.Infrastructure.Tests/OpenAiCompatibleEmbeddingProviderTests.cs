using System.Net;
using System.Text;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class OpenAiCompatibleEmbeddingProviderTests
{
    [Fact]
    public async Task EmbedAsync_PostsToEmbeddingsEndpoint_WithBearerToken()
    {
        var handler = new OllamaEmbeddingProviderTests.RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"data":[{"index":0,"embedding":[1.0,2.0]}],"usage":{"total_tokens":5}}""",
                Encoding.UTF8,
                "application/json"),
        });
        var provider = new Embeddings.OpenAiCompatibleEmbeddingProvider(
            new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) },
            "http://127.0.0.1:1234/",
            "test-api-key");

        var vector = await provider.EmbedAsync("some text", CancellationToken.None);

        vector.ShouldBe([1.0f, 2.0f]);

        handler.Method.ShouldBe(HttpMethod.Post);
        handler.Uri.ShouldBe(new Uri("http://127.0.0.1:1234/v1/embeddings"));
        handler.Authorization.ShouldBe("Bearer test-api-key");
        handler.Body.ShouldContain($"\"model\":\"{Embeddings.OpenAiCompatibleEmbeddingProvider.DefaultModel}\"");
        handler.Body.ShouldContain("\"input\":\"some text\"");
    }

    [Fact]
    public async Task EmbedAsync_EmptyData_Throws()
    {
        var handler = new OllamaEmbeddingProviderTests.RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"data":[]}""", Encoding.UTF8, "application/json"),
        });
        var provider = new Embeddings.OpenAiCompatibleEmbeddingProvider(
            new HttpClient(handler), "https://api.openai.com", "key");

        await Should.ThrowAsync<InvalidOperationException>(
            () => provider.EmbedAsync("x", CancellationToken.None));
    }
}
