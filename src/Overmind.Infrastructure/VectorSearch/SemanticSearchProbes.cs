using Overmind.Core;
using Qdrant.Client;

namespace Overmind.Infrastructure.VectorSearch;

internal static class SemanticSearchProbes
{
    public static readonly TimeSpan QdrantProbeTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan EmbeddingProbeTimeout = TimeSpan.FromSeconds(5);

    public const string EmbeddingProbeText = "ping";

    public static async Task<QdrantHealthLeg> ProbeQdrantAsync(
        QdrantClient client, string endpoint, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(QdrantProbeTimeout);
            await client.HealthAsync(cts.Token);
            return new QdrantHealthLeg(true, endpoint, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return new QdrantHealthLeg(false, endpoint, DescribeFailure(ex, QdrantProbeTimeout));
        }
    }

    public static async Task<EmbeddingHealthLeg> ProbeEmbeddingAsync(
        IEmbeddingProvider provider, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(EmbeddingProbeTimeout);
            var vector = await provider.EmbedAsync(EmbeddingProbeText, cts.Token);
            return new EmbeddingHealthLeg(true, provider.Name, provider.Model, vector.Length, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            return new EmbeddingHealthLeg(
                false, provider.Name, provider.Model, null, DescribeFailure(ex, EmbeddingProbeTimeout));
        }
    }

    private static string DescribeFailure(Exception ex, TimeSpan timeout) =>
        ex is OperationCanceledException
            ? $"Timed out after {timeout.TotalSeconds:0} seconds."
            : ex.Message;
}
