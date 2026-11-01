"use client";

import { readJson, writeJson } from "../../../shared/lib/session-json.ts";

export type SectionPrefs = Record<string, Record<string, boolean>>;

export const INFO_PANEL_SECTIONS_KEY = "overmind.info-panel.sections";
const PREFS_EVENT = "overmind:info-panel-sections";

let cache: SectionPrefs | null = null;

function toMap(raw: unknown): SectionPrefs {
  return typeof raw === "object" && raw !== null ? (raw as SectionPrefs) : {};
}

function readMap(): SectionPrefs {
  if (cache === null) {
    cache = toMap(readJson<unknown>(INFO_PANEL_SECTIONS_KEY, {}));
  }
  return cache;
}

export function sectionPrefsSnapshot(): SectionPrefs {
  return readMap();
}

export function sectionCollapsed(page: string, section: string): boolean {
  return readMap()[page]?.[section] === true;
}

export function toggleSection(page: string, section: string): void {
  if (typeof window === "undefined") {
    return;
  }

  const map = readMap();
  const sections: Record<string, boolean> = { ...map[page] };
  if (sections[section] === true) {
    delete sections[section];
  } else {
    sections[section] = true;
  }
  const next: SectionPrefs = { ...map, [page]: sections };

  writeJson(INFO_PANEL_SECTIONS_KEY, next);
  cache = next;
  window.dispatchEvent(new Event(PREFS_EVENT));
}

export function subscribeSectionPrefs(listener: () => void): () => void {
  if (typeof window === "undefined") {
    return () => undefined;
  }

  const notify = () => {
    cache = null;
    listener();
  };
  const onStorage = (event: StorageEvent) => {
    if (event.key === null || event.key === INFO_PANEL_SECTIONS_KEY) {
      notify();
    }
  };

  window.addEventListener(PREFS_EVENT, notify);
  window.addEventListener("storage", onStorage);
  return () => {
    window.removeEventListener(PREFS_EVENT, notify);
    window.removeEventListener("storage", onStorage);
  };
}
