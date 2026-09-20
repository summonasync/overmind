using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Overmind.Core;
using Shouldly;
using Xunit;

namespace Overmind.Api.Tests;

public sealed class SettingsTestApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;
    private readonly FakeEmbeddingServer _ollama = new();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public SettingsTestApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _ollama.Dispose();
    }

    [Fact]
    public async Task Post_HealthyEmbeddingCandidate_ReportsEmbeddingUpWithProbedDimensions()
    {
        var response = await _client.PostAsJsonAsync("/api/settings/test", new
        {
            Provider = "ollama",
            OllamaBaseUrl = _ollama.BaseUrl,
            OllamaModel = "nomic-embed-text",
            QdrantEndpoint = "http://127.0.0.1:1",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<VectorSearchHealthReport>(JsonOptions);
        report.ShouldNotBeNull();
        report!.Embedding.Up.ShouldBeTrue();
        report.Embedding.Provider.ShouldBe("ollama");
        report.Embedding.Model.ShouldBe("nomic-embed-text");
        report.Embedding.Dimensions.ShouldBe(768);
        report.Embedding.Error.ShouldBeNull();
        var requestBody = _ollama.LastRequestBody.ShouldNotBeNull();
        requestBody.ShouldContain("nomic-embed-text");

        report.Qdrant.Up.ShouldBeFalse();
        report.Qdrant.Endpoint.ShouldBe("http://127.0.0.1:1");
        report.Qdrant.Error.ShouldNotBeNullOrWhiteSpace();
        report.Up.ShouldBeFalse();
    }

    [Fact]
    public async Task Post_DeadEndpoints_ReportsBothLegsDownWithErrors()
    {
        var response = await _client.PostAsJsonAsync("/api/settings/test", new
        {
            Provider = "ollama",
            OllamaBaseUrl = "http://127.0.0.1:1",
            OllamaModel = "missing-model",
            QdrantEndpoint = "http://127.0.0.1:1",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<VectorSearchHealthReport>(JsonOptions);
        report.ShouldNotBeNull();
        report!.Up.ShouldBeFalse();
        report.Qdrant.Up.ShouldBeFalse();
        report.Qdrant.Error.ShouldNotBeNullOrWhiteSpace();
        report.Embedding.Up.ShouldBeFalse();
        report.Embedding.Provider.ShouldBe("ollama");
        report.Embedding.Model.ShouldBe("missing-model");
        report.Embedding.Dimensions.ShouldBeNull();
        report.Embedding.Error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Post_EmptyBody_FallsBackToSavedSettings()
    {
        var response = await _client.PostAsJsonAsync("/api/settings/test", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<VectorSearchHealthReport>(JsonOptions);
        report.ShouldNotBeNull();
        report!.Embedding.Provider.ShouldBe("onnx");
        report.Embedding.Up.ShouldBeFalse();
        report.Embedding.Dimensions.ShouldBeNull();
    }

    [Fact]
    public async Task Post_RejectsUnknownProvider()
    {
        using var response = await _client.PostAsJsonAsync("/api/settings/test", new
        {
            Provider = "chromadb",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://127.0.0.1:11434")]
    public async Task Post_RejectsInvalidOllamaBaseUrl(string baseUrl)
    {
        using var response = await _client.PostAsJsonAsync("/api/settings/test", new
        {
            OllamaBaseUrl = baseUrl,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://127.0.0.1:6334")]
    public async Task Post_RejectsInvalidQdrantEndpoint(string endpoint)
    {
        using var response = await _client.PostAsJsonAsync("/api/settings/test", new
        {
            QdrantEndpoint = endpoint,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Post_RejectsBlankOllamaModel(string model)
    {
        using var response = await _client.PostAsJsonAsync("/api/settings/test", new
        {
            OllamaModel = model,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private sealed class FakeEmbeddingServer : IDisposable
    {
        private readonly HttpListener _listener;

        public string BaseUrl { get; }

        public string? LastRequestBody { get; private set; }

        public FakeEmbeddingServer()
        {
            BaseUrl = $"http://127.0.0.1:{ReserveFreePort()}";
            _listener = new HttpListener();
            _listener.Prefixes.Add($"{BaseUrl}/");
            _listener.Start();
            _ = ServeAsync();
        }

        public void Dispose()
        {
            ((IDisposable)_listener).Dispose();
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                LastRequestBody = await reader.ReadToEndAsync();

                var vector = string.Join(",", Enumerable.Repeat("0.1", 768));
                var payload = Encoding.UTF8.GetBytes($"{{\"embeddings\":[[{vector}]]}}");
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = payload.Length;
                await context.Response.OutputStream.WriteAsync(payload);
                context.Response.Close();
            }
        }

        private static int ReserveFreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            try
            {
                return ((IPEndPoint)probe.LocalEndpoint).Port;
            }
            finally
            {
                probe.Stop();
            }
        }
    }
}
