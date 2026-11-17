namespace Overmind.Infrastructure.Entries;

// Spec R3 collision rule, owner ruling 2026-11-17: markers are strict, the reserved container is deterministic.
// The two modes are separate methods on purpose — a caller cannot inherit the fallback by forgetting a flag.
public static class ProjectNameMatch
{
    public static Guid? PickStrict(
        IEnumerable<(Guid Id, string Name, DateTime CreatedAt)> projects,
        string name)
    {
        var (exact, variants) = Collect(projects, name);

        return exact ?? (variants.Count == 1 ? variants[0].Id : null);
    }

    public static Guid? PickDeterministic(
        IEnumerable<(Guid Id, string Name, DateTime CreatedAt)> projects,
        string name)
    {
        var (exact, variants) = Collect(projects, name);
        if (exact is not null)
        {
            return exact;
        }

        if (variants.Count == 0)
        {
            return null;
        }

        return variants
            .OrderBy(project => project.CreatedAt)
            .ThenBy(project => project.Id)
            .First()
            .Id;
    }

    private static (Guid? Exact, List<(Guid Id, string Name, DateTime CreatedAt)> Variants) Collect(
        IEnumerable<(Guid Id, string Name, DateTime CreatedAt)> projects,
        string name)
    {
        Guid? exact = null;
        var variants = new List<(Guid Id, string Name, DateTime CreatedAt)>();

        foreach (var project in projects)
        {
            if (!string.Equals(project.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (exact is null && string.Equals(project.Name, name, StringComparison.Ordinal))
            {
                exact = project.Id;
                continue;
            }

            variants.Add(project);
        }

        return (exact, variants);
    }
}
