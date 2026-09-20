export type PlanningMaterializationDefault = "off" | "on" | "archive";

export type PlanningValidationDefault = "on" | "off";

export interface SettingsResponse {
  embeddingProvider: string;
  ollamaBaseUrl: string;
  ollamaModel: string;
  openAiBaseUrl: string;
  qdrantEndpoint: string;
  openAiApiKeyConfigured: boolean;
  planningMaterializationDefault: PlanningMaterializationDefault;
  planningRequireHumanValidationDefault: PlanningValidationDefault;
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
