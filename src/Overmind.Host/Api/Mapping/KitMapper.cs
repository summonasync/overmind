using Overmind.Core;
using Overmind.Host.Api.Contracts;

namespace Overmind.Host.Api.Mapping;

public static class KitMapper
{
    public static KitResponse ToResponse(SkillKit kit)
    {
        return new KitResponse(
            kit.Id,
            kit.Name,
            kit.Version,
            kit.Description,
            kit.SkillKitEntries.Select(e => e.EntryId).OrderBy(id => id).ToArray(),
            kit.SourceKind,
            kit.SourcePath,
            kit.ImportedAtUtc);
    }

    public static MaterializationReportResponse ToResponse(MaterializationReport report)
    {
        return new MaterializationReportResponse(
            report.ProjectId,
            report.KitId,
            report.Files.Select(f => new MaterializedFileResponse(f.Platform, f.RelativePath, f.Sha256)).ToArray());
    }

    public static DriftItemResponse ToResponse(DriftItem item)
    {
        return new DriftItemResponse(item.Platform, item.RelativePath, item.Kind);
    }
}
