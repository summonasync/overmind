using System.Text.Json.Serialization;
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
    bool HumanDigestEnabled,
    int OpenFeatureCount,
    DateTime CreatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ProjectRelocation? Relocation = null);

public sealed record ProjectRelocation(
    string PreviousDiskPath,
    int PinsRemapped,
    List<string> RemappedFeatureSlugs,
    List<string> CompanionSteps);

public sealed record FavoriteRequest(bool IsFavorite);

public sealed record AutoActRequest(bool Enabled);

public sealed record HumanDigestRequest(bool Enabled);

public sealed record ProjectManifestImportResponse(string SourceFile, string Manifest);
