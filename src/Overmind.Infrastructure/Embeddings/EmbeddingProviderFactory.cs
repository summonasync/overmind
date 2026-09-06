using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Infrastructure.Configuration;
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

    public async Task<IEmbeddingProvider> ResolveAsync(CancellationToken ct)
    {
        var settings = new AppSettingsService(_dbContext);
        var providerName = (await settings.GetOrDefaultAsync(
            SettingKeys.EmbeddingProvider, SettingDefaults.EmbeddingProvider, ct)).ToLowerInvariant();

        switch (providerName)
        {
            case OllamaEmbeddingProvider.ProviderName:
                var ollamaBaseUrl = await settings.GetOrDefaultAsync(
                    SettingKeys.EmbeddingOllamaBaseUrl, SettingDefaults.EmbeddingOllamaBaseUrl, ct);
                return new OllamaEmbeddingProvider(CreateEmbeddingHttpClient(), ollamaBaseUrl);

            case OpenAiCompatibleEmbeddingProvider.ProviderName:
                var openAiBaseUrl = await settings.GetOrDefaultAsync(
                    SettingKeys.EmbeddingOpenAiBaseUrl, SettingDefaults.EmbeddingOpenAiBaseUrl, ct);
                var apiKey = await settings.GetOrDefaultAsync(SettingKeys.EmbeddingOpenAiApiKey, string.Empty, ct);
                return new OpenAiCompatibleEmbeddingProvider(CreateEmbeddingHttpClient(), openAiBaseUrl, apiKey);

            default:
                return CreateOnnxProvider();
        }
    }

    private IEmbeddingProvider CreateOnnxProvider()
    {
        var modelDirectory = OvermindModelPaths.ModelDirectory(OnnxModelSpec.AllMiniLmL6V2.Name);
        var downloader = new ModelDownloader(
            CreateEmbeddingHttpClient(),
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

    private static HttpClient CreateEmbeddingHttpClient() => new() { Timeout = EmbeddingTimeout };
}
