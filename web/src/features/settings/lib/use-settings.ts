"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { reindex, testSettings, updateSettings } from "@/features/settings/api";
import { settingsKeys, settingsQueryOptions } from "@/features/settings/queries";
import type { SettingsFormValues, TestSettingsValues } from "@/features/settings/types";

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

  const test = useMutation({
    mutationFn: (values: TestSettingsValues) => testSettings(values),
  });

  const rebuild = useMutation({
    mutationFn: () => reindex(),
  });

  return { save, test, rebuild };
}
