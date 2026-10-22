namespace Overmind.Core;

public static class PlanningTaskSections
{
    public static readonly IReadOnlyList<string> Declared =
    [
        "goal", "scope", "anti-scope", "constraints", "acceptance-criteria", "refs", "obstacles", "log"
    ];

    public static bool IsDeclared(string name) => Declared.Contains(name, StringComparer.Ordinal);

    public static string WithSkeleton(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        foreach (var section in Declared)
        {
            if (!PlanningSections.TryFind(section, body, out _))
            {
                body = PlanningSections.Insert(section, body, null);
            }
        }

        return body;
    }
}
