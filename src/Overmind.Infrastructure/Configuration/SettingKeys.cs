namespace Overmind.Infrastructure.Configuration;

public static class SettingKeys
{
    public const string EmbeddingProvider = "embedding.provider";
    public const string EmbeddingOllamaBaseUrl = "embedding.ollama.baseUrl";
    public const string EmbeddingOllamaModel = "embedding.ollama.model";
    public const string EmbeddingOpenAiBaseUrl = "embedding.openai.baseUrl";
    public const string EmbeddingOpenAiApiKey = "embedding.openai.apiKey";
    public const string QdrantEndpoint = "qdrant.endpoint";
    public const string TreeRoots = "treeRoots";
    public const string PlanningMaterializationDefault = "planning.materializationDefault";
    public const string PlanningMaterializationSync = "planning.materializationSync";
    public const string PlanningRequireHumanValidationDefault = "planning.requireHumanValidationDefault";
    public const string PlanningAgentNamingRulesDefault = "planning.agentNamingRulesDefault";
    public const string NotificationNudgeMode = "notification.nudgeMode";
    public const string NotificationNudgeIntervalMinutes = "notification.nudgeIntervalMinutes";

    public const string GrimoireOverridePath = "grimoire.overridePath";
    public const string GrimoireBookName = "grimoire.bookName";
    public const string GrimoireWired = "grimoire.wired";
    public const string GrimoireInstallChannel = "grimoire.installChannel";

    public const string AgentBaseUrl = "agent.baseUrl";
    public const string AgentApiKey = "agent.apiKey";
    public const string AgentModel = "agent.model";
    public const string AgentAlias = "agent.alias";
    public const string AgentPerTurnMaxTokens = "agent.perTurnMaxTokens";
    public const string AgentDailyBudget = "agent.dailyBudget";
    public const string AgentDailyBudgetUsedPrefix = "agent.budget.used.";
    public const string AgentGrimoireBaseUrl = "agent.grimoire.baseUrl";
    public const string AgentGrimoireTome = "agent.grimoire.tome";
    public const string AgentGrimoireCompactionHours = "agent.grimoire.compactionHours";
}
