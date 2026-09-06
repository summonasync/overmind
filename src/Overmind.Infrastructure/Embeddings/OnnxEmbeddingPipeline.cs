using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Overmind.Infrastructure.Embeddings;

public sealed class OnnxEmbeddingPipeline : IOnnxEmbeddingPipeline
{
    public const int MaxTokens = 512;

    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;

    private OnnxEmbeddingPipeline(InferenceSession session, BertTokenizer tokenizer)
    {
        _session = session;
        _tokenizer = tokenizer;
    }

    public static async Task<OnnxEmbeddingPipeline> CreateAsync(string modelPath, string vocabPath, CancellationToken ct)
    {
        var tokenizer = await BertTokenizer.CreateAsync(
            vocabPath,
            new BertOptions
            {
                LowerCaseBeforeTokenization = true,
                RemoveNonSpacingMarks = true,
            },
            ct);

        var session = new InferenceSession(modelPath);
        return new OnnxEmbeddingPipeline(session, tokenizer);
    }

    public int Dimensions => _session.OutputMetadata.Values.First().Dimensions[^1];

    public float[] Embed(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var ids = _tokenizer.EncodeToIds(text, addSpecialTokens: true, considerPreTokenization: true, considerNormalization: true);
        if (ids.Count > MaxTokens)
        {
            ids = ids.Take(MaxTokens).ToList();
        }

        var length = ids.Count;
        var inputIds = new long[length];
        var attentionMask = new long[length];
        var tokenTypeIds = new long[length];
        for (var i = 0; i < length; i++)
        {
            inputIds[i] = ids[i];
            attentionMask[i] = 1;
        }

        var dimensions = Dimensions;
        var inputShape = new[] { 1, length };
        using var results = _session.Run(new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(inputIds, inputShape)),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(attentionMask, inputShape)),
            NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(tokenTypeIds, inputShape)),
        });

        var hidden = results.First().AsTensor<float>();
        var vector = MeanPool(hidden, attentionMask, length, dimensions);
        Normalize(vector);
        return vector;
    }

    private static float[] MeanPool(Tensor<float> hidden, long[] attentionMask, int length, int dimensions)
    {
        var vector = new float[dimensions];
        var values = hidden.ToArray();
        var tokenCount = 0L;
        for (var t = 0; t < length; t++)
        {
            var mask = attentionMask[t];
            tokenCount += mask;
            var offset = t * dimensions;
            for (var d = 0; d < dimensions; d++)
            {
                vector[d] += values[offset + d] * mask;
            }
        }

        if (tokenCount == 0)
        {
            return vector;
        }

        for (var d = 0; d < dimensions; d++)
        {
            vector[d] /= tokenCount;
        }

        return vector;
    }

    private static void Normalize(float[] vector)
    {
        var norm = 0.0;
        foreach (var value in vector)
        {
            norm += value * value;
        }

        norm = Math.Sqrt(norm);
        if (norm <= 0)
        {
            return;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / norm);
        }
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
