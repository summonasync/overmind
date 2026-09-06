namespace Overmind.Host.Api.Contracts;

public sealed record CreateProjectRequest(string Name, string DiskPath, string? Description);

public sealed record UpdateProjectRequest(string Name, string DiskPath, string? Description);

public sealed record ProjectResponse(Guid Id, string Name, string DiskPath, string Description, DateTime CreatedAt);
