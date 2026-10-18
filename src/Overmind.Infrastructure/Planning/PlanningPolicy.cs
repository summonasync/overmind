using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Planning;

public sealed class PlanningPolicy
{
    private readonly OvermindDbContext _db;
    private readonly AppSettingsService _settings;

    public PlanningPolicy(OvermindDbContext db, AppSettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<bool> ResolveValidationGateAsync(Guid projectId, CancellationToken ct)
    {
        var project = await LoadProjectAsync(projectId, ct);

        return project.RequireHumanValidation switch
        {
            HumanValidationOverride.On => true,
            HumanValidationOverride.Off => false,
            _ => string.Equals(
                await _settings.GetOrDefaultAsync(
                    SettingKeys.PlanningRequireHumanValidationDefault,
                    SettingDefaults.PlanningRequireHumanValidationDefault, ct),
                "on", StringComparison.OrdinalIgnoreCase),
        };
    }

    public async Task<PlanningMaterialization> ResolveMaterializationAsync(Guid projectId, CancellationToken ct)
    {
        var project = await LoadProjectAsync(projectId, ct);
        if (project.PlanningMaterialization != PlanningMaterialization.Inherit)
        {
            return project.PlanningMaterialization;
        }

        var globalDefault = await _settings.GetOrDefaultAsync(
            SettingKeys.PlanningMaterializationDefault,
            SettingDefaults.PlanningMaterializationDefault, ct);

        return Enum.TryParse<PlanningMaterialization>(globalDefault, ignoreCase: true, out var parsed) &&
               parsed != PlanningMaterialization.Inherit
            ? parsed
            : PlanningMaterialization.Off;
    }

    public async Task<string?> ResolveAgentNamingRulesAsync(Project project, CancellationToken ct)
    {
        if (project.AgentNamingRules is not null)
        {
            return project.AgentNamingRules;
        }

        var global = await _settings.GetAsync(SettingKeys.PlanningAgentNamingRulesDefault, ct);
        return string.IsNullOrWhiteSpace(global) ? null : global;
    }

    private async Task<Project> LoadProjectAsync(Guid projectId, CancellationToken ct)
    {
        return await _db.Projects.FindAsync([projectId], ct)
            ?? throw new InvalidOperationException($"Project '{projectId}' was not found.");
    }
}
