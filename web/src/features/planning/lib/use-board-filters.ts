"use client";

import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import {
  EMPTY_BOARD_FILTERS,
  coerceBoardFilters,
  isBoardFilterActive,
  parseBoardFilters,
} from "@/features/planning/lib/board-filters";
import type { PlanningBoardFilters } from "@/features/planning/lib/board-filters";
import { readJson, writeJson } from "@/shared/lib/session-json";

function readFiltersFromLocation(): PlanningBoardFilters {
  return parseBoardFilters(new URLSearchParams(window.location.search));
}

const FILTER_CHANGE_EVENT = "board-filters:change";
const BOARD_FILTERS_STORAGE_KEY = "overmind.board.filters";
const FILTER_PARAM_NAMES = ["status", "priority", "spec", "plan", "current", "hidedone", "blocked"] as const;

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
    if (next.statuses.length > 0) {
      params.set("status", next.statuses.join(","));
    } else {
      params.delete("status");
    }
    if (next.priorities.length > 0) {
      params.set("priority", next.priorities.join(","));
    } else {
      params.delete("priority");
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
    if (!next.hideDone) {
      params.set("hidedone", "0");
    } else {
      params.delete("hidedone");
    }
    if (next.blockedOnly) {
      params.set("blocked", "1");
    } else {
      params.delete("blocked");
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
