namespace Overmind.Infrastructure.Embeddings;

public sealed record OnnxModelSpec(string Name, ModelFileSpec[] Files)
{
    public static readonly OnnxModelSpec AllMiniLmL6V2 = new(
        "all-MiniLM-L6-v2",
        [
            new ModelFileSpec(
                "model.onnx",
                "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx"),
            new ModelFileSpec(
                "vocab.txt",
                "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt"),
        ]);
}
