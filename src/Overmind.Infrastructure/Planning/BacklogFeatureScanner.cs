using Overmind.Core;
using Overmind.Infrastructure.Materialization;

namespace Overmind.Infrastructure.Planning;

public sealed class BacklogFeatureScanner(BacklogImporter importer)
{
    public async Task<BacklogFeatureScanReport> ScanAsync(
        Guid projectId,
        string projectRoot,
        string? subPath,
        IReadOnlyCollection<string> importedSlugs,
        CancellationToken ct = default)
    {
        var root = ResolveScanRoot(projectRoot, subPath);
        var imported = new HashSet<string>(importedSlugs, StringComparer.Ordinal);
        var entries = new List<BacklogFeatureScanEntry>();

        if (Directory.Exists(root))
        {
            if (File.Exists(Path.Combine(root, "feature.md")))
            {
                entries.Add(await ScanFolderAsync(projectId, projectRoot, root, Path.GetFileName(root), imported, ct));
            }
            else
            {
                foreach (var folder in Directory.EnumerateDirectories(root).OrderBy(p => p, StringComparer.Ordinal).ToArray())
                {
                    var folderName = Path.GetFileName(folder);
                    if (folderName == "_TEMPLATE")
                    {
                        continue;
                    }

                    entries.Add(await ScanFolderAsync(projectId, projectRoot, folder, folderName, imported, ct));
                }
            }
        }

        return new BacklogFeatureScanReport(root, entries);
    }

    private async Task<BacklogFeatureScanEntry> ScanFolderAsync(
        Guid projectId,
        string projectRoot,
        string folder,
        string folderName,
        IReadOnlySet<string> imported,
        CancellationToken ct)
    {
        try
        {
            var import = await importer.ParseFeatureAsync(projectId, projectRoot, folder, ct);
            return new BacklogFeatureScanEntry(
                folderName,
                import.Slug,
                import.Title,
                import.Status,
                import.Artifacts.Count(a => a.Kind == PlanningArtifactKind.Spec),
                import.Artifacts.Count(a => a.Kind == PlanningArtifactKind.Plan),
                import.Artifacts.Count(a => a.Kind == PlanningArtifactKind.Result),
                import.Artifacts.Count(a => a.Kind == PlanningArtifactKind.Task),
                imported.Contains(import.Slug),
                Error: null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new BacklogFeatureScanEntry(
                folderName, Slug: null, Title: null, Status: null,
                0, 0, 0, 0, AlreadyImported: false, ex.Message);
        }
    }

    private static string ResolveScanRoot(string projectRoot, string? subPath) =>
        string.IsNullOrWhiteSpace(subPath)
            ? BacklogImporter.ResolveBacklogDirectory(projectRoot) ?? Path.Combine(projectRoot, "BACKLOG")
            : PathGuard.ResolveUnderRoot(projectRoot, subPath);
}
