using System.Text.Json.Serialization;
using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

public sealed record CreateIssueRequest(
    Guid AuthorProjectId, string Title, string Body, string Kind = Issue.DefaultKind);

public sealed record AppendIssueRequest(Guid AuthorProjectId, string Text);

public sealed record SetIssueStatusRequest(IssueStatus Status, PlanningLogSource Source);

public sealed record PromoteIssueRequest(string FeatureSlug, PlanningLogSource Source);

public sealed record CreateIssueResponse(
    Guid Id, IssueStatus Status, string Title, string Kind = Issue.DefaultKind);

public sealed record SetIssueStatusResponse(
    Guid Id, IssueStatus Status, PlanningLogSource Source, DateTime? ClosedAt);

public sealed record IssuePromoteResponse(
    Guid Id, IssueStatus Status, Guid ArtifactId, Guid FeatureId, int ArtifactVersion);

public sealed record IssueRow(
    Guid Id,
    string Title,
    IssueStatus Status,
    Guid AuthorProjectId,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    Guid? PromotedToArtifactId,
    int AppendCount,
    string Kind = Issue.DefaultKind);

public sealed record CrossProjectIssueRow(
    Guid Id,
    Guid ProjectId,
    string Title,
    IssueStatus Status,
    Guid AuthorProjectId,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    Guid? PromotedToArtifactId,
    int AppendCount,
    string Kind = Issue.DefaultKind);

public sealed record IssueAppendItem(
    Guid Id,
    Guid AuthorProjectId,
    string Text,
    DateTime CreatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<object>? Warnings = null);

public sealed record IssueCard(
    Guid Id,
    string Title,
    string Body,
    IssueStatus Status,
    Guid AuthorProjectId,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    Guid? PromotedToArtifactId,
    IReadOnlyList<IssueAppendItem> Appends,
    string Kind = Issue.DefaultKind);
