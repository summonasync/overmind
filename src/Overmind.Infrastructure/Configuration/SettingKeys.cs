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
    public const string NotificationNudgeMode = "notification.nudgeMode";
    public const string NotificationNudgeIntervalMinutes = "notification.nudgeIntervalMinutes";
}
