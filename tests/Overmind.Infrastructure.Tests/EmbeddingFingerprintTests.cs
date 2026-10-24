using Overmind.Infrastructure.VectorSearch;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;

namespace Overmind.Infrastructure.Tests;

public sealed class EmbeddingFingerprintTests
{
    [Fact]
    public void From_SameInputs_ProducesStableValue()
    {
        var first = EmbeddingFingerprint.From("ollama", "nomic-embed-text", 768);
        var second = EmbeddingFingerprint.From("ollama", "nomic-embed-text", 768);

        first.Value.ShouldBe(second.Value);
        first.ShouldBe(second);
    }

    [Fact]
    public void From_EveryInputAffectsValue()
    {
        var baseline = EmbeddingFingerprint.From("ollama", "nomic-embed-text", 768);

        EmbeddingFingerprint.From("onnx", "nomic-embed-text", 768).Value.ShouldNotBe(baseline.Value);
        EmbeddingFingerprint.From("ollama", "all-minilm", 768).Value.ShouldNotBe(baseline.Value);
        EmbeddingFingerprint.From("ollama", "nomic-embed-text", 384).Value.ShouldNotBe(baseline.Value);
    }

    [Fact]
    public void ToPayload_FromPayload_RoundTrips()
    {
        var fingerprint = EmbeddingFingerprint.From("ollama", "nomic-embed-text", 768);

        EmbeddingFingerprint.FromPayload(fingerprint.ToPayload()).ShouldBe(fingerprint);
    }

    [Fact]
    public void FromPayload_EmptyPayload_ReturnsNull()
    {
        EmbeddingFingerprint.FromPayload(new Dictionary<string, Value>()).ShouldBeNull();
    }

    [Fact]
    public void FromPayload_IncompletePayload_ReturnsNull()
    {
        var payload = new Dictionary<string, Value>
        {
            [EmbeddingFingerprint.ProviderField] = "ollama",
            [EmbeddingFingerprint.ModelField] = "nomic-embed-text",
            [EmbeddingFingerprint.DimensionsField] = 768,
        };

        EmbeddingFingerprint.FromPayload(payload).ShouldBeNull();
    }

    [Fact]
    public void Display_NamesModelDimensionsAndValue()
    {
        var fingerprint = EmbeddingFingerprint.From("ollama", "nomic-embed-text", 768);

        fingerprint.Display.ShouldContain("nomic-embed-text");
        fingerprint.Display.ShouldContain("768");
        fingerprint.Display.ShouldContain(fingerprint.Value);
    }

    [Fact]
    public void From_BlankOrNonPositiveInput_Throws()
    {
        Should.Throw<ArgumentException>(() => EmbeddingFingerprint.From(" ", "nomic-embed-text", 768));
        Should.Throw<ArgumentException>(() => EmbeddingFingerprint.From("ollama", "", 768));
        Should.Throw<ArgumentOutOfRangeException>(() => EmbeddingFingerprint.From("ollama", "nomic-embed-text", 0));
    }
}
