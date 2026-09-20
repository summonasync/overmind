using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Embeddings;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class EmbeddingProviderFactoryTests : IDisposable
{
    private readonly TempDatabase _temp = new();

    [Fact]
    public void CreateEmbeddingHandler_LoopbackBaseUrl_DisablesProxy()
    {
        var handler = EmbeddingProviderFactory.CreateEmbeddingHandler("http://127.0.0.1:11434");

        var sockets = handler.ShouldBeOfType<SocketsHttpHandler>();
        sockets.UseProxy.ShouldBeFalse();
    }

    [Fact]
    public void CreateEmbeddingHandler_RemoteBaseUrl_KeepsSystemProxy()
    {
        var handler = EmbeddingProviderFactory.CreateEmbeddingHandler("https://api.openai.com");

        var defaultHandler = handler.ShouldBeOfType<HttpClientHandler>();
        defaultHandler.UseProxy.ShouldBeTrue();
    }

    [Fact]
    public async Task ResolveAsync_OllamaModelDefaults_WhenSettingMissing()
    {
        await using var context = _temp.CreateContext();
        await new AppSettingsService(context).SetAsync(SettingKeys.EmbeddingProvider, "ollama", CancellationToken.None);
        var factory = CreateFactory(context);

        var provider = await factory.ResolveAsync(CancellationToken.None);

        var ollama = provider.ShouldBeOfType<OllamaEmbeddingProvider>();
        ollama.Model.ShouldBe(OllamaEmbeddingProvider.DefaultModel);
    }

    [Fact]
    public async Task ResolveAsync_WithOllamaModelSetting_PassesModelToProvider()
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);
        await settings.SetAsync(SettingKeys.EmbeddingProvider, "ollama", CancellationToken.None);
        await settings.SetAsync(SettingKeys.EmbeddingOllamaModel, "nomic-embed-text", CancellationToken.None);
        var factory = CreateFactory(context);

        var provider = await factory.ResolveAsync(CancellationToken.None);

        var ollama = provider.ShouldBeOfType<OllamaEmbeddingProvider>();
        ollama.Model.ShouldBe("nomic-embed-text");
    }

    [Fact]
    public async Task ResolveAsync_WithoutSettings_ReturnsOnnxProvider()
    {
        await using var context = _temp.CreateContext();
        var factory = CreateFactory(context);

        var provider = await factory.ResolveAsync(CancellationToken.None);

        provider.ShouldBeOfType<OnnxEmbeddingProvider>();
        provider.Name.ShouldBe("onnx");
        provider.Dimensions.ShouldBe(OnnxEmbeddingProvider.OutputDimensions);
    }

    [Fact]
    public async Task ResolveAsync_WithOllamaSetting_ReturnsOllamaProvider()
    {
        await using var context = _temp.CreateContext();
        await new AppSettingsService(context).SetAsync(SettingKeys.EmbeddingProvider, "ollama", CancellationToken.None);
        var factory = CreateFactory(context);

        var provider = await factory.ResolveAsync(CancellationToken.None);

        provider.ShouldBeOfType<OllamaEmbeddingProvider>();
        provider.Dimensions.ShouldBe(OllamaEmbeddingProvider.DefaultDimensions);
    }

    [Fact]
    public async Task ResolveAsync_WithOpenAiSetting_ReturnsOpenAiProvider()
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);
        await settings.SetAsync(SettingKeys.EmbeddingProvider, "openai", CancellationToken.None);
        await settings.SetAsync(SettingKeys.EmbeddingOpenAiBaseUrl, "http://127.0.0.1:11434/v1", CancellationToken.None);
        await settings.SetAsync(SettingKeys.EmbeddingOpenAiApiKey, "secret-key", CancellationToken.None);
        var factory = CreateFactory(context);

        var provider = await factory.ResolveAsync(CancellationToken.None);

        provider.ShouldBeOfType<OpenAiCompatibleEmbeddingProvider>();
        provider.Dimensions.ShouldBe(OpenAiCompatibleEmbeddingProvider.DefaultDimensions);
    }

    [Fact]
    public async Task ResolveAsync_ProviderName_IsCaseInsensitive()
    {
        await using var context = _temp.CreateContext();
        await new AppSettingsService(context).SetAsync(SettingKeys.EmbeddingProvider, "OLLAMA", CancellationToken.None);
        var factory = CreateFactory(context);

        var provider = await factory.ResolveAsync(CancellationToken.None);

        provider.ShouldBeOfType<OllamaEmbeddingProvider>();
    }

    [Fact]
    public async Task ResolveAsync_SwitchesProvider_WhenSettingsChange()
    {
        await using var context = _temp.CreateContext();
        var settings = new AppSettingsService(context);
        var factory = CreateFactory(context);

        await settings.SetAsync(SettingKeys.EmbeddingProvider, "ollama", CancellationToken.None);
        (await factory.ResolveAsync(CancellationToken.None)).ShouldBeOfType<OllamaEmbeddingProvider>();

        await settings.SetAsync(SettingKeys.EmbeddingProvider, "openai", CancellationToken.None);
        (await factory.ResolveAsync(CancellationToken.None)).ShouldBeOfType<OpenAiCompatibleEmbeddingProvider>();
    }

    private static EmbeddingProviderFactory CreateFactory(Persistence.OvermindDbContext context) =>
        new(context, NullLoggerFactory.Instance);

    public void Dispose() => _temp.Dispose();
}
