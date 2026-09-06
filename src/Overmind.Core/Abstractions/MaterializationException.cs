namespace Overmind.Core;

public enum MaterializationFailureKind
{
    NotFound,
    PathTraversal,
    OverwriteBlocked,
    InvalidTarget,
    IoError
}

public sealed class MaterializationException : Exception
{
    private MaterializationException(
        MaterializationFailureKind kind,
        string message,
        string? offendingPath = null,
        Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
        OffendingPath = offendingPath;
    }

    public MaterializationFailureKind Kind { get; }

    public string? OffendingPath { get; }

    public static MaterializationException MissingProject(Guid projectId) =>
        new(MaterializationFailureKind.NotFound, $"Project '{projectId}' was not found.");

    public static MaterializationException MissingKit(Guid kitId) =>
        new(MaterializationFailureKind.NotFound, $"Kit '{kitId}' was not found.");

    public static MaterializationException Traversal(string root, string relativePath) =>
        new(
            MaterializationFailureKind.PathTraversal,
            $"Relative path '{relativePath}' escapes the project directory '{root}'.",
            relativePath);

    public static MaterializationException Overwrite(string absolutePath) =>
        new(
            MaterializationFailureKind.OverwriteBlocked,
            $"Refusing to overwrite '{absolutePath}': the file exists on disk but is not tracked by Overmind.",
            absolutePath);

    public static MaterializationException DuplicateTarget(string absolutePath) =>
        new(
            MaterializationFailureKind.InvalidTarget,
            $"The kit maps more than one file to '{absolutePath}'.",
            absolutePath);

    public static MaterializationException IoFailure(string path, Exception inner) =>
        new(
            MaterializationFailureKind.IoError,
            $"File operation failed for '{path}': {inner.Message}",
            path,
            inner);
}
