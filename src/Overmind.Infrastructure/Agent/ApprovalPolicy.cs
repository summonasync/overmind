using Overmind.Core;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Persistence;

namespace Overmind.Infrastructure.Agent;

public sealed class ApprovalPolicy(OvermindDbContext db, AppSettingsService settings)
{
    public async Task<bool> ResolveDelegateApprovalsAsync(Guid projectId, CancellationToken ct)
    {
        var project = await db.Projects.FindAsync([projectId], ct)
            ?? throw new InvalidOperationException($"Project '{projectId}' was not found.");

        return project.DelegateApprovals switch
        {
            DelegateApprovalsOverride.On => true,
            DelegateApprovalsOverride.Off => false,
            _ => string.Equals(
                await settings.GetOrDefaultAsync(
                    SettingKeys.AgentDelegateApprovalsDefault,
                    SettingDefaults.AgentDelegateApprovalsDefault, ct),
                "on", StringComparison.OrdinalIgnoreCase),
        };
    }
}
