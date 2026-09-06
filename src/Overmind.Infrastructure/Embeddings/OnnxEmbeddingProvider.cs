using Microsoft.Extensions.Logging;
using Overmind.Core;

namespace Overmind.Infrastructure.Embeddings;

public sealed class OnnxEmbeddingProvider : IEmbeddingProvider
{
    public const string ProviderName = "onnx";
    public const int OutputDimensions = 384;

    private readonly SemaphoreSlim _initGate = new(1, 1);
    private readonly Func<CancellationToken, Task<IOnnxEmbeddingPipeline>> _pipelineFactory;
    private IOnnxEmbeddingPipeline? _pipeline;

    public OnnxEmbeddingProvider(IOnnxEmbeddingPipeline pipeline)
    {
        _pipelineFactory = _ => Task.FromResult(pipeline);
        _pipeline = pipeline;
    }

    public OnnxEmbeddingProvider(
        string modelDirectory,
        OnnxModelSpec spec,
        ModelDownloader downloader,
        ILogger<OnnxEmbeddingProvider> logger,
        Action<string, long, long>? progress = null)
    {
        _pipelineFactory = ct => InitializeAsync(modelDirectory, spec, downloader, progress, logger, ct);
    }

    public string Name => ProviderName;

    public int Dimensions => OutputDimensions;

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var pipeline = await GetPipelineAsync(ct);
        return await Task.Run(() => pipeline.Embed(text), ct);
    }

    private async Task<IOnnxEmbeddingPipeline> GetPipelineAsync(CancellationToken ct)
    {
        if (_pipeline is not null)
        {
            return _pipeline;
        }

        await _initGate.WaitAsync(ct);
        try
        {
            return _pipeline ??= await _pipelineFactory(ct);
        }
        finally
        {
            _initGate.Release();
        }
    }

    private static async Task<IOnnxEmbeddingPipeline> InitializeAsync(
        string modelDirectory,
        OnnxModelSpec spec,
        ModelDownloader downloader,
        Action<string, long, long>? progress,
        ILogger<OnnxEmbeddingProvider> logger,
        CancellationToken ct)
    {
        await downloader.EnsureAsync(modelDirectory, spec.Files, progress, ct);
        logger.LogInformation("Loading ONNX embedding model from {ModelDirectory}", modelDirectory);

        var modelPath = Path.Combine(modelDirectory, spec.Files[0].FileName);
        var vocabPath = Path.Combine(modelDirectory, spec.Files[1].FileName);
        return await OnnxEmbeddingPipeline.CreateAsync(modelPath, vocabPath, ct);
    }
}
