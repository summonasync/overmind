using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record TaskDependencyImport(
    PlanningArtifact Task,
    string SourcePath,
    IReadOnlyList<string> DependsOn);
