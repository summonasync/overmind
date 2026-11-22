using Overmind.Infrastructure.Grimoire;

namespace Overmind.Host.Api.Contracts;

public sealed record GrimoireStatusResponse(
    GrimoireStatus Status,
    string? BinaryPath,
    string? Version,
    int? Port,
    string BookName,
    string OverridePath,
    bool Wired,
    string InstallChannel);

public sealed record GrimoireWireRequest(
    string? BookName,
    string? OverridePath);

public sealed record GrimoireAgentOutcomeResponse(
    string Agent,
    string Outcome,
    string? ConfigPath,
    string? Error);

public sealed record GrimoireWireResponse(
    bool Success,
    bool Wired,
    IReadOnlyList<GrimoireAgentOutcomeResponse> Agents,
    string? Error);

public sealed record GrimoireAssistedInstallRequest(
    string? Intent,
    string? Channel);

public sealed record GrimoireAssistedInstallResponse(
    bool Degraded,
    bool Success,
    GrimoireStatus? Status,
    string? BinaryPath,
    IReadOnlyList<string> Instructions,
    string? Error);

public sealed record GrimoireDisconnectResponse(
    bool Success,
    bool Wired,
    string? Error);
