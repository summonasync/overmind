import { apiGet, apiPost, apiPut } from "@/shared/api/client";
import type { ReindexReport, SettingsFormValues, SettingsResponse } from "@/features/settings/types";

export function getSettings(): Promise<SettingsResponse> {
  return apiGet<SettingsResponse>("/settings");
}

export function updateSettings(values: SettingsFormValues): Promise<SettingsResponse> {
  return apiPut<SettingsResponse>("/settings", {
    embeddingProvider: values.embeddingProvider,
    ollamaBaseUrl: values.ollamaBaseUrl || null,
    openAiBaseUrl: values.openAiBaseUrl || null,
    openAiApiKey: values.openAiApiKey || null,
  });
}

export function reindex(): Promise<ReindexReport> {
  return apiPost<ReindexReport>("/search/reindex");
}
