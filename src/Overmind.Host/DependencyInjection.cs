using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Validation;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Configuration;
using Overmind.Infrastructure.Embeddings;
using Overmind.Infrastructure.Grimoire;
using Overmind.Infrastructure.Kits;
using Overmind.Infrastructure.Llm;
using Overmind.Infrastructure.Manifest;
using Overmind.Infrastructure.Materialization;
using Overmind.Infrastructure.Notifications;
using Overmind.Infrastructure.Persistence;
using Overmind.Infrastructure.Planning;
using Overmind.Infrastructure.Search;
using Overmind.Infrastructure.VectorSearch;
using Overmind.Infrastructure.Wake;
using Overmind.Infrastructure.Wave;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Overmind.Host;

public static class DependencyInjection
{
    public static IServiceCollection AddOvermind(this IServiceCollection services)
    {
        services.AddBacklogImportPipeline();
        services.AddHostedService<WaveChatRetentionSweepService>();

        services.AddScoped<FtsSearchService>();

        services.AddScoped<EmbeddingProviderFactory>();
        services.AddSingleton<LlmChatClientCache>();
        services.AddScoped<ILlmChatClientFactory, LlmChatClientFactory>();
        services.AddSingleton<Overmind.Host.Loopback.LoopbackOrigin>();
        services.AddSingleton<EmbeddedAgentSessionState>();
        services.AddSingleton(sp => sp.GetRequiredService<Overmind.Host.Loopback.LoopbackOrigin>()
            .CreateClient(sp.GetRequiredService<EmbeddedAgentSessionState>()));
        services.AddSingleton<AgentLoopbackClient>();
        services.AddScoped<IAgentGuardNotifier, AgentLoopbackGuardNotifier>();
        services.AddScoped<EmbeddedAgentSessionRegistrar>();
        services.AddScoped<DelegatedApprovalGate>();
        services.AddScoped<IAgentDelegationGate>(provider => provider.GetRequiredService<DelegatedApprovalGate>());
        services.AddSingleton<AgentToolRegistry>();
        services.AddSingleton<IAgentToolInvoker>(provider => provider.GetRequiredService<AgentToolRegistry>());
        services.AddScoped<IAgentPromptComposer, AgentPromptComposer>();
        services.AddScoped<IAgentRunSettingsProvider, AgentRunSettingsProvider>();
        services.AddScoped<IAgentBudgetStore, AgentAppSettingsBudgetStore>();
        services.AddScoped<AgentLoop>();
        services.AddScoped<IConversationStreamSource, LlmConversationStreamSource>();
        services.AddHostedService<AgentLoopHostedService>();
        services.AddSingleton<VectorSearchHealthCache>();
        services.AddScoped<QdrantSemanticSearch>();
        services.AddScoped<ISemanticSearch>(provider => provider.GetRequiredService<QdrantSemanticSearch>());
        services.AddScoped<VectorSearchTester>();
        services.AddScoped<EntryIndexingService>();
        services.AddScoped<ReindexService>();

        services.AddSingleton(PlatformDirectoryMap.Load());
        services.AddScoped<IMaterializer, SkillMaterializer>();

        services.AddScoped<KitScanner>();
        services.AddScoped<KitImporter>();
        services.AddScoped<BundledKitForkService>();
        services.AddScoped<BundledSeedService>(provider => new BundledSeedService(
            provider.GetRequiredService<OvermindDbContext>(),
            provider.GetRequiredService<KitImporter>(),
            provider.GetRequiredService<AppSettingsService>(),
            provider.GetRequiredService<ILogger<BundledSeedService>>(),
            OvermindPaths.BundledDirectory));

        services.AddSingleton<IOvermindCliLocator, OvermindCliLocator>();
        services.AddSingleton<IGrimoireEngineRunner, GrimoireEngineRunner>();
        services.AddSingleton<IGrimoireBinaryLocator, GrimoireBinaryLocator>();
        services.AddSingleton<IGrimoireCli, GrimoireCli>();
        services.AddSingleton<IGrimoireHealthClient, GrimoireHealthClient>();
        services.AddSingleton<GrimoireConfigReader>();
        services.AddScoped<GrimoireDetectionService>();
        services.AddScoped<GrimoireIntegrationService>();

        services.AddScoped<TaskDependencyService>();
        services.AddScoped<PlanningArtifactService>();
        services.AddScoped<PlanningArtifactDiffService>();
        services.AddScoped<PlanningOperationStore>();
        services.AddScoped<NotificationsService>();
        services.AddScoped<CeremoniesService>();
        services.AddScoped<ManifestImporter>();
        services.AddSingleton<BrowserOrigin>();

        services.AddSingleton<IValidator<CreateProjectRequest>, CreateProjectRequestValidator>();
        services.AddSingleton<IValidator<UpdateProjectRequest>, UpdateProjectRequestValidator>();
        services.AddSingleton<IValidator<UpdateClaimSettingsRequest>, UpdateClaimSettingsRequestValidator>();
        services.AddSingleton<IValidator<CreateEntryRequest>, CreateEntryRequestValidator>();
        services.AddSingleton<IValidator<UpdateEntryRequest>, UpdateEntryRequestValidator>();
        services.AddSingleton<IValidator<EntryAckHumanRequest>, EntryAckHumanRequestValidator>();
        services.AddSingleton<IValidator<CreateKitRequest>, CreateKitRequestValidator>();
        services.AddSingleton<IValidator<UpdateKitRequest>, UpdateKitRequestValidator>();
        services.AddSingleton<IValidator<KitScanRequest>, KitScanRequestValidator>();
        services.AddSingleton<IValidator<KitImportRequest>, KitImportRequestValidator>();
        services.AddSingleton<IValidator<CreatePlanningFeatureRequest>, CreatePlanningFeatureRequestValidator>();
        services.AddSingleton<IValidator<UpdatePlanningFeatureRequest>, UpdatePlanningFeatureRequestValidator>();
        services.AddSingleton<IValidator<UpdatePlanningFeatureByAgentRequest>, UpdatePlanningFeatureByAgentRequestValidator>();
        services.AddSingleton<IValidator<CreatePlanningArtifactRequest>, CreatePlanningArtifactRequestValidator>();
        services.AddSingleton<IValidator<CreatePlanningTasksRequest>, CreatePlanningTasksRequestValidator>();
        services.AddSingleton<IValidator<UpdatePlanningArtifactRequest>, UpdatePlanningArtifactRequestValidator>();
        services.AddSingleton<IValidator<SetPlanningFeatureCodePinRequest>, SetPlanningFeatureCodePinRequestValidator>();
        services.AddSingleton<IValidator<SetPlanningFeatureStatusRequest>, SetPlanningFeatureStatusRequestValidator>();
        services.AddSingleton<IValidator<SetPlanningArtifactStatusRequest>, SetPlanningArtifactStatusRequestValidator>();
        services.AddSingleton<IValidator<AppendPlanningLogRequest>, AppendPlanningLogRequestValidator>();
        services.AddSingleton<IValidator<EditPlanningLogRequest>, EditPlanningLogRequestValidator>();
        services.AddSingleton<IValidator<DeclinePlanningArtifactRequest>, DeclinePlanningArtifactRequestValidator>();
        services.AddSingleton<IValidator<ImportPlanningFeatureRequest>, ImportPlanningFeatureRequestValidator>();
        services.AddSingleton<IValidator<PlanningImportFolderRequest>, PlanningImportFolderRequestValidator>();
        services.AddSingleton<IValidator<PlanningExportRequest>, PlanningExportRequestValidator>();
        services.AddSingleton<IValidator<AddPlanningFeatureDependencyRequest>, AddPlanningFeatureDependencyRequestValidator>();
        services.AddSingleton<IValidator<PlanningQueueMoveRequest>, PlanningQueueMoveRequestValidator>();
        services.AddSingleton<IValidator<PlanningQueueReorderRequest>, PlanningQueueReorderRequestValidator>();
        services.AddSingleton<IValidator<SetLanesRequest>, SetLanesRequestValidator>();
        services.AddSingleton<IValidator<UpdateLaneRequest>, UpdateLaneRequestValidator>();
        services.AddSingleton<IValidator<AppendPlanningDirectiveRequest>, AppendPlanningDirectiveRequestValidator>();
        services.AddSingleton<IValidator<AppendPlanningDirectiveHumanRequest>, AppendPlanningDirectiveHumanRequestValidator>();
        services.AddSingleton<IValidator<PostWaveMessageRequest>, PostWaveMessageRequestValidator>();
        services.AddSingleton<IValidator<PostWaveMessageHumanRequest>, PostWaveMessageHumanRequestValidator>();
        services.AddSingleton<IValidator<OnboardRequest>, OnboardRequestValidator>();
        services.AddSingleton<IValidator<UpdateTagRequest>, UpdateTagRequestValidator>();
        services.AddSingleton<IValidator<CreateNotificationRequest>, CreateNotificationRequestValidator>();
        services.AddSingleton<IValidator<ReplyNotificationRequest>, ReplyNotificationRequestValidator>();
        services.AddSingleton<IValidator<CreateConversationRequest>, CreateConversationRequestValidator>();
        services.AddSingleton<IValidator<RenameConversationRequest>, RenameConversationRequestValidator>();
        services.AddSingleton<IValidator<SendConversationMessageRequest>, SendConversationMessageRequestValidator>();

        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

        return services;
    }

    public static IServiceCollection AddBacklogImportPipeline(this IServiceCollection services)
    {
        services.AddDbContext<OvermindDbContext>((provider, options) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var databasePath = configuration["Overmind:DatabasePath"] ?? OvermindPaths.DatabasePath;
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            options.UseSqlite($"Data Source={databasePath};Pooling=false");
        });

        services.AddScoped<AppSettingsService>();
        services.AddScoped<PlanningPolicy>();
        services.AddScoped<IPlanningMaterializer>(provider => new PlanningMaterializer(
            provider.GetRequiredService<OvermindDbContext>(),
            provider.GetRequiredService<PlanningPolicy>(),
            OvermindPaths.DataDirectory));
        services.AddSingleton<PlanningMaterializationQueue>();
        services.AddSingleton<PlanningMaterializationWorker>();
        services.AddHostedService<PlanningMaterializationHostedService>();
        services.AddSingleton<ReplyAutoActQueue>();
        services.AddSingleton<ReplyAutoActWorker>();
        services.AddHostedService<ReplyAutoActHostedService>();
        services.AddScoped<ReplyAutoActFanout>();
        services.AddScoped<ReplyAutoActProcessor>();
        services.AddScoped<IReplyAutoActProcessor>(provider => provider.GetRequiredService<ReplyAutoActProcessor>());
        services.AddScoped<PlanningMaterializationDispatcher>();
        services.AddScoped<PlanningMaterializationStatusReader>(provider => new PlanningMaterializationStatusReader(
            provider.GetRequiredService<OvermindDbContext>(),
            provider.GetRequiredService<PlanningPolicy>(),
            provider.GetRequiredService<IPlanningMaterializer>(),
            OvermindPaths.DataDirectory));
        services.AddScoped<PlanningMaterializationLagReader>();
        services.AddScoped<PlanningFeatureService>();
        services.AddScoped<PlanningStatusBackfill>();
        services.AddScoped<FeatureClaimService>();
        services.AddScoped<LaneService>();
        services.AddScoped<DirectiveService>();
        services.AddScoped<EntryAckService>();
        services.AddScoped<IssueService>();
        services.AddScoped<TaskDependencyService>();
        services.AddScoped<FeatureDependencyService>();
        services.AddScoped<FeatureQueueService>();
        services.AddScoped<PlanningCurrentService>();
        services.AddScoped<WaveSnapshotService>();
        services.AddScoped<FrontmatterKeyRegistry>();
        services.AddScoped<WaveChatService>();
        services.AddScoped<WaveParticipantService>();
        services.AddScoped<AgentWakeService>();
        services.AddScoped<ConversationStore>();
        services.AddScoped<ConversationMessageStore>();
        services.AddSingleton<WakeDropWriter>();
        services.AddSingleton<WakeDropFanout>();
        services.AddScoped<WaveChatRetentionSweeper>();
        services.AddScoped<BacklogImportSeeder>();
        services.AddScoped<BacklogImporter>();
        services.AddScoped<BacklogFeatureScanner>();
        services.AddScoped<PlanningExportService>();

        return services;
    }
}
