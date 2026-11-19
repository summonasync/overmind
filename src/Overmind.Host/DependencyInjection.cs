using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Overmind.Core;
using Overmind.Host.AgentChat;
using Overmind.Host.Api.Contracts;
using Overmind.Host.Api.Validation;
using Overmind.Infrastructure.Agent;
using Overmind.Infrastructure.Agent.Routes;
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
using Overmind.Infrastructure.Usage;
using Overmind.Infrastructure.Telegram;
using Overmind.Infrastructure.VectorSearch;
using Overmind.Infrastructure.Wake;
using Overmind.Infrastructure.Wave;
using Overmind.Host.WaveChat;
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
        services.AddScoped<ApprovalPolicy>();
        services.AddScoped<DelegatedApprovalGate>();
        services.AddScoped<IAgentDelegationGate>(provider => provider.GetRequiredService<DelegatedApprovalGate>());
        services.AddScoped<AgentToolRegistry>();
        services.AddScoped<IAgentToolInvoker>(provider => provider.GetRequiredService<AgentToolRegistry>());
        services.AddSingleton<IGrimoireMemoryClient, GrimoireMemoryClient>();
        services.AddScoped<GrimoireCompactionRunner>();
        services.AddHostedService<GrimoireCompactionSweepService>();
        services.AddHostedService<HumanInboxDigestSweepService>();
        services.AddScoped<IAgentPromptComposer, AgentPromptComposer>();
        services.AddScoped<IAgentRunSettingsProvider, AgentRunSettingsProvider>();
        services.AddScoped<IAgentBudgetStore, AgentAppSettingsBudgetStore>();
        services.AddScoped<AgentLoop>();
        services.AddKeyedScoped<IConversationStreamSource, LlmConversationStreamSource>(
            RouterConversationStreamSource.WorkerStreamSourceKey);
        services.AddScoped<IConversationStreamSource>(provider =>
            provider.GetRequiredService<RouterConversationStreamSource>());
        services.AddKeyedSingleton<EmbeddedAgentSessionState>(NotificationValidatorSessionRegistrar.Alias);
        services.AddScoped<NotificationValidatorSessionRegistrar>();
        services.AddScoped<NotificationValidatorPromptComposer>();
        services.AddScoped<NotificationValidatorToolInvoker>();
        services.AddKeyedScoped<AgentLoop>(NotificationValidatorSessionRegistrar.Alias, (provider, _) =>
        {
            var registrar = new NotificationValidatorSessionRegistrar(
                provider.GetRequiredService<AgentLoopbackClient>(),
                provider.GetRequiredKeyedService<EmbeddedAgentSessionState>(
                    NotificationValidatorSessionRegistrar.Alias),
                provider.GetRequiredService<IAgentRunSettingsProvider>());
            return ActivatorUtilities.CreateInstance<AgentLoop>(
                provider,
                provider.GetRequiredService<NotificationValidatorPromptComposer>(),
                provider.GetRequiredService<NotificationValidatorToolInvoker>(),
                registrar);
        });
        services.AddScoped<INotificationValidatorRunner, NotificationValidatorRunner>();
        services.AddScoped<TelegramValidatorConversationStore>();
        services.AddScoped<IRouterPromptComposer, RouterPromptComposer>();
        services.AddScoped<IntentRouter>();
        services.AddScoped<RouterChatResponder>();
        services.AddScoped<RouterConversationStreamSource>();
        services.AddSingleton<ConversationTurnRunner>();
        services.AddSingleton<ConversationTurnRegistry>();
        services.AddHostedService<AgentLoopHostedService>();
        services.AddScoped<TelegramTransportOptionsProvider>();
        services.AddSingleton<ITelegramBotClientFactory, TelegramBotClientFactory>();
        services.AddSingleton<TelegramBotClientCache>();
        services.AddSingleton<TelegramPollingRunner>();
        services.AddHostedService<TelegramPollingService>();
        services.AddSingleton<ConversationTurnGate>();
        services.AddSingleton(new TelegramWaveDraftStore(TimeProvider.System));
        services.AddSingleton(new TelegramWavePromptTombstone(TimeProvider.System));
        services.AddSingleton<WaveSseHub>();
        services.AddSingleton(WaveStreamOptions.Default);
        services.AddSingleton<IWaveChatBroadcaster>(provider => provider.GetRequiredService<WaveSseHub>());
        services.AddScoped<TelegramChatStore>();
        services.AddScoped<TelegramReplySender>();
        services.AddScoped<TelegramNotificationDeliveryStore>();
        services.AddScoped<ITelegramMessageHandler, TelegramAgentChatHandler>();
        services.AddScoped<ITelegramWaveCommandService, TelegramWaveCommandService>();
        services.AddScoped<ITelegramWaveSnapshotService, TelegramWaveSnapshotService>();
        services.AddScoped<TelegramNotificationCallbackHandler>();
        services.AddScoped<TelegramWaveCallbackHandler>();
        services.AddScoped<TelegramCallbackRouter>();
        services.AddScoped<ITelegramCallbackHandler>(
            provider => provider.GetRequiredService<TelegramCallbackRouter>());
        services.AddSingleton<INotificationDeliveryChannel>(provider => new TelegramNotificationFanout(
            provider.GetRequiredService<IServiceScopeFactory>(),
            () => provider.GetRequiredService<BrowserOrigin>().BoundOrigin,
            provider.GetRequiredService<ILogger<TelegramNotificationFanout>>()));
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
        services.AddScoped<KitActivationService>();
        services.AddScoped<LegacyKitMigrationService>();
        services.AddScoped<BundledKitForkService>();
        services.AddScoped<BundledSeedService>(provider => new BundledSeedService(
            provider.GetRequiredService<OvermindDbContext>(),
            provider.GetRequiredService<KitImporter>(),
            provider.GetRequiredService<AppSettingsService>(),
            provider.GetRequiredService<ILogger<BundledSeedService>>(),
            OvermindPaths.BundledDirectory,
            provider.GetRequiredService<LegacyKitMigrationService>(),
            // No IGlobalKitDeployer is registered yet: the mandatory kit's home-directory door is
            // feature 251, and the seed records the missing door in the log until then.
            provider.GetService<IGlobalKitDeployer>()));

        services.AddSingleton<IOvermindCliLocator, OvermindCliLocator>();
        services.AddSingleton<IGrimoireEngineRunner, GrimoireEngineRunner>();
        services.AddSingleton<IGrimoireBinaryLocator, GrimoireBinaryLocator>();
        services.AddSingleton<IGrimoireCli, GrimoireCli>();
        services.AddSingleton<IGrimoireHealthClient, GrimoireHealthClient>();
        services.AddSingleton<GrimoireConfigReader>();
        services.AddScoped<GrimoireDetectionService>();
        services.AddScoped<GrimoireIntegrationService>();

        services.AddScoped<TaskDependencyService>();
        services.AddScoped<PlanningBatchScope>();
        services.AddScoped<PlanningArtifactService>();
        services.AddScoped<PlanningArtifactDiffService>();
        services.AddScoped<PlanningOperationStore>();
        services.AddScoped<PlanningBatchService>();
        services.AddScoped<NotificationsService>();
        services.AddScoped<HumanInboxService>();
        services.AddScoped<HumanDigestSweeper>();
        services.AddScoped<UsagePricingCatalog>();
        services.AddScoped<UsageSummaryService>();
        services.AddScoped<CeremoniesService>();
        services.AddScoped<ManifestImporter>();
        services.AddSingleton<BrowserOrigin>();

        services.AddSingleton<IValidator<CreateProjectRequest>, CreateProjectRequestValidator>();
        services.AddSingleton<IValidator<UpdateProjectRequest>, UpdateProjectRequestValidator>();
        services.AddSingleton<IValidator<UpdateClaimSettingsRequest>, UpdateClaimSettingsRequestValidator>();
        services.AddSingleton<IValidator<CreateEntryRequest>, CreateEntryRequestValidator>();
        services.AddSingleton<IValidator<UpdateEntryRequest>, UpdateEntryRequestValidator>();
        services.AddSingleton<IValidator<EntryAckHumanRequest>, EntryAckHumanRequestValidator>();
        services.AddSingleton<IValidator<EntryLiftRequest>, EntryLiftRequestValidator>();
        services.AddSingleton<IValidator<EntryAppendRequest>, EntryAppendRequestValidator>();
        services.AddSingleton<IValidator<CreateKitRequest>, CreateKitRequestValidator>();
        services.AddSingleton<IValidator<UpdateKitRequest>, UpdateKitRequestValidator>();
        services.AddSingleton<IValidator<KitScanRequest>, KitScanRequestValidator>();
        services.AddSingleton<IValidator<KitImportRequest>, KitImportRequestValidator>();
        services.AddSingleton<IValidator<CreatePlanningFeatureRequest>, CreatePlanningFeatureRequestValidator>();
        services.AddSingleton<IValidator<UpdatePlanningFeatureRequest>, UpdatePlanningFeatureRequestValidator>();
        services.AddSingleton<IValidator<UpdatePlanningFeatureByAgentRequest>, UpdatePlanningFeatureByAgentRequestValidator>();
        services.AddSingleton<IValidator<PatchPlanningFeatureRequest>, PatchPlanningFeatureRequestValidator>();
        services.AddSingleton<IValidator<CreatePlanningArtifactRequest>, CreatePlanningArtifactRequestValidator>();
        services.AddSingleton<IValidator<CreatePlanningTasksRequest>, CreatePlanningTasksRequestValidator>();
        services.AddSingleton<IValidator<UpdatePlanningArtifactRequest>, UpdatePlanningArtifactRequestValidator>();
        services.AddSingleton<IValidator<SetPlanningFeatureCodePinRequest>, SetPlanningFeatureCodePinRequestValidator>();
        services.AddSingleton<IValidator<SetPlanningFeatureStatusRequest>, SetPlanningFeatureStatusRequestValidator>();
        services.AddSingleton<IValidator<SetPlanningArtifactStatusRequest>, SetPlanningArtifactStatusRequestValidator>();
        services.AddSingleton<IValidator<PlanningBatchRequest>, PlanningBatchRequestValidator>();
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
        services.AddSingleton<IValidator<CreateLaneRequest>, CreateLaneRequestValidator>();
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
        services.AddSingleton<IValidator<MarkConversationReadRequest>, MarkConversationReadRequestValidator>();

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
        services.AddScoped<RescueCandidateService>();
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
        services.AddScoped<AgentRouteEntityStore>();
        services.AddScoped<Overmind.Infrastructure.VectorSearch.QdrantRouteVectorIndex>();
        services.AddScoped<Overmind.Infrastructure.Agent.Routes.IRouteVectorIndex>(
            provider => provider.GetRequiredService<Overmind.Infrastructure.VectorSearch.QdrantRouteVectorIndex>());
        services.AddScoped<Overmind.Infrastructure.Agent.Routes.IAgentRouteStore>(provider =>
            new Overmind.Infrastructure.Agent.Routes.RouteVectorIndexingStore(
                provider.GetRequiredService<AgentRouteEntityStore>(),
                provider.GetRequiredService<Overmind.Infrastructure.Agent.Routes.IRouteVectorIndex>(),
                provider.GetRequiredService<ILogger<Overmind.Infrastructure.Agent.Routes.RouteVectorIndexingStore>>()));
        services.AddScoped<AgentRouteRecorder>();
        services.AddScoped<ConversationReadStore>();
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
