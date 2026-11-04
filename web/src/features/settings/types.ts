export type PlanningMaterializationDefault = "off" | "on" | "archive";

export type PlanningValidationDefault = "on" | "off";

export type NotificationNudgeMode = "quiet" | "nag" | "mini";

export type AgentDelegateApprovalsDefault = "on" | "off";

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
  agentRouteMatchThreshold: number;
  agentInstructions: string;
  agentDelegateApprovalsDefault: AgentDelegateApprovalsDefault;
  telegramEnabled: boolean;
  telegramBotTokenConfigured: boolean;
  telegramAllowedChatIds: number[];
}

export interface UpdateSettingsResponse extends SettingsResponse {
  reindexRecommended: boolean;
}

export interface TelegramSettingsValues {
  telegramEnabled: boolean;
  telegramBotToken: string;
  telegramAllowedChatIds: number[];
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
  agentRouteMatchThreshold: number;
  agentInstructions: string;
  agentDelegateApprovalsDefault: AgentDelegateApprovalsDefault;
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
