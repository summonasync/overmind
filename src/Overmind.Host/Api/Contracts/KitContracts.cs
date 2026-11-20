using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

public sealed record CreateKitRequest(string Name, int Version, string? Description);

public sealed record UpdateKitRequest(string Name, int Version, string? Description);

public sealed record KitResponse(
    Guid Id,
    string Name,
    int Version,
    string Description,
    IReadOnlyList<Guid> EntryIds,
    KitSourceKind SourceKind,
    string? SourcePath,
    DateTime? ImportedAtUtc,
    bool Mandatory,
    bool Legacy);

public sealed record MaterializedFileResponse(AgentPlatform Platform, string RelativePath, string Sha256);

public sealed record MaterializationReportResponse(
    Guid ProjectId,
    Guid KitId,
    IReadOnlyList<MaterializedFileResponse> Files,
    IReadOnlyList<string> Warnings);

public sealed record DriftItemResponse(AgentPlatform Platform, string RelativePath, DriftKind Kind);
