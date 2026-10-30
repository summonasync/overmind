using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

public sealed record CreateProjectRequest(string Name, string DiskPath, string? Description);

public sealed record UpdateProjectRequest(
    string Name,
    string DiskPath,
    string? Description,
    string? Manifest = null,
    bool SetManifest = false,
    string? NumberStyle = null,
    string? NumberPrefix = null);

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string DiskPath,
    string Description,
    string? Manifest,
    ProjectNumberStyle NumberStyle,
    string? NumberPrefix,
    bool IsFavorite,
    bool AutoActOnReplies,
    int OpenFeatureCount,
    DateTime CreatedAt);

public sealed record FavoriteRequest(bool IsFavorite);

public sealed record AutoActRequest(bool Enabled);

public sealed record ProjectManifestImportResponse(string SourceFile, string Manifest);
