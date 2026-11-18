"use client";

import { useCallback, useMemo } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import {
  EMPTY_JOURNAL_FILTERS,
  parseJournalFilters,
  writeJournalFilters,
} from "@/features/journal/lib/stream-filters";
import type { JournalStreamFilters } from "@/features/journal/types";

// The URL is the filter store: nothing here mirrors it into local state, so a reload, a
// back gesture and a shared link all open the same list. `router.replace` rather than
// `push` because every keystroke in a select would otherwise become a history entry the
// owner has to back out of one by one.
export function useJournalFilters() {
  const router = useRouter();
  const pathname = usePathname() ?? "/journal";
  const params = useSearchParams();
  const query = params?.toString() ?? "";

  const filters = useMemo(
    () => parseJournalFilters(query.length > 0 ? new URLSearchParams(query) : null),
    [query],
  );

  const commit = useCallback(
    (next: JournalStreamFilters) => {
      const target = new URLSearchParams(query);
      writeJournalFilters(target, next);
      const search = target.toString();
      router.replace(search.length > 0 ? `${pathname}?${search}` : pathname, {
        scroll: false,
      });
    },
    [pathname, query, router],
  );

  const setFilter = useCallback(
    (patch: Partial<JournalStreamFilters>) => commit({ ...filters, ...patch }),
    [commit, filters],
  );

  const clear = useCallback(() => commit(EMPTY_JOURNAL_FILTERS), [commit]);

  return {
    filters,
    setFilter,
    clear,
  };
}
