"use client";

import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import {
  EMPTY_BOARD_FILTERS,
  isBoardFilterActive,
  parseBoardFilters,
} from "@/features/planning/lib/board-filters";
import type { PlanningBoardFilters } from "@/features/planning/lib/board-filters";

function readFiltersFromLocation(): PlanningBoardFilters {
  return parseBoardFilters(new URLSearchParams(window.location.search));
}

const FILTER_CHANGE_EVENT = "board-filters:change";

export function useBoardFilters() {
  const initialSearchParams = useSearchParams();
  const [filters, setFiltersState] = useState<PlanningBoardFilters>(() =>
    parseBoardFilters(initialSearchParams ?? new URLSearchParams()),
  );

  useEffect(() => {
    const syncFromLocation = () => {
      setFiltersState(readFiltersFromLocation());
    };
    window.addEventListener("popstate", syncFromLocation);
    window.addEventListener(FILTER_CHANGE_EVENT, syncFromLocation);
    return () => {
      window.removeEventListener("popstate", syncFromLocation);
      window.removeEventListener(FILTER_CHANGE_EVENT, syncFromLocation);
    };
  }, []);

  const setFilters = useCallback((next: PlanningBoardFilters) => {
    setFiltersState(next);
    const params = new URLSearchParams(window.location.search);
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
    const path = window.location.pathname;
    window.history.replaceState(null, "", query ? `${path}?${query}` : path);
    window.dispatchEvent(new Event(FILTER_CHANGE_EVENT));
  }, []);

  const clearFilters = useCallback(() => {
    setFilters({ ...EMPTY_BOARD_FILTERS });
  }, [setFilters]);

  const isActive = isBoardFilterActive(filters);

  return { filters, isActive, setFilters, clearFilters };
}
