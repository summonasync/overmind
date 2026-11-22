using System.Text.Json;
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
            kit.ImportedAtUtc,
            kit.Mandatory,
            kit.Legacy);
    }

    public static MaterializationReportResponse ToResponse(MaterializationReport report)
    {
        return new MaterializationReportResponse(
            report.ProjectId,
            report.KitId,
            report.Files.Select(f => new MaterializedFileResponse(f.Platform, f.RelativePath, f.Sha256)).ToArray(),
            report.Warnings.ToArray());
    }

    public static DriftItemResponse ToResponse(DriftItem item)
    {
        return new DriftItemResponse(item.Platform, item.RelativePath, item.Kind);
    }

    public static GlobalDeployReportResponse ToResponse(GlobalDeployReport report)
    {
        return new GlobalDeployReportResponse(
            report.KitId,
            report.Agent,
            report.StartedAt,
            report.FinishedAt,
            report.Entries.Select(e => new GlobalDeployEntryResponse(
                Camel(e.Kind), report.Agent, e.Path, Camel(e.Outcome), e.Reason)).ToArray(),
            report.Warnings,
            report.Success);
    }

    public static GlobalKitStateItemResponse ToResponse(
        GlobalKitStateItem item, IReadOnlyDictionary<AgentPlatform, string> agentNames)
    {
        return new GlobalKitStateItemResponse(
            item.KitId,
            item.KitName,
            item.Mandatory,
            AgentName(item.Platform, agentNames),
            item.State,
            item.Paths,
            item.Drifts.Select(d => new GlobalDriftItemResponse(
                d.KitId, AgentName(d.Platform, agentNames), d.Path, Camel(d.Kind))).ToArray());
    }

    private static string AgentName(AgentPlatform platform, IReadOnlyDictionary<AgentPlatform, string> agentNames) =>
        agentNames.GetValueOrDefault(platform, Camel(platform));

    private static string Camel(Enum value) =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
