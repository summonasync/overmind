export type PlanningMaterializationDefault = "off" | "on" | "archive";

export type PlanningValidationDefault = "on" | "off";

export interface SettingsResponse {
  embeddingProvider: string;
  ollamaBaseUrl: string;
  openAiBaseUrl: string;
  openAiApiKeyConfigured: boolean;
  planningMaterializationDefault: PlanningMaterializationDefault;
  planningRequireHumanValidationDefault: PlanningValidationDefault;
}

export interface SettingsFormValues {
  embeddingProvider: string;
  ollamaBaseUrl: string;
  openAiBaseUrl: string;
  openAiApiKey: string;
  planningMaterializationDefault: PlanningMaterializationDefault;
  planningRequireHumanValidationDefault: PlanningValidationDefault;
}

export interface ReindexReport {
  total: number;
  indexed: number;
  failed: number;
}
