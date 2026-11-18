using Microsoft.EntityFrameworkCore;
using Overmind.Core;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public static class ProjectPinRelocator
{
    private static readonly char[] Separators = ['/', '\\'];

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static async Task<IReadOnlyList<PinRelocation>> RelocateWorktreePinsAsync(
        OvermindDbContext db,
        Project project,
        string previousDiskPath,
        string newDiskPath,
        PlanningLogSource source,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(project);

        var features = await db.PlanningFeatures
            .Where(f => f.ProjectId == project.Id && f.PinnedWorktreePath != null)
            .ToListAsync(ct);

        var relocations = new List<PinRelocation>();
        foreach (var feature in features)
        {
            var previousPin = feature.PinnedWorktreePath!;
            if (!TryRemap(previousPin, previousDiskPath, newDiskPath, out var relocatedPin))
            {
                continue;
            }

            if (!feature.RemapCodePin(previousPin, relocatedPin, source))
            {
                continue;
            }

            relocations.Add(new PinRelocation(feature.Slug, previousPin, relocatedPin));
        }

        return relocations;
    }

    private static bool TryRemap(string pin, string oldRoot, string newRoot, out string relocated)
    {
        relocated = pin;

        var oldPrefix = oldRoot.TrimEnd(Separators);
        var newPrefix = newRoot.TrimEnd(Separators);
        if (oldPrefix.Length == 0 || newPrefix.Length == 0)
        {
            return false;
        }

        var probe = oldPrefix.Replace('\\', '/');
        var haystack = pin.Replace('\\', '/');
        if (!haystack.StartsWith(probe, PathComparison))
        {
            return false;
        }

        relocated = haystack.Length == probe.Length
            ? newPrefix
            : IsSeparator(haystack[probe.Length])
                ? newPrefix + pin[oldPrefix.Length..]
                : pin;

        return !PathsEqual(pin, relocated);
    }

    private static bool IsSeparator(char value) => value is '/' or '\\';

    private static bool PathsEqual(string left, string right) =>
        string.Equals(left.Replace('\\', '/'), right.Replace('\\', '/'), PathComparison);
}
