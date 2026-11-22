"use client";

import { useCallback, useEffect, useMemo, useState, useSyncExternalStore } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  EMPTY_GRAPH_FILTERS,
  parseGraphFilters,
  writeGraphFilters,
} from "@/features/journal/lib/graph-filters";
import { paletteForDocument, type ResolvedPalette } from "@/features/journal/lib/graph-palette";
import { graphKeys, graphQueryOptions } from "@/features/journal/queries";
import { saveGraphPositions } from "@/features/journal/api";
import type { GraphPositionWrite, GraphViewFilters } from "@/features/journal/types";

// The same posture as the inbox: the URL is the filter store, nothing mirrors it into state, so
// a reload, a back gesture and a pasted link open the identical field. `replace` because a chip
// click is not a history entry the owner has to back out of one by one.
export function useGraphFilters() {
  const router = useRouter();
  const pathname = usePathname() ?? "/journal/graph";
  const params = useSearchParams();
  const query = params?.toString() ?? "";

  const filters = useMemo(
    () => parseGraphFilters(query.length > 0 ? new URLSearchParams(query) : null),
    [query],
  );

  const commit = useCallback(
    (next: GraphViewFilters) => {
      const target = new URLSearchParams(query);
      writeGraphFilters(target, next);
      const search = target.toString();
      router.replace(search.length > 0 ? `${pathname}?${search}` : pathname, {
        scroll: false,
      });
    },
    [pathname, query, router],
  );

  const setFilter = useCallback(
    (patch: Partial<GraphViewFilters>) => commit({ ...filters, ...patch }),
    [commit, filters],
  );

  const clear = useCallback(() => commit(EMPTY_GRAPH_FILTERS), [commit]);

  return { filters, setFilter, clear };
}

export function useGraphField(filters: GraphViewFilters) {
  const query = useQuery(graphQueryOptions(filters));
  return {
    field: query.data,
    loading: query.isPending,
    error: query.error,
    refetch: query.refetch,
  };
}

// The write half of the pin door: the canvas mutates through here and the field query refetches
// as the confirmation. No optimistic cache surgery — the read projection already answered "which
// dots are pinned", and a hand-patched cache is a second place for that truth to drift.
export function useGraphPinWriter() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (positions: GraphPositionWrite[]) => saveGraphPositions(positions),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: graphKeys.all }),
  });
}

const REDUCED_MOTION_QUERY = "(prefers-reduced-motion: reduce)";

// R7's settled-layout switch. It is a browser capability read, not data, which is the one thing
// `useEffect` is for in this slice; the field itself comes from React Query.
export function useReducedMotion(): boolean {
  const [reduced, setReduced] = useState(() =>
    typeof window === "undefined" ? false : window.matchMedia(REDUCED_MOTION_QUERY).matches,
  );

  useEffect(() => {
    if (typeof window === "undefined") {
      return;
    }
    const listener = (event: MediaQueryListEvent) => setReduced(event.matches);
    const media = window.matchMedia(REDUCED_MOTION_QUERY);
    media.addEventListener("change", listener);
    return () => media.removeEventListener("change", listener);
  }, []);

  return reduced;
}

export interface GraphTheme {
  palette: ResolvedPalette;
  font: string;
  // True until the stylesheet has been read, which on a static export is after hydration: the
  // prerendered HTML has no `document`, so the first client pass is the first real answer.
  pending: boolean;
}

const FALLBACK_FONT_STACK = "ui-sans-serif, system-ui, sans-serif";
const PENDING_THEME: GraphTheme = {
  palette: { colors: {} as ResolvedPalette["colors"], unresolved: ["label"] },
  font: FALLBACK_FONT_STACK,
  pending: true,
};

let cachedTheme: GraphTheme | null = null;

// The shipped stylesheet is read once per mount and never changes under the canvas, so this is
// an external store with a constant subscription rather than a `setState` inside an effect —
// which is also what keeps the prerendered shell from claiming the palette failed.
function readTheme(): GraphTheme {
  if (cachedTheme === null) {
    const resolved = paletteForDocument();
    const stack =
      typeof document === "undefined"
        ? ""
        : getComputedStyle(document.documentElement).getPropertyValue("--font-sans").trim();
    cachedTheme = {
      palette: resolved,
      font: stack.length > 0 ? stack : FALLBACK_FONT_STACK,
      pending: false,
    };
  }
  return cachedTheme;
}

function subscribe(): () => void {
  return () => {};
}

export function useGraphTheme(): GraphTheme {
  return useSyncExternalStore(subscribe, readTheme, () => PENDING_THEME);
}
