namespace Overmind.Infrastructure.Planning;

public sealed record PlanningExportResult(Guid ProjectId, string TargetDirectory, int FileCount);
