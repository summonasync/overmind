namespace Overmind.Infrastructure.Entries;

// Spec R3 collision rule, owner ruling 2026-11-17: markers stay strict — exact spelling wins,
// a single case variant is adoptable, a tie is unresolvable. The deterministic fallback that used
// to live beside it died with the IX_Projects_NameNormalized unique index: the multi-variant world
// it arbitrated can no longer exist.
public static class ProjectNameMatch
{
    public static Guid? PickStrict(
        IEnumerable<(Guid Id, string Name, DateTime CreatedAt)> projects,
        string name)
    {
        var (exact, variants) = Collect(projects, name);

        return exact ?? (variants.Count == 1 ? variants[0].Id : null);
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
