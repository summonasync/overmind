using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed class BacklogImporter
{
    private static readonly HashSet<string> StructuralKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "status",
        "review",
        "version",
        "created",
        "feature",
        "task",
        "slug",
        "validated",
        "brief"
    };

    private readonly PlanningFeatureService _features;
    private readonly FrontmatterKeyRegistry _keys;
    private readonly Dictionary<string, FrontmatterKeyRegistration> _keyCache = new(StringComparer.Ordinal);

    public BacklogImporter(PlanningFeatureService features, FrontmatterKeyRegistry keys)
    {
        _features = features;
        _keys = keys;
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
        var notes = new BacklogImportNotes();
        var featuresCreated = 0;
        var featuresUpdated = 0;
        var artifactsCreated = 0;
        var artifactsUpdated = 0;
        var trackedFiles = 0;
        PlanningMaterialization? strategy = null;

        foreach (var folder in Directory.EnumerateDirectories(backlogDir).OrderBy(p => p, StringComparer.Ordinal))
        {
            var folderName = Path.GetFileName(folder);
            var relative = Rel(repoRoot, folder);
            if (folderName == "_TEMPLATE")
            {
                notes.Skipped.Add($"skipped: {relative} (_TEMPLATE)");
                continue;
            }

            if (!File.Exists(Path.Combine(folder, "feature.md")))
            {
                notes.Skipped.Add($"skipped: {relative} (no feature.md)");
                continue;
            }

            try
            {
                var import = await ParseFeatureFolderAsync(folder, folderName, repoRoot, notes, ct);
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
                trackedFiles += outcome.TrackedFiles;
                strategy = outcome.Materialization;
                notes.Skipped.AddRange(outcome.Kept);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw PlanningException.ImportFailed(relative, ex);
            }
        }

        foreach (var file in Directory.EnumerateFiles(backlogDir).OrderBy(p => p, StringComparer.Ordinal))
        {
            notes.Skipped.Add($"ignored: {Rel(repoRoot, file)}");
        }

        return new BacklogImportReport(
            featuresCreated, featuresUpdated, artifactsCreated, artifactsUpdated,
            notes.Skipped, notes.NewKeys, notes.Preserved, notes.CreatedFallbacks, ResolveNextStep(strategy, trackedFiles));
    }

    private async Task<BacklogFeatureImport?> ParseFeatureFolderAsync(
        string folder, string folderName, string repoRoot, BacklogImportNotes notes, CancellationToken ct)
    {
        var featurePath = Path.Combine(folder, "feature.md");
        var featureContent = await TryReadAsync(featurePath, Rel(repoRoot, featurePath), notes, ct);
        if (featureContent is null)
        {
            return null;
        }

        var artifacts = new List<BacklogArtifactImport>();
        var sourcePaths = new List<string> { featurePath };

        foreach (var name in new[] { "spec.md", "plan.md", "result.md" })
        {
            var path = Path.Combine(folder, name);
            if (!File.Exists(path))
            {
                continue;
            }

            var kind = name switch
            {
                "spec.md" => PlanningArtifactKind.Spec,
                "plan.md" => PlanningArtifactKind.Plan,
                _ => PlanningArtifactKind.Result,
            };
            var artifact = await TryParseDocumentAsync(path, kind, repoRoot, notes, ct);
            if (artifact is not null)
            {
                artifacts.Add(artifact);
                sourcePaths.Add(path);
            }
        }

        var tasksPath = Path.Combine(folder, "tasks");
        if (Directory.Exists(tasksPath))
        {
            var taskFiles = new List<(string Path, int Order, char Suffix)>();
            foreach (var file in Directory.EnumerateFiles(tasksPath).OrderBy(p => p, StringComparer.Ordinal))
            {
                if (!BacklogNaming.TryParseTaskOrder(Path.GetFileName(file), out var order, out var suffix))
                {
                    notes.Skipped.Add($"skipped: {Rel(repoRoot, file)} (task filename has no NN- order prefix)");
                    continue;
                }

                taskFiles.Add((file, order, suffix));
            }

            var plainWinners = new Dictionary<int, string>();
            var dedupedTaskFiles = new List<(string Path, int Order, char Suffix)>();
            foreach (var (path, order, suffix) in taskFiles)
            {
                if (suffix == '\0')
                {
                    if (plainWinners.TryGetValue(order, out var winner))
                    {
                        notes.Skipped.Add(
                            $"skipped: {Rel(repoRoot, path)} (duplicate order {order:D2}; {winner} imported first)");
                        continue;
                    }

                    plainWinners.Add(order, Path.GetFileName(path));
                }

                dedupedTaskFiles.Add((path, order, suffix));
            }

            var usedOrders = dedupedTaskFiles.Where(t => t.Suffix == '\0').Select(t => t.Order).ToHashSet();
            foreach (var (path, order, suffix) in dedupedTaskFiles)
            {
                int? reassignedFrom = null;
                var assigned = order;
                if (suffix != '\0')
                {
                    if (usedOrders.Contains(order))
                    {
                        assigned = usedOrders.Max() + 1;
                        reassignedFrom = order;
                    }

                    usedOrders.Add(assigned);
                }

                var task = await TryParseTaskAsync(path, assigned, reassignedFrom, repoRoot, notes, ct);
                if (task is not null)
                {
                    artifacts.Add(task);
                    sourcePaths.Add(path);
                }
            }
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
        {
            var entryName = Path.GetFileName(entry);
            if (entryName is not ("feature.md" or "spec.md" or "plan.md" or "result.md" or "tasks"))
            {
                notes.Skipped.Add($"ignored: {Rel(repoRoot, entry)}");
            }
        }

        var (fields, featureBody) = BacklogMarkdown.Parse(featureContent);
        var featureRelative = Rel(repoRoot, featurePath);
        var title = ResolveTitle(BacklogMarkdown.FirstHeading(featureBody), folderName);
        await ExtractPreservedAsync(fields, featureRelative, notes, ct);

        return new BacklogFeatureImport(
            folderName,
            title,
            BacklogMarkdown.StripLeadingHeadings(featureBody),
            MapFeatureStatus(Token(fields, "status"), featureRelative, notes.Skipped),
            int.TryParse(Token(fields, "version"), out var version) && version > 0 ? version : 1,
            ResolveCreatedAt(fields, featurePath, featureRelative, notes),
            artifacts,
            sourcePaths);
    }

    private static string? ResolveNextStep(PlanningMaterialization? strategy, int trackedFiles)
    {
        if (strategy is null)
        {
            return null;
        }

        return strategy.Value switch
        {
            PlanningMaterialization.On when trackedFiles > 0 => $"{trackedFiles} files tracked, materialize is a no-op",
            PlanningMaterialization.On => null,
            var value => $"materialization is '{value.ToString().ToLowerInvariant()}'; " +
                "set planning materialization to 'on' to write imported features to the repo",
        };
    }

    private async Task<BacklogArtifactImport?> TryParseDocumentAsync(
        string path, PlanningArtifactKind kind, string repoRoot, BacklogImportNotes notes, CancellationToken ct)
    {
        var relative = Rel(repoRoot, path);
        var content = await TryReadAsync(path, relative, notes, ct);
        if (content is null)
        {
            return null;
        }

        var (fields, body) = BacklogMarkdown.Parse(content);
        body = BacklogMarkdown.StripLeadingHeadings(body);
        if (string.IsNullOrWhiteSpace(body))
        {
            notes.Skipped.Add($"skipped: {relative} (empty body)");
            return null;
        }

        var status = MapDocumentStatus(Token(fields, "status"), relative, notes.Skipped);
        var frozenAt = status == PlanningArtifactStatus.Validated
            ? BacklogMarkdown.ParseDate(Raw(fields, "validated")) ?? BacklogMarkdown.ParseDate(Raw(fields, "created"))
            : null;
        var preserved = await ExtractPreservedAsync(fields, relative, notes, ct);

        return new BacklogArtifactImport(
            kind,
            0,
            status,
            PlanningReviewLevel.Light,
            body,
            frozenAt?.ToDateTime(TimeOnly.MinValue),
            ResolveCreatedAt(fields, path, relative, notes),
            relative,
            Preserved: preserved);
    }

    private async Task<BacklogArtifactImport?> TryParseTaskAsync(
        string path, int order, int? reassignedFrom, string repoRoot, BacklogImportNotes notes, CancellationToken ct)
    {
        var relative = Rel(repoRoot, path);
        var content = await TryReadAsync(path, relative, notes, ct);
        if (content is null)
        {
            return null;
        }

        var (fields, body) = BacklogMarkdown.Parse(content);
        body = BacklogMarkdown.CollapseLeadingHeadings(body);
        if (string.IsNullOrWhiteSpace(body))
        {
            notes.Skipped.Add($"skipped: {relative} (empty body)");
            return null;
        }

        var preserved = await ExtractPreservedAsync(fields, relative, notes, ct);

        return new BacklogArtifactImport(
            PlanningArtifactKind.Task,
            order,
            MapTaskStatus(Token(fields, "status"), relative, notes.Skipped),
            MapReview(Token(fields, "review"), relative, notes.Skipped),
            body,
            null,
            ResolveCreatedAt(fields, path, relative, notes),
            relative,
            reassignedFrom,
            preserved,
            Raw(fields, "brief"));
    }

    private static async Task<string?> TryReadAsync(
        string path, string relative, BacklogImportNotes notes, CancellationToken ct)
    {
        try
        {
            return await File.ReadAllTextAsync(path, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            notes.Skipped.Add($"skipped: {relative} (unreadable: {ex.Message})");
            return null;
        }
    }

    private async Task<IReadOnlyDictionary<string, string>?> ExtractPreservedAsync(
        IReadOnlyDictionary<string, string> fields, string relative, BacklogImportNotes notes, CancellationToken ct)
    {
        Dictionary<string, string>? preserved = null;
        foreach (var (key, value) in fields)
        {
            if (StructuralKeys.Contains(key))
            {
                continue;
            }

            preserved ??= new Dictionary<string, string>(StringComparer.Ordinal);
            preserved[key] = value;

            var registration = await ResolveKeyAsync(key, ct);
            if (registration.IsNew)
            {
                notes.NewKey(key, registration.Color);
            }
        }

        if (preserved is not null)
        {
            notes.PreservedKeys(relative, preserved.Keys);
        }

        return preserved;
    }

    private async Task<FrontmatterKeyRegistration> ResolveKeyAsync(string name, CancellationToken ct)
    {
        var normalized = name.Trim().ToLowerInvariant();
        if (!_keyCache.TryGetValue(normalized, out var registration))
        {
            registration = await _keys.GetOrRegisterAsync(normalized, ct);
            _keyCache[normalized] = registration;
        }

        return registration;
    }

    private static string ResolveTitle(string? heading, string folderName)
    {
        var (featurePart, _) = BacklogNaming.SplitSlug(folderName);
        var title = BacklogNaming.StripTitlePrefix(heading, "Feature", featurePart);
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

    private static PlanningArtifactStatus MapDocumentStatus(string? raw, string relative, List<string> skipped)
    {
        switch (raw?.ToLowerInvariant())
        {
            case "validated":
            case "approved":
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

        var comment = value.IndexOf(" #", StringComparison.Ordinal);
        if (comment >= 0)
        {
            value = value[..comment];
        }

        var cut = value.IndexOfAny([' ', '\t']);
        return cut < 0 ? value : value[..cut];
    }

    private static string? Raw(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value : null;

    private static DateTime ResolveCreatedAt(
        IReadOnlyDictionary<string, string> fields, string path, string relative, BacklogImportNotes notes)
    {
        var created = BacklogMarkdown.ParseDate(Raw(fields, "created"));
        if (created.HasValue)
        {
            return created.Value.ToDateTime(TimeOnly.MinValue);
        }

        var modifiedAt = File.GetLastWriteTimeUtc(path);
        notes.CreatedFallback(relative, modifiedAt);
        return modifiedAt;
    }

    private static string Rel(string root, string path) => Path.GetRelativePath(root, path);
}
