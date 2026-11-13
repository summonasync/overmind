"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  reindex,
  testSettings,
  updateHumanDigestSettings,
  updateSettings,
  updateTelegramSettings,
} from "@/features/settings/api";
import { settingsKeys, settingsQueryOptions } from "@/features/settings/queries";
import type {
  HumanDigestSettingsValues,
  SettingsFormValues,
  TelegramSettingsValues,
  TestSettingsValues,
} from "@/features/settings/types";

export function useSettings() {
  const query = useQuery(settingsQueryOptions());
  return { data: query.data ?? null, loading: query.isPending, error: query.error };
}

export function useSettingsMutations() {
  const queryClient = useQueryClient();

  const save = useMutation({
    mutationFn: (values: SettingsFormValues) => updateSettings(values),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: settingsKeys.current() }),
  });

  const telegramSave = useMutation({
    mutationFn: (values: TelegramSettingsValues) => updateTelegramSettings(values),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: settingsKeys.current() }),
  });

  const digestSave = useMutation({
    mutationFn: (values: HumanDigestSettingsValues) => updateHumanDigestSettings(values),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: settingsKeys.current() }),
  });

  const test = useMutation({
    mutationFn: (values: TestSettingsValues) => testSettings(values),
  });

  const rebuild = useMutation({
    mutationFn: () => reindex(),
  });

  return { save, telegramSave, digestSave, test, rebuild };
}
