using System.Security.Cryptography;
using System.Text;
using Overmind.Core;
using QdrantValue = Qdrant.Client.Grpc.Value;

namespace Overmind.Infrastructure.VectorSearch;

public sealed record EmbeddingFingerprint(string Provider, string Model, int Dimensions, string Value)
{
    public static readonly Guid MarkerPointId = Guid.Parse("6d3a152c-32cf-49a4-8bfc-9295bc4dbf2c");

    public const string ProviderField = "__overmind_embedding_provider";

    public const string ModelField = "__overmind_embedding_model";

    public const string DimensionsField = "__overmind_embedding_dimensions";

    public const string MarkerField = "__overmind_embedding_fingerprint";

    public string Display => $"'{Model}' ({Dimensions} dims, fingerprint {Value})";

    public static EmbeddingFingerprint From(string provider, string model, int dimensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimensions);

        return new EmbeddingFingerprint(provider, model, dimensions, Hash(provider, model, dimensions));
    }

    public Dictionary<string, QdrantValue> ToPayload() => new()
    {
        [ProviderField] = Provider,
        [ModelField] = Model,
        [DimensionsField] = Dimensions,
        [MarkerField] = Value,
    };

    public static EmbeddingFingerprint? FromPayload(IReadOnlyDictionary<string, QdrantValue> payload)
    {
        if (!payload.TryGetValue(ProviderField, out var provider) || provider.KindCase != QdrantValue.KindOneofCase.StringValue ||
            !payload.TryGetValue(ModelField, out var model) || model.KindCase != QdrantValue.KindOneofCase.StringValue ||
            !payload.TryGetValue(DimensionsField, out var dims) || dims.KindCase != QdrantValue.KindOneofCase.IntegerValue ||
            !payload.TryGetValue(MarkerField, out var value) || value.KindCase != QdrantValue.KindOneofCase.StringValue)
        {
            return null;
        }

        return new EmbeddingFingerprint(
            provider.StringValue, model.StringValue, (int)dims.IntegerValue, value.StringValue);
    }

    private static string Hash(string provider, string model, int dimensions)
    {
        var data = Encoding.UTF8.GetBytes(string.Join('|', provider, model, dimensions));
        return Convert.ToHexString(SHA256.HashData(data), 0, 8).ToLowerInvariant();
    }
}
