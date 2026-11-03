import { apiGet, apiPost, apiPut } from "@/shared/api/client";
import type { VectorSearchHealth } from "@/shared/api/types";
import type {
  ReindexReport,
  SettingsFormValues,
  SettingsResponse,
  TelegramSettingsValues,
  TestSettingsValues,
  UpdateSettingsResponse,
} from "@/features/settings/types";

export function getSettings(): Promise<SettingsResponse> {
  return apiGet<SettingsResponse>("/settings");
}

export function updateSettings(values: SettingsFormValues): Promise<UpdateSettingsResponse> {
  return apiPut<UpdateSettingsResponse>("/settings", {
    embeddingProvider: values.embeddingProvider,
    ollamaBaseUrl: values.ollamaBaseUrl || null,
    ollamaModel: values.ollamaModel || null,
    openAiBaseUrl: values.openAiBaseUrl || null,
    openAiApiKey: values.openAiApiKey || null,
    qdrantEndpoint: values.qdrantEndpoint || null,
    planningMaterializationDefault: values.planningMaterializationDefault,
    planningRequireHumanValidationDefault: values.planningRequireHumanValidationDefault,
    planningAgentNamingRulesDefault: values.planningAgentNamingRulesDefault,
    notificationNudgeMode: values.notificationNudgeMode,
    notificationNudgeIntervalMinutes: values.notificationNudgeIntervalMinutes,
    agentBaseUrl: values.agentBaseUrl || null,
    agentModel: values.agentModel || null,
    agentApiKey: values.agentApiKey || null,
    agentPerTurnMaxTokens: values.agentPerTurnMaxTokens,
    agentDailyBudget: values.agentDailyBudget,
    agentInstructions: values.agentInstructions,
    agentDelegateApprovalsDefault: values.agentDelegateApprovalsDefault,
  });
}

export function updateTelegramSettings(values: TelegramSettingsValues): Promise<UpdateSettingsResponse> {
  return apiPut<UpdateSettingsResponse>("/settings", {
    telegramEnabled: values.telegramEnabled,
    telegramBotToken: values.telegramBotToken || null,
    telegramAllowedChatIds: values.telegramAllowedChatIds,
  });
}

export function testSettings(values: TestSettingsValues): Promise<VectorSearchHealth> {
  return apiPost<VectorSearchHealth>("/settings/test", {
    provider: values.provider,
    ollamaBaseUrl: values.ollamaBaseUrl || null,
    ollamaModel: values.ollamaModel || null,
    openAiBaseUrl: values.openAiBaseUrl || null,
    qdrantEndpoint: values.qdrantEndpoint || null,
  });
}

export function reindex(): Promise<ReindexReport> {
  return apiPost<ReindexReport>("/search/reindex");
}
