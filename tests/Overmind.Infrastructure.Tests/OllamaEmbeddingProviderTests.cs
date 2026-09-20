using System.Net;
using System.Text;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class OllamaEmbeddingProviderTests
{
    [Fact]
    public async Task EmbedAsync_PostsToEmbedEndpoint_WithModelAndInput()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"embeddings":[[0.1,0.2,0.3]]}""", Encoding.UTF8, "application/json"),
        });
        var provider = new Embeddings.OllamaEmbeddingProvider(
            new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) },
            "http://127.0.0.1:11434/");

        var vector = await provider.EmbedAsync("hello world", CancellationToken.None);

        vector.ShouldBe([0.1f, 0.2f, 0.3f]);

        handler.Method.ShouldBe(HttpMethod.Post);
        handler.Uri.ShouldBe(new Uri("http://127.0.0.1:11434/api/embed"));
        handler.Body.ShouldContain($"\"model\":\"{Embeddings.OllamaEmbeddingProvider.DefaultModel}\"");
        handler.Body.ShouldContain("\"input\":\"hello world\"");
    }

    [Fact]
    public async Task EmbedAsync_EmptyEmbeddings_Throws()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"embeddings":[]}""", Encoding.UTF8, "application/json"),
        });
        var provider = new Embeddings.OllamaEmbeddingProvider(new HttpClient(handler), "http://127.0.0.1:11434");

        await Should.ThrowAsync<InvalidOperationException>(
            () => provider.EmbedAsync("x", CancellationToken.None));
    }

    [Fact]
    public async Task EmbedAsync_SendsConfiguredModel()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"embeddings":[[0.5,0.5]]}""", Encoding.UTF8, "application/json"),
        });
        var provider = new Embeddings.OllamaEmbeddingProvider(
            new HttpClient(handler), "http://127.0.0.1:11434", "nomic-embed-text");

        await provider.EmbedAsync("hello", CancellationToken.None);

        provider.Model.ShouldBe("nomic-embed-text");
        handler.Body.ShouldContain("\"model\":\"nomic-embed-text\"");
    }

    [Fact]
    public async Task EmbedAsync_DiscoversDimensions_FromFirstSuccessfulEmbed()
    {
        var vector = string.Join(",", Enumerable.Repeat("0.1", 768));
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"embeddings\":[[{vector}]]}}", Encoding.UTF8, "application/json"),
        });
        var provider = new Embeddings.OllamaEmbeddingProvider(new HttpClient(handler), "http://127.0.0.1:11434");

        provider.Dimensions.ShouldBe(Embeddings.OllamaEmbeddingProvider.DefaultDimensions);

        var embedding = await provider.EmbedAsync("hello", CancellationToken.None);

        embedding.Length.ShouldBe(768);
        provider.Dimensions.ShouldBe(768);
    }

    [Fact]
    public async Task EmbedAsync_Dimensions_StickToFirstDiscoveredVector()
    {
        var longJson = $"{{\"embeddings\":[[{string.Join(",", Enumerable.Repeat("0.1", 768))}]]}}";
        var responses = new Queue<string>([longJson, "{\"embeddings\":[[0.1,0.2,0.3]]}"]);
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responses.Dequeue(), Encoding.UTF8, "application/json"),
        });
        var provider = new Embeddings.OllamaEmbeddingProvider(new HttpClient(handler), "http://127.0.0.1:11434");

        await provider.EmbedAsync("first", CancellationToken.None);
        await provider.EmbedAsync("second", CancellationToken.None);

        provider.Dimensions.ShouldBe(768);
    }

    internal sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        public HttpMethod? Method { get; private set; }

        public Uri? Uri { get; private set; }

        public string? Authorization { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Uri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return _respond(request);
        }
    }
}
