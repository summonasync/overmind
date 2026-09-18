"use client";

import { useCallback } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import {
  EMPTY_BOARD_FILTERS,
  isBoardFilterActive,
  parseBoardFilters,
} from "@/features/planning/lib/board-filters";
import type { PlanningBoardFilters } from "@/features/planning/lib/board-filters";

export function useBoardFilters() {
  const router = useRouter();
  const searchParams = useSearchParams();

  const filters = parseBoardFilters(searchParams ?? new URLSearchParams());
  const isActive = isBoardFilterActive(filters);

  const setFilters = useCallback(
    (next: PlanningBoardFilters) => {
      const params = new URLSearchParams(searchParams?.toString() ?? "");
      if (next.statuses.length > 0) {
        params.set("status", next.statuses.join(","));
      } else {
        params.delete("status");
      }
      if (next.spec !== "any") {
        params.set("spec", next.spec);
      } else {
        params.delete("spec");
      }
      if (next.plan !== "any") {
        params.set("plan", next.plan);
      } else {
        params.delete("plan");
      }
      if (next.currentOnly) {
        params.set("current", "1");
      } else {
        params.delete("current");
      }
      const query = params.toString();
      router.replace(query ? `/planning?${query}` : "/planning", { scroll: false });
    },
    [router, searchParams],
  );

  const clearFilters = useCallback(() => {
    setFilters(EMPTY_BOARD_FILTERS);
  }, [setFilters]);

  return { filters, isActive, setFilters, clearFilters };
}
