using Overmind.Core;

namespace Overmind.Infrastructure.Planning;

public sealed record IssueBoardRow(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string Title,
    IssueStatus Status,
    Guid AuthorProjectId,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    Guid? PromotedToArtifactId,
    int AppendCount);
