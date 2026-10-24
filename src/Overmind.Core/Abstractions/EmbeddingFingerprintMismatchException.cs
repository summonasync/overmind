namespace Overmind.Core;

public sealed class EmbeddingFingerprintMismatchException : Exception
{
    public string Expected { get; }

    public string? Found { get; }

    public EmbeddingFingerprintMismatchException(string expected, string? found)
        : base(BuildMessage(expected, found))
    {
        Expected = expected;
        Found = found;
    }

    private static string BuildMessage(string expected, string? found) =>
        found is null
            ? $"Embedding fingerprint mismatch: the collection carries no embedding fingerprint but {expected} is configured — model changed, reindex required."
            : $"Embedding fingerprint mismatch: expected {expected}, found {found} — model changed, reindex required.";
}
