using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Infrastructure.Planning;

namespace Overmind.Host.Api.Mapping;

public static class EntryMapper
{
    public static EntryResponse ToResponse(ContextEntry entry) =>
        ToDetailResponse(entry, new EntryLinkView([], [], []));

    public static EntryResponse ToDetailResponse(ContextEntry entry, EntryLinkView links)
    {
        return new EntryResponse(
            entry.Id,
            entry.ProjectId,
            entry.Type,
            entry.Title,
            entry.Content,
            entry.CurrentVersion,
            entry.CreatedAt,
            entry.UpdatedAt,
            entry.EntryTags
                .Where(t => t.Tag is not null)
                .Select(t => t.Tag.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray(),
            entry.SkillFiles
                .Select(f => new SkillFileDto(f.Platform, f.RelativePath, f.Content))
                .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
                .ToArray(),
            entry.Acks
                .OrderBy(a => a.At)
                .ThenBy(a => a.Id)
                .Select(a => new EntryReadAck(a.Alias, a.Vendor, a.At))
                .ToArray(),
            [.. PointerMarkerGrammar.Parse(entry.Content)
                .Select(p => new EntryPointerResponse(p.Path, p.Anchor))],
            entry.State,
            entry.StateAt,
            entry.StateBy,
            entry.StateReason,
            entry.StateProof,
            entry.SupersededById,
            entry.Enforces,
            entry.AuthorAlias,
            links.Outgoing,
            links.Incoming,
            links.Unresolved);
    }

    public static SkillFile ToEntity(SkillFileDto dto) =>
        SkillFile.Create(dto.Platform, dto.RelativePath, dto.Content);
}
