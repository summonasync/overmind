using Microsoft.Extensions.DependencyInjection;
using Overmind.Mcp.Planning;

namespace Overmind.Mcp;

public static class McpProfileRegistration
{
    public static IMcpServerBuilder WithProfileSurface(this IMcpServerBuilder builder, McpProfile profile)
    {
        if (profile == McpProfile.Journal)
        {
            builder.Services.AddSingleton<JournalProjectResolver>();

            return builder.WithTools<JournalTools>();
        }

        builder.Services.AddSingleton<MutationGuard>();
        builder.Services.AddSingleton<IGitCli, GitCli>();
        builder.Services.AddSingleton<ProjectResolver>();
        builder.Services.AddSingleton<OvermindTools>();
        builder.Services.AddSingleton<OvermindBriefingTools>();
        builder.Services.AddSingleton<PlanningFeatureTools>();
        builder.Services.AddSingleton<PlanningArtifactTools>();
        builder.Services.AddSingleton<PlanningBatchTools>();
        builder.Services.AddSingleton<PlanningMaterializationTools>();
        builder.Services.AddSingleton<PlanningArtifactAliasResolver>();
        builder.Services.AddSingleton<FeatureCodeTools>();
        builder.Services.AddSingleton<RescueScanTools>();
        builder.Services.AddSingleton<WaveListingCache>();
        builder.Services.AddSingleton<WaveSnapshotTools>();
        builder.Services.AddSingleton<WaveChatTools>();
        builder.Services.AddSingleton<WaveRoleTools>();
        builder.Services.AddSingleton<SessionIdentityTools>();
        builder.Services.AddSingleton<LaneTools>();
        builder.Services.AddSingleton<DirectiveTools>();
        builder.Services.AddSingleton<EntryAckTools>();
        builder.Services.AddSingleton<IssuesTools>();
        builder.Services.AddSingleton<OvermindOnboardingTools>();
        builder.Services.AddSingleton<OvermindNotificationTools>();
        builder.Services.AddSingleton<AgentWakeTools>();
        builder.Services.AddSingleton<OvermindResources>();
        builder.Services.AddSingleton<OvermindPlanningResources>();
        builder.Services.AddSingleton<SessionBootstrapPrompt>();

        return builder
            .WithTools<OvermindTools>()
            .WithTools<OvermindBriefingTools>()
            .WithTools<PlanningFeatureTools>()
            .WithTools<PlanningArtifactTools>()
            .WithTools<PlanningBatchTools>()
            .WithTools<PlanningMaterializationTools>()
            .WithTools<FeatureCodeTools>()
            .WithTools<RescueScanTools>()
            .WithTools<WaveSnapshotTools>()
            .WithTools<WaveChatTools>()
            .WithTools<WaveRoleTools>()
            .WithTools<SessionIdentityTools>()
            .WithTools<LaneTools>()
            .WithTools<DirectiveTools>()
            .WithTools<EntryAckTools>()
            .WithTools<IssuesTools>()
            .WithTools<OvermindOnboardingTools>()
            .WithTools<OvermindNotificationTools>()
            .WithTools<AgentWakeTools>()
            .WithResources<OvermindResources>()
            .WithResources<OvermindPlanningResources>()
            .WithPrompts<SessionBootstrapPrompt>();
    }
}
