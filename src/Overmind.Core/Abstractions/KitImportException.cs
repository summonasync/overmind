namespace Overmind.Core;

public sealed class KitImportException : Exception
{
    private KitImportException(KitImportFailureKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public KitImportFailureKind Kind { get; }

    public static KitImportException PathMissing(string path) =>
        new(KitImportFailureKind.PathMissing, $"Import path '{path}' does not exist.");

    public static KitImportException NotADirectory(string path) =>
        new(KitImportFailureKind.NotADirectory, $"Import path '{path}' is not a directory.");

    public static KitImportException DuplicateName(string name) =>
        new(KitImportFailureKind.DuplicateName, $"Kit name '{name}' is already taken.");

    public static KitImportException ReImportRequired(string name, Guid kitId) =>
        new(KitImportFailureKind.ReImportRequired,
            $"Candidate '{name}' matches imported kit '{kitId}'; use re-import to update it.");

    public static KitImportException CandidateNotFound(string relativePath) =>
        new(KitImportFailureKind.CandidateNotFound, $"Scanned candidate at '{relativePath}' was not found.");

    public static KitImportException KitNotFound(Guid kitId) =>
        new(KitImportFailureKind.KitNotFound, $"Kit '{kitId}' was not found.");

    public static KitImportException NoSource(Guid kitId) =>
        new(KitImportFailureKind.NoSource, $"Kit '{kitId}' has no import source; only imported kits can be re-imported.");

    public static KitImportException SourceMissing(string path) =>
        new(KitImportFailureKind.SourceMissing, $"Import source path '{path}' does not exist.");
}
