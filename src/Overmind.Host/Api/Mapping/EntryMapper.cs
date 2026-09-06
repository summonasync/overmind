using Overmind.Core;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Mapping;

public static class EntryMapper
{
    public static EntryResponse ToResponse(ContextEntry entry)
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
            entry.EntryTags.Select(t => t.Tag.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            entry.SkillFiles
                .Select(f => new SkillFileDto(f.Platform, f.RelativePath, f.Content))
                .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
                .ToArray());
    }

    public static SkillFile ToEntity(SkillFileDto dto) =>
        SkillFile.Create(dto.Platform, dto.RelativePath, dto.Content);
}
