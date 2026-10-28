"use client";

import { usePlanningBoard } from "@/features/planning/lib/use-planning";

export function useFeatureTitle(projectId: string, slug: string): string | null {
  const { data } = usePlanningBoard(projectId);
  const feature = slug.length === 0 ? undefined : data.find((item) => item.slug === slug);
  return feature?.title ?? null;
}
