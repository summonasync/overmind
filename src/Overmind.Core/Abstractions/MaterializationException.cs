namespace Overmind.Core;

public enum MaterializationFailureKind
{
    NotFound,
    PathTraversal,
    OverwriteBlocked,
    InvalidTarget,
    IoError,
    LocalEditsBlocked,
    AllSkipped
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

    public static MaterializationException OutsideRoot(string root, string absolutePath) =>
        new(
            MaterializationFailureKind.PathTraversal,
            $"Path '{absolutePath}' resolves outside the project directory '{root}'.",
            absolutePath);

    public static MaterializationException Traversal(string root, string relativePath) =>
        new(
            MaterializationFailureKind.PathTraversal,
            $"Relative path '{relativePath}' escapes the project directory '{root}'.",
            relativePath);

    public static MaterializationException NotAbsolute(string root, string path) =>
        new(
            MaterializationFailureKind.PathTraversal,
            $"Path '{path}' is not absolute; give a folder under the project root '{root}'.",
            path);

    public static MaterializationException Overwrite(string absolutePath) =>
        new(
            MaterializationFailureKind.OverwriteBlocked,
            $"Refusing to overwrite '{absolutePath}': the file exists on disk but is not tracked by Overmind.",
            absolutePath);

    public static MaterializationException LocalEdits(IReadOnlyList<string> paths) =>
        new(
            MaterializationFailureKind.LocalEditsBlocked,
            string.Join('\n',
                new[] { "Refusing to overwrite locally modified tracked files:" }
                    .Concat(paths.Select(path => $"- {path}"))
                    .Append("Transcribe the edits to the board (planning_upsert_artifact / planning_append_log) or re-run materialize with force.")),
            paths is [var only] ? only : null);

    public static MaterializationException AllSkipped(IReadOnlyList<PlanningMaterializationSkip> skips) =>
        new(
            MaterializationFailureKind.AllSkipped,
            string.Join('\n',
                new[] { "Every planned file was skipped; nothing was written:" }
                    .Concat(skips.Select(skip => $"- {skip.RelativePath}: {skip.Reason}"))
                    .Append("Transcribe the edits to the board (planning_upsert_artifact / planning_append_log) or re-run materialize with force.")),
            skips is [var onlySkip] ? onlySkip.RelativePath : null);

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
