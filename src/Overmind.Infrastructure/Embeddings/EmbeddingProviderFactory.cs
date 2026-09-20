using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Http;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Embeddings;

public sealed class EmbeddingProviderFactory
{
    public static readonly TimeSpan EmbeddingTimeout = TimeSpan.FromSeconds(30);

    private readonly OvermindDbContext _dbContext;
    private readonly ILoggerFactory _loggerFactory;

    public EmbeddingProviderFactory(OvermindDbContext dbContext, ILoggerFactory loggerFactory)
    {
        _dbContext = dbContext;
        _loggerFactory = loggerFactory;
    }

    public Task<IEmbeddingProvider> ResolveAsync(CancellationToken ct) =>
        CreateAsync(null, null, null, null, ct);

    public async Task<IEmbeddingProvider> CreateAsync(
        string? providerName,
        string? ollamaBaseUrl,
        string? ollamaModel,
        string? openAiBaseUrl,
        CancellationToken ct)
    {
        var settings = new AppSettingsService(_dbContext);
        providerName ??= await settings.GetOrDefaultAsync(
            SettingKeys.EmbeddingProvider, SettingDefaults.EmbeddingProvider, ct);

        switch (providerName.ToLowerInvariant())
        {
            case OllamaEmbeddingProvider.ProviderName:
                ollamaBaseUrl ??= await settings.GetOrDefaultAsync(
                    SettingKeys.EmbeddingOllamaBaseUrl, SettingDefaults.EmbeddingOllamaBaseUrl, ct);
                ollamaModel ??= await settings.GetOrDefaultAsync(
                    SettingKeys.EmbeddingOllamaModel, SettingDefaults.EmbeddingOllamaModel, ct);
                return new OllamaEmbeddingProvider(
                    CreateEmbeddingHttpClient(ollamaBaseUrl), ollamaBaseUrl, ollamaModel);

            case OpenAiCompatibleEmbeddingProvider.ProviderName:
                openAiBaseUrl ??= await settings.GetOrDefaultAsync(
                    SettingKeys.EmbeddingOpenAiBaseUrl, SettingDefaults.EmbeddingOpenAiBaseUrl, ct);
                var apiKey = await settings.GetOrDefaultAsync(SettingKeys.EmbeddingOpenAiApiKey, string.Empty, ct);
                return new OpenAiCompatibleEmbeddingProvider(
                    CreateEmbeddingHttpClient(openAiBaseUrl), openAiBaseUrl, apiKey);

            default:
                return CreateOnnxProvider();
        }
    }

    internal static HttpMessageHandler CreateEmbeddingHandler(string baseUrl) =>
        LoopbackHttp.CreateHandler(baseUrl);

    private IEmbeddingProvider CreateOnnxProvider()
    {
        var modelDirectory = OvermindModelPaths.ModelDirectory(OnnxModelSpec.AllMiniLmL6V2.Name);
        var downloader = new ModelDownloader(
            new HttpClient { Timeout = EmbeddingTimeout },
            _loggerFactory.CreateLogger<ModelDownloader>());
        var logger = _loggerFactory.CreateLogger<OnnxEmbeddingProvider>();

        return new OnnxEmbeddingProvider(
            modelDirectory,
            OnnxModelSpec.AllMiniLmL6V2,
            downloader,
            logger,
            (file, read, total) => logger.LogInformation(
                "Downloading {File}: {ReadBytes}/{TotalBytes} bytes", file, read, total));
    }

    private static HttpClient CreateEmbeddingHttpClient(string baseUrl) =>
        new(CreateEmbeddingHandler(baseUrl)) { Timeout = EmbeddingTimeout };
}
