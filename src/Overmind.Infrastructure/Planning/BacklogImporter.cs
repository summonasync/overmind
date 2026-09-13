using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed class BacklogImporter
{
    private readonly PlanningFeatureService _features;

    public BacklogImporter(PlanningFeatureService features)
    {
        _features = features;
    }

    public static string? ResolveBacklogDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        var child = Path.Combine(full, "BACKLOG");
        if (Directory.Exists(child))
        {
            return child;
        }

        var name = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return Directory.Exists(full) && string.Equals(name, "BACKLOG", StringComparison.OrdinalIgnoreCase)
            ? full
            : null;
    }

    public async Task<BacklogImportReport> ImportAsync(BacklogImportRequest request, CancellationToken ct = default)
    {
        var backlogDir = ResolveBacklogDirectory(request.BacklogPath)
            ?? throw new ArgumentException(
                $"'{request.BacklogPath}' is neither a repository root containing a BACKLOG folder nor a BACKLOG folder itself.");

        var repoRoot = Path.GetFullPath(Path.GetDirectoryName(backlogDir)!);
        var skipped = new List<string>();
        var featuresCreated = 0;
        var featuresUpdated = 0;
        var artifactsCreated = 0;
        var artifactsUpdated = 0;

        foreach (var folder in Directory.EnumerateDirectories(backlogDir).OrderBy(p => p, StringComparer.Ordinal))
        {
            var folderName = Path.GetFileName(folder);
            var relative = Rel(repoRoot, folder);
            if (folderName == "_TEMPLATE")
            {
                skipped.Add($"skipped: {relative} (_TEMPLATE)");
                continue;
            }

            if (!File.Exists(Path.Combine(folder, "feature.md")))
            {
                skipped.Add($"skipped: {relative} (no feature.md)");
                continue;
            }

            try
            {
                var import = await ParseFeatureFolderAsync(folder, folderName, repoRoot, skipped, ct);
                if (import is null)
                {
                    continue;
                }

                var outcome = await _features.ImportAsync(request.ProjectId, import, ct);
                if (outcome.Created)
                {
                    featuresCreated++;
                }
                else
                {
                    featuresUpdated++;
                }

                artifactsCreated += outcome.ArtifactsCreated;
                artifactsUpdated += outcome.ArtifactsUpdated;
                skipped.AddRange(outcome.Kept);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw PlanningException.ImportFailed(relative, ex);
            }
        }

        foreach (var file in Directory.EnumerateFiles(backlogDir).OrderBy(p => p, StringComparer.Ordinal))
        {
            skipped.Add($"ignored: {Rel(repoRoot, file)}");
        }

        return new BacklogImportReport(featuresCreated, featuresUpdated, artifactsCreated, artifactsUpdated, skipped);
    }

    private static async Task<BacklogFeatureImport?> ParseFeatureFolderAsync(
        string folder, string folderName, string repoRoot, List<string> skipped, CancellationToken ct)
    {
        var featurePath = Path.Combine(folder, "feature.md");
        var featureContent = await TryReadAsync(featurePath, Rel(repoRoot, featurePath), skipped, ct);
        if (featureContent is null)
        {
            return null;
        }

        var artifacts = new List<BacklogArtifactImport>();

        foreach (var name in new[] { "spec.md", "plan.md" })
        {
            var path = Path.Combine(folder, name);
            if (!File.Exists(path))
            {
                continue;
            }

            var kind = name == "spec.md" ? PlanningArtifactKind.Spec : PlanningArtifactKind.Plan;
            var artifact = await TryParseSpecOrPlanAsync(path, kind, repoRoot, skipped, ct);
            if (artifact is not null)
            {
                artifacts.Add(artifact);
            }
        }

        var tasksPath = Path.Combine(folder, "tasks");
        if (Directory.Exists(tasksPath))
        {
            foreach (var file in Directory.EnumerateFiles(tasksPath).OrderBy(p => p, StringComparer.Ordinal))
            {
                if (!BacklogNaming.TryParseTaskOrder(Path.GetFileName(file), out var order))
                {
                    skipped.Add($"skipped: {Rel(repoRoot, file)} (task filename has no NN- order prefix)");
                    continue;
                }

                var task = await TryParseTaskAsync(file, order, repoRoot, skipped, ct);
                if (task is not null)
                {
                    artifacts.Add(task);
                }
            }
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
        {
            var entryName = Path.GetFileName(entry);
            if (entryName is not ("feature.md" or "spec.md" or "plan.md" or "tasks"))
            {
                skipped.Add($"ignored: {Rel(repoRoot, entry)}");
            }
        }

        var (fields, featureBody) = BacklogMarkdown.Parse(featureContent);
        var title = ResolveTitle(BacklogMarkdown.FirstHeading(featureBody), folderName);

        return new BacklogFeatureImport(
            folderName,
            title,
            featureBody,
            MapFeatureStatus(Token(fields, "status"), Rel(repoRoot, featurePath), skipped),
            int.TryParse(Token(fields, "version"), out var version) && version > 0 ? version : 1,
            ResolveCreatedAt(fields),
            artifacts);
    }

    private static async Task<BacklogArtifactImport?> TryParseSpecOrPlanAsync(
        string path, PlanningArtifactKind kind, string repoRoot, List<string> skipped, CancellationToken ct)
    {
        var relative = Rel(repoRoot, path);
        var content = await TryReadAsync(path, relative, skipped, ct);
        if (content is null)
        {
            return null;
        }

        var (fields, body) = BacklogMarkdown.Parse(content);
        if (string.IsNullOrWhiteSpace(body))
        {
            skipped.Add($"skipped: {relative} (empty body)");
            return null;
        }

        var status = MapSpecOrPlanStatus(Token(fields, "status"), relative, skipped);
        var frozenAt = status == PlanningArtifactStatus.Validated
            ? BacklogMarkdown.ParseDate(Raw(fields, "validated")) ?? BacklogMarkdown.ParseDate(Raw(fields, "created"))
            : null;

        return new BacklogArtifactImport(
            kind,
            0,
            status,
            PlanningReviewLevel.Light,
            body,
            frozenAt?.ToDateTime(TimeOnly.MinValue),
            ResolveCreatedAt(fields),
            relative);
    }

    private static async Task<BacklogArtifactImport?> TryParseTaskAsync(
        string path, int order, string repoRoot, List<string> skipped, CancellationToken ct)
    {
        var relative = Rel(repoRoot, path);
        var content = await TryReadAsync(path, relative, skipped, ct);
        if (content is null)
        {
            return null;
        }

        var (fields, body) = BacklogMarkdown.Parse(content);
        if (string.IsNullOrWhiteSpace(body))
        {
            skipped.Add($"skipped: {relative} (empty body)");
            return null;
        }

        return new BacklogArtifactImport(
            PlanningArtifactKind.Task,
            order,
            MapTaskStatus(Token(fields, "status"), relative, skipped),
            MapReview(Token(fields, "review"), relative, skipped),
            body,
            null,
            ResolveCreatedAt(fields),
            relative);
    }

    private static async Task<string?> TryReadAsync(
        string path, string relative, List<string> skipped, CancellationToken ct)
    {
        try
        {
            return await File.ReadAllTextAsync(path, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            skipped.Add($"skipped: {relative} (unreadable: {ex.Message})");
            return null;
        }
    }

    private static string ResolveTitle(string? heading, string folderName)
    {
        var (featurePart, _) = BacklogNaming.SplitSlug(folderName);
        var prefix = $"Feature {featurePart} — ";
        var title = heading is not null && heading.StartsWith(prefix, StringComparison.Ordinal)
            ? heading[prefix.Length..].Trim()
            : heading;
        return string.IsNullOrWhiteSpace(title) ? folderName : title;
    }

    private static PlanningFeatureStatus MapFeatureStatus(string? raw, string relative, List<string> skipped)
    {
        switch (raw?.ToLowerInvariant())
        {
            case "ready":
                return PlanningFeatureStatus.Ready;
            case "done":
                return PlanningFeatureStatus.Done;
            case "in_progress":
            case "inprogress":
                return PlanningFeatureStatus.InProgress;
            case "draft":
            case null:
                return PlanningFeatureStatus.Draft;
            default:
                skipped.Add($"unknown status '{raw}' in {relative}, defaulted to draft");
                return PlanningFeatureStatus.Draft;
        }
    }

    private static PlanningArtifactStatus MapSpecOrPlanStatus(string? raw, string relative, List<string> skipped)
    {
        switch (raw?.ToLowerInvariant())
        {
            case "validated":
                return PlanningArtifactStatus.Validated;
            case "draft":
            case null:
                return PlanningArtifactStatus.Draft;
            default:
                skipped.Add($"unknown status '{raw}' in {relative}, defaulted to draft");
                return PlanningArtifactStatus.Draft;
        }
    }

    private static PlanningArtifactStatus MapTaskStatus(string? raw, string relative, List<string> skipped)
    {
        switch (raw?.ToLowerInvariant())
        {
            case "in_progress":
            case "inprogress":
                return PlanningArtifactStatus.InProgress;
            case "review":
                return PlanningArtifactStatus.Review;
            case "done":
                return PlanningArtifactStatus.Done;
            case "blocked":
                return PlanningArtifactStatus.Blocked;
            case "todo":
            case null:
                return PlanningArtifactStatus.Todo;
            default:
                skipped.Add($"unknown status '{raw}' in {relative}, defaulted to todo");
                return PlanningArtifactStatus.Todo;
        }
    }

    private static PlanningReviewLevel MapReview(string? raw, string relative, List<string> skipped)
    {
        switch (raw?.ToLowerInvariant())
        {
            case "hard":
                return PlanningReviewLevel.Hard;
            case "light":
            case null:
                return PlanningReviewLevel.Light;
            default:
                skipped.Add($"unknown review level '{raw}' in {relative}, defaulted to light");
                return PlanningReviewLevel.Light;
        }
    }

    private static string? Token(IReadOnlyDictionary<string, string> fields, string key)
    {
        var value = Raw(fields, key);
        if (value is null)
        {
            return null;
        }

        var cut = value.IndexOfAny([' ', '\t']);
        return cut < 0 ? value : value[..cut];
    }

    private static string? Raw(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value : null;

    private static DateTime ResolveCreatedAt(IReadOnlyDictionary<string, string> fields) =>
        BacklogMarkdown.ParseDate(Raw(fields, "created"))?.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow;

    private static string Rel(string root, string path) => Path.GetRelativePath(root, path);
}
