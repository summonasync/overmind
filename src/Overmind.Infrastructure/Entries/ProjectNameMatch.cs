namespace Overmind.Infrastructure.Entries;

public static class ProjectNameMatch
{
    public static Guid? Pick(
        IEnumerable<(Guid Id, string Name, DateTime CreatedAt)> projects,
        string name)
    {
        Guid? fallback = null;
        DateTime fallbackCreatedAt = DateTime.MaxValue;

        foreach (var project in projects)
        {
            if (!string.Equals(project.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(project.Name, name, StringComparison.Ordinal))
            {
                return project.Id;
            }

            if (fallback is null
                || project.CreatedAt < fallbackCreatedAt
                || (project.CreatedAt == fallbackCreatedAt
                    && project.Id.CompareTo(fallback.Value) < 0))
            {
                fallback = project.Id;
                fallbackCreatedAt = project.CreatedAt;
            }
        }

        return fallback;
    }
}
