using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

public sealed record GlobalDeployEntryResponse(
    string Kind,
    string Agent,
    string Path,
    string Outcome,
    string? Reason);

public sealed record GlobalDeployReportResponse(
    Guid KitId,
    string Agent,
    DateTime StartedAt,
    DateTime FinishedAt,
    GlobalDeployEntryResponse[] Entries,
    string[] Warnings,
    bool Success);

public sealed record GlobalDriftItemResponse(
    Guid KitId,
    string Agent,
    string Path,
    string Kind);

public sealed record GlobalKitStateItemResponse(
    Guid KitId,
    string KitName,
    bool Mandatory,
    string Agent,
    string State,
    string[] Paths,
    GlobalDriftItemResponse[] Drifts);
