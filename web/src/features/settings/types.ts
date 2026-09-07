export interface SettingsResponse {
  embeddingProvider: string;
  ollamaBaseUrl: string;
  openAiBaseUrl: string;
  openAiApiKeyConfigured: boolean;
}

export interface SettingsFormValues {
  embeddingProvider: string;
  ollamaBaseUrl: string;
  openAiBaseUrl: string;
  openAiApiKey: string;
}

export interface ReindexReport {
  total: number;
  indexed: number;
  failed: number;
}
