using Overmind.Core;
using Overmind.Infrastructure.Materialization;

namespace Overmind.Infrastructure.Planning;

public static class PlanningArchive
{
    public static string Root(string dataDirectory, Project project) =>
        Path.Combine(dataDirectory, "projects", Slug.From(project.Name, project.Id.ToString("N")), "backlog");
}
