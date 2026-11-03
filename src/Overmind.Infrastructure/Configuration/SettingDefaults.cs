namespace Overmind.Infrastructure.Configuration;

public static class SettingDefaults
{
    public const string EmbeddingProvider = "onnx";
    public const string EmbeddingOllamaBaseUrl = "http://127.0.0.1:11434";
    public const string EmbeddingOllamaModel = "all-minilm-l6-v2";
    public const string EmbeddingOpenAiBaseUrl = "https://api.openai.com";
    public const string QdrantEndpoint = "http://127.0.0.1:6334";

    public const string PlanningMaterializationDefault = "on";
    public const string PlanningMaterializationSync = "off";
    public const string PlanningRequireHumanValidationDefault = "off";
    public const string PlanningAgentNamingRulesDefault = "";

    public const string NotificationNudgeMode = "quiet";
    public const int NotificationNudgeIntervalMinutes = 10;

    public const string GrimoireOverridePath = "";
    public const string GrimoireBookName = "";
    public const string GrimoireWired = "false";
    public const string GrimoireInstallChannel = "";

    public const string AgentBaseUrl = "https://api.z.ai/api/paas/v4";
    public const string AgentModel = "glm-5.3-flash";
    public const string AgentAlias = "overmind-agent";
    public const int AgentPerTurnMaxTokens = 8192;
    public const int AgentDailyBudget = 0;
    public const string AgentInstructions = "";
    public const int AgentInstructionsMaxLength = 4000;
    public const string AgentDelegateApprovalsDefault = "off";

    public const string AgentGrimoireBaseUrl = "";
    public const string AgentGrimoireTome = "crew";
    public const string AgentGrimoirePersonalTome = "";
    public const string AgentGrimoireCrewTome = "crew";
    public const int AgentGrimoireCompactionHours = 24;

    public const string TelegramEnabled = "false";
    public const string TelegramAllowedChatIds = "";

    public static readonly string[] EmbeddingProviders = ["onnx", "ollama", "openai"];
    public static readonly string[] PlanningMaterializationDefaults = ["off", "on", "archive"];
    public static readonly string[] PlanningMaterializationSyncModes = ["on", "off"];
    public static readonly string[] PlanningRequireHumanValidationDefaults = ["on", "off"];
    public static readonly string[] AgentDelegateApprovalsDefaults = ["on", "off"];
    public static readonly string[] NotificationNudgeModes = ["quiet", "nag", "mini"];
}
