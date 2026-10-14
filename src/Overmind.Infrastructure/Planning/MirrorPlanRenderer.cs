using System.Text;
using Overmind.Core;
using Overmind.Infrastructure.Materialization;

namespace Overmind.Infrastructure.Planning;

internal static class MirrorPlanRenderer
{
    public static List<MirrorFilePlan> BuildPlans(
        string root, List<PlanningFeature> features, List<PlanningMaterializedFile> rows)
    {
        var slugById = features.ToDictionary(f => f.Id, f => f.Slug);
        var blockerOrders = BlockerOrdersByTaskId(features);
        var plans = new List<MirrorFilePlan>();

        foreach (var feature in features)
        {
            var (featurePart, slugPart) = BacklogNaming.SplitSlug(feature.Slug);
            var directory = $"BACKLOG/{feature.Slug}";

            plans.Add(MirrorFilePlan.Create(
                root, feature.Id, $"{directory}/feature.md",
                FeatureDocument(feature, featurePart, slugPart, slugById)));

            foreach (var artifact in feature.Artifacts.Where(a =>
                         a.Kind is PlanningArtifactKind.Spec or PlanningArtifactKind.Plan or PlanningArtifactKind.Result))
            {
                var name = artifact.Kind.ToString().ToLowerInvariant();
                plans.Add(MirrorFilePlan.Create(
                    root, feature.Id, $"{directory}/{name}.md",
                    ArtifactDocument(artifact, featurePart, slugPart, feature.Title)));
            }

            foreach (var task in feature.Artifacts
                         .Where(a => a.Kind == PlanningArtifactKind.Task)
                         .OrderBy(a => a.Order))
            {
                var relative = PinnedTaskPath(rows, feature.Id, directory, task.Order);
                if (relative is null)
                {
                    var title = FirstHeading(task.Body) ?? task.Brief;
                    var taskSlug = title is null
                        ? $"task-{task.Order:D2}"
                        : Slug.From(title, $"task-{task.Order:D2}");
                    relative = $"{directory}/tasks/{BacklogNaming.TaskFileName(task.Order, taskSlug)}";
                }

                blockerOrders.TryGetValue(task.Id, out var taskBlockers);
                plans.Add(MirrorFilePlan.Create(
                    root, feature.Id, relative,
                    TaskDocument(task, featurePart, slugPart, taskBlockers ?? [])));
            }
        }

        var duplicate = plans
            .GroupBy(p => p.AbsolutePath, PathGuard.PathComparer)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw MaterializationException.DuplicateTarget(duplicate.Key);
        }

        return plans;
    }

    public static List<MirrorFilePlan> BuildEntryPlans(
        string root, List<ContextEntry> entries, List<PlanningMaterializedFile> rows)
    {
        var plans = new List<MirrorFilePlan>(entries.Count);
        foreach (var entry in entries)
        {
            var id8 = entry.Id.ToString("N")[..8];
            var document = EntryDocument(entry);
            var relative =
                $"ENTRIES/{EntryMirrorNaming.Directory(entry.Type)}/{Slug.From(entry.Title, "entry")}-{id8}.md";

            // Slug rules changed over time, so a board-identical pass keeps the tracked filename;
            // legacy non-ASCII names converge to the fresh slug only once the entry content moves.
            if (TrackedEntryPath(rows, entry.Type, id8, ContentHash.Sha256(document)) is { } tracked)
            {
                relative = tracked;
            }

            plans.Add(MirrorFilePlan.Create(root, featureId: null, relative, document));
        }

        var duplicate = plans
            .GroupBy(p => p.AbsolutePath, PathGuard.PathComparer)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw MaterializationException.DuplicateTarget(duplicate.Key);
        }

        return plans;
    }

    private static string? PinnedTaskPath(
        List<PlanningMaterializedFile> rows, Guid featureId, string directory, int order)
    {
        var prefix = $"{directory}/tasks/{order:D2}-";
        var row = rows.FirstOrDefault(r =>
            r.FeatureId == featureId &&
            r.RelativePath.StartsWith(prefix, StringComparison.Ordinal) &&
            r.RelativePath.EndsWith(".md", StringComparison.Ordinal));

        return row?.RelativePath;
    }

    private static string? TrackedEntryPath(
        List<PlanningMaterializedFile> rows, EntryType type, string id8, string sha256)
    {
        var prefix = $"ENTRIES/{EntryMirrorNaming.Directory(type)}/";
        var suffix = $"-{id8}.md";
        return rows
            .FirstOrDefault(r =>
                r.RelativePath.StartsWith(prefix, StringComparison.Ordinal) &&
                r.RelativePath.EndsWith(suffix, StringComparison.Ordinal) &&
                string.Equals(r.Sha256, sha256, StringComparison.OrdinalIgnoreCase))
            ?.RelativePath;
    }

    // Board values render verbatim, so line breaks inside a title or tag would split the
    // frontmatter line; they flatten here instead of a write-time refusal because rows stored
    // before any validator would still reach this projection.
    private static string FrontmatterLine(string value) =>
        value.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');

    private static string EntryDocument(ContextEntry entry)
    {
        var builder = new StringBuilder();
        builder.Append("---\n");
        builder.Append($"id: {entry.Id}\n");
        builder.Append($"type: {EntryMirrorNaming.Wire(entry.Type)}\n");
        builder.Append($"title: {FrontmatterLine(entry.Title)}\n");
        var tags = entry.EntryTags
            .Select(t => t.Tag.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        if (tags.Count > 0)
        {
            builder.Append($"tags: {string.Join(", ", tags.Select(FrontmatterLine))}\n");
        }

        builder.Append($"createdAt: {entry.CreatedAt:yyyy-MM-dd'T'HH:mm:ss'Z'}\n");
        builder.Append($"updatedAt: {entry.UpdatedAt:yyyy-MM-dd'T'HH:mm:ss'Z'}\n");
        builder.Append("---\n\n");
        builder.Append(entry.Content);
        return builder.ToString();
    }

    private static string? FirstHeading(string body)
    {
        foreach (var line in body.Split('\n'))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                return line[2..];
            }
        }

        return null;
    }

    private static string FeatureDocument(
        PlanningFeature feature, string featurePart, string slugPart, IReadOnlyDictionary<Guid, string> slugById)
    {
        var builder = new StringBuilder();
        builder.Append("---\n");
        builder.Append($"feature: {featurePart}\n");
        builder.Append($"slug: {slugPart}\n");
        builder.Append($"status: {FeatureStatus(feature.Status)}\n");
        builder.Append($"priority: {PriorityText(feature.Priority)}\n");
        builder.Append($"version: {feature.Version}\n");
        builder.Append($"created: {feature.CreatedAt:yyyy-MM-dd}\n");

        var blockers = feature.BlockedBy
            .Where(link => slugById.ContainsKey(link.BlockedByFeatureId))
            .Select(link => slugById[link.BlockedByFeatureId])
            .OrderBy(slug => slug, StringComparer.Ordinal)
            .ToList();
        if (blockers.Count > 0)
        {
            builder.Append($"{FrontmatterKeyRegistry.DependenciesKey}: {string.Join(", ", blockers)}\n");
        }

        builder.Append("---\n\n");
        builder.Append($"# Feature {featurePart} — {feature.Title}\n\n");
        builder.Append(BacklogMarkdown.StripLeadingHeadings(PlanningSections.Normalize(feature.Body)));
        return builder.ToString();
    }

    private static string ArtifactDocument(
        PlanningArtifact artifact, string featurePart, string slugPart, string featureTitle)
    {
        var builder = new StringBuilder();
        builder.Append("---\n");
        builder.Append($"feature: {featurePart}\n");
        builder.Append($"slug: {slugPart}\n");
        builder.Append($"status: {artifact.Status.ToString().ToLowerInvariant()}\n");
        builder.Append($"created: {artifact.CreatedAt:yyyy-MM-dd}\n");
        if (artifact.FrozenAt.HasValue)
        {
            builder.Append($"validated: {artifact.FrozenAt:yyyy-MM-dd}\n");
        }

        AppendPreservedFrontmatter(builder, artifact.PreservedFrontmatter);
        builder.Append("---\n\n");
        builder.Append($"# {artifact.Kind} {featurePart} — {featureTitle}\n\n");
        builder.Append(BacklogMarkdown.StripLeadingHeadings(PlanningSections.Normalize(artifact.Body)));
        return builder.ToString();
    }

    private static Dictionary<Guid, IReadOnlyList<int>> BlockerOrdersByTaskId(List<PlanningFeature> features)
    {
        var orders = new Dictionary<Guid, IReadOnlyList<int>>();
        foreach (var feature in features)
        {
            var siblingOrders = feature.Artifacts
                .Where(a => a.Kind == PlanningArtifactKind.Task)
                .ToDictionary(a => a.Id, a => a.Order);

            foreach (var task in feature.Artifacts)
            {
                if (task.Kind != PlanningArtifactKind.Task)
                {
                    continue;
                }

                orders[task.Id] = task.BlockedBy
                    .Where(d => siblingOrders.ContainsKey(d.BlockingId))
                    .Select(d => siblingOrders[d.BlockingId])
                    .OrderBy(o => o)
                    .ToList();
            }
        }

        return orders;
    }

    private static string TaskDocument(
        PlanningArtifact task, string featurePart, string slugPart, IReadOnlyList<int> blockerOrders)
    {
        var builder = new StringBuilder();
        builder.Append("---\n");
        builder.Append($"feature: {featurePart}\n");
        builder.Append($"task: {task.Order:D2}\n");
        builder.Append($"slug: {slugPart}\n");
        builder.Append($"status: {task.Status.ToString().ToLowerInvariant()}\n");
        builder.Append($"review: {task.Review.ToString().ToLowerInvariant()}\n");
        builder.Append($"created: {task.CreatedAt:yyyy-MM-dd}\n");
        if (blockerOrders.Count > 0)
        {
            builder.Append(
                $"{FrontmatterKeyRegistry.DependenciesKey}: {string.Join(", ", blockerOrders.Select(o => $"{o:D2}"))}\n");
        }

        if (task.Brief is not null)
        {
            builder.Append($"brief: {task.Brief}\n");
        }

        AppendPreservedFrontmatter(builder, task.PreservedFrontmatter);
        builder.Append("---\n\n");
        builder.Append(BacklogMarkdown.CollapseLeadingHeadings(PlanningSections.Normalize(task.Body)));
        return builder.ToString();
    }

    private static void AppendPreservedFrontmatter(StringBuilder builder, string? preservedJson)
    {
        var preserved = PreservedFrontmatterJson.Deserialize(preservedJson);
        if (preserved is null)
        {
            return;
        }

        foreach (var key in preserved.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            builder.Append($"{key}: {preserved[key]}\n");
        }
    }

    private static string FeatureStatus(PlanningFeatureStatus status) => status switch
    {
        PlanningFeatureStatus.Draft => "draft",
        PlanningFeatureStatus.Ready => "ready",
        PlanningFeatureStatus.InProgress => "inProgress",
        PlanningFeatureStatus.Review => "review",
        PlanningFeatureStatus.Done => "done",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static string PriorityText(FeaturePriority priority) => priority switch
    {
        FeaturePriority.P0 => "p0",
        FeaturePriority.P1 => "p1",
        FeaturePriority.P2 => "p2",
        FeaturePriority.P3 => "p3",
        _ => throw new ArgumentOutOfRangeException(nameof(priority)),
    };
}
