using Overmind.Core;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Mapping;

public static class ProjectMapper
{
    public static ProjectResponse ToResponse(Project project) =>
        new(project.Id, project.Name, project.DiskPath, project.Description, project.Manifest, project.CreatedAt);
}
