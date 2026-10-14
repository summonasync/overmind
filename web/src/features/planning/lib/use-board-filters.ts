"use client";

import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import {
  EMPTY_BOARD_FILTERS,
  coerceBoardFilters,
  isBoardFilterActive,
  parseBoardFilters,
  writeBoardFilterParams,
} from "@/features/planning/lib/board-filters";
import type { PlanningBoardFilters } from "@/features/planning/lib/board-filters";
import { readJson, writeJson } from "@/shared/lib/session-json";

function readFiltersFromLocation(): PlanningBoardFilters {
  return parseBoardFilters(new URLSearchParams(window.location.search));
}

const FILTER_CHANGE_EVENT = "board-filters:change";
const BOARD_FILTERS_STORAGE_KEY = "overmind.board.filters";
const FILTER_PARAM_NAMES = ["status", "priority", "spec", "plan", "pinned", "hidedone", "blocked", "attention"] as const;

export function useBoardFilters() {
  const initialSearchParams = useSearchParams();
  const [filters, setFiltersState] = useState<PlanningBoardFilters>(() =>
    parseBoardFilters(initialSearchParams ?? new URLSearchParams()),
  );

  useEffect(() => {
    const frame = requestAnimationFrame(() => {
      const urlParams = new URLSearchParams(window.location.search);
      const urlCarriesFilters = FILTER_PARAM_NAMES.some(
        (name) => urlParams.get(name) !== null,
      );
      if (urlCarriesFilters) {
        return;
      }
      const stored = coerceBoardFilters(
        readJson<unknown>(BOARD_FILTERS_STORAGE_KEY, null),
      );
      if (stored !== null) {
        setFiltersState(stored);
      }
    });
    return () => cancelAnimationFrame(frame);
  }, []);

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
    writeJson(BOARD_FILTERS_STORAGE_KEY, next);
    const params = new URLSearchParams(window.location.search);
    writeBoardFilterParams(params, next);
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
