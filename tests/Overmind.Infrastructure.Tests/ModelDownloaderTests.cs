using System.Net;
using Overmind.Infrastructure.Embeddings;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class ModelDownloaderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"overmind-models-{Guid.NewGuid():N}");

    [Fact]
    public async Task EnsureAsync_SkipsDownload_WhenFileExists()
    {
        Directory.CreateDirectory(_directory);
        var target = Path.Combine(_directory, "model.onnx");
        await File.WriteAllTextAsync(target, "fake");
        var handler = new OllamaEmbeddingProviderTests.RecordingHandler(
            _ => throw new InvalidOperationException("HTTP should not be called"));
        var downloader = new Embeddings.ModelDownloader(new HttpClient(handler));

        await downloader.EnsureAsync(
            _directory,
            [new ModelFileSpec("model.onnx", "https://example.test/model.onnx")],
            null,
            CancellationToken.None);

        File.Exists(target).ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureAsync_DownloadsToTargetFile()
    {
        var handler = new OllamaEmbeddingProviderTests.RecordingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("payload-bytes"),
            });
        var downloader = new Embeddings.ModelDownloader(new HttpClient(handler));

        await downloader.EnsureAsync(
            _directory,
            [new ModelFileSpec("vocab.txt", "https://example.test/vocab.txt")],
            null,
            CancellationToken.None);

        var target = Path.Combine(_directory, "vocab.txt");
        File.Exists(target).ShouldBeTrue();
        (await File.ReadAllTextAsync(target)).ShouldBe("payload-bytes");
        handler.Uri.ShouldBe(new Uri("https://example.test/vocab.txt"));
    }

    [Fact]
    public async Task EnsureAsync_DeletesPartialFile_WhenDownloadFails()
    {
        var handler = new OllamaEmbeddingProviderTests.RecordingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var downloader = new Embeddings.ModelDownloader(new HttpClient(handler));

        await Should.ThrowAsync<HttpRequestException>(() => downloader.EnsureAsync(
            _directory,
            [new ModelFileSpec("model.onnx", "https://example.test/model.onnx")],
            null,
            CancellationToken.None));

        File.Exists(Path.Combine(_directory, "model.onnx")).ShouldBeFalse();
        File.Exists(Path.Combine(_directory, "model.onnx.tmp")).ShouldBeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
