"use client";

import { useCallback, useSyncExternalStore } from "react";
import {
  sectionPrefsSnapshot,
  subscribeSectionPrefs,
  toggleSection,
} from "@/widgets/app-shell/lib/section-prefs";

interface SectionPrefsApi {
  isCollapsed: (section: string) => boolean;
  toggle: (section: string) => void;
}

export function useSectionPrefs(page: string): SectionPrefsApi {
  const map = useSyncExternalStore(
    subscribeSectionPrefs,
    sectionPrefsSnapshot,
    sectionPrefsSnapshot,
  );

  const isCollapsed = useCallback(
    (section: string) => map[page]?.[section] === true,
    [map, page],
  );
  const toggle = useCallback((section: string) => toggleSection(page, section), [page]);

  return { isCollapsed, toggle };
}
