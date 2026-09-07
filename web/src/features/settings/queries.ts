import { queryOptions } from "@tanstack/react-query";
import { getSettings } from "@/features/settings/api";

export const settingsKeys = {
  all: ["settings"] as const,
  current: () => [...settingsKeys.all, "current"] as const,
};

export const settingsQueryOptions = () =>
  queryOptions({
    queryKey: settingsKeys.current(),
    queryFn: getSettings,
  });
