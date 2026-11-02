export type PlanningMaterializationDefault = "off" | "on" | "archive";

export type PlanningValidationDefault = "on" | "off";

export type NotificationNudgeMode = "quiet" | "nag" | "mini";

export interface SettingsResponse {
  embeddingProvider: string;
  ollamaBaseUrl: string;
  ollamaModel: string;
  openAiBaseUrl: string;
  qdrantEndpoint: string;
  openAiApiKeyConfigured: boolean;
  planningMaterializationDefault: PlanningMaterializationDefault;
  planningRequireHumanValidationDefault: PlanningValidationDefault;
  planningAgentNamingRulesDefault: string;
  notificationNudgeMode: NotificationNudgeMode;
  notificationNudgeIntervalMinutes: number;
  agentBaseUrl: string;
  agentModel: string;
  agentApiKeyConfigured: boolean;
  agentPerTurnMaxTokens: number;
  agentDailyBudget: number;
  agentInstructions: string;
}

export interface UpdateSettingsResponse extends SettingsResponse {
  reindexRecommended: boolean;
}

export interface SettingsFormValues {
  embeddingProvider: string;
  ollamaBaseUrl: string;
  ollamaModel: string;
  openAiBaseUrl: string;
  openAiApiKey: string;
  qdrantEndpoint: string;
  planningMaterializationDefault: PlanningMaterializationDefault;
  planningRequireHumanValidationDefault: PlanningValidationDefault;
  planningAgentNamingRulesDefault: string;
  notificationNudgeMode: NotificationNudgeMode;
  notificationNudgeIntervalMinutes: number;
  agentBaseUrl: string;
  agentModel: string;
  agentApiKey: string;
  agentPerTurnMaxTokens: number;
  agentDailyBudget: number;
  agentInstructions: string;
}

export interface TestSettingsValues {
  provider: string;
  ollamaBaseUrl: string;
  ollamaModel: string;
  openAiBaseUrl: string;
  qdrantEndpoint: string;
}

export interface ReindexReport {
  total: number;
  indexed: number;
  failed: number;
}
