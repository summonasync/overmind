using Overmind.Core;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Mapping;

public static class ProjectMapper
{
    public static ProjectResponse ToResponse(Project project, int openFeatureCount = 0) =>
        new(project.Id, project.Name, project.DiskPath, project.Description, project.Manifest,
            project.NumberStyle, project.NumberPrefix, project.IsFavorite, project.AutoActOnReplies,
            openFeatureCount, project.CreatedAt);
}
