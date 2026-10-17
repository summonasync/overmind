using Overmind.Core;

namespace Overmind.Host.Api.Contracts;

public sealed record SkillFileDto(AgentPlatform? Platform, string RelativePath, string Content);

public sealed record CreateEntryRequest(
    Guid ProjectId,
    EntryType Type,
    string Title,
    string Content,
    string[]? Tags,
    SkillFileDto[]? SkillFiles);

public sealed record UpdateEntryRequest(
    string Title,
    string Content,
    string[]? Tags,
    SkillFileDto[]? SkillFiles);

public sealed record EntryResponse(
    Guid Id,
    Guid ProjectId,
    EntryType Type,
    string Title,
    string Content,
    int CurrentVersion,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<string> Tags,
    IReadOnlyList<SkillFileDto> SkillFiles,
    IReadOnlyList<EntryReadAck> ReadBy);

public sealed record EntryReadAck(string? Alias, string? Vendor, DateTime At);

public sealed record EntryAckHumanRequest(string? Alias, string? Vendor);

public sealed record EntryAckResponse(Guid EntryId, bool Ack, string? Alias, string? Vendor, DateTime At);

public sealed record EntryVersionResponse(int Version, string Content, DateTime CreatedAt);
