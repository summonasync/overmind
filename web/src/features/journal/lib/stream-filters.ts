// The URL is the inbox's only state: reading these four params back must reproduce the
// same list, so the grammar lives here, away from the components, where node --test can
// reach it. Deep relative import with a spelled-out extension is deliberate — the test
// runner loads this module without the bundler's `@/` alias map.
import { parseInstant } from "../../../shared/lib/relative-time.ts";
import type {
  JournalLinkFilter,
  JournalServerFilters,
  JournalStreamFilters,
} from "../types.ts";

export const EMPTY_JOURNAL_FILTERS: JournalStreamFilters = {
  day: "",
  tag: "",
  link: "",
  project: "",
};

export const JOURNAL_FILTER_KEYS = ["day", "tag", "link", "project"] as const;

const DAY_SHAPE = /^\d{4}-\d{2}-\d{2}$/;

export function journalServerFilters(filters: JournalStreamFilters): JournalServerFilters {
  return { tag: filters.tag, link: filters.link, project: filters.project };
}

export function isJournalFilterActive(filters: JournalStreamFilters): boolean {
  return JOURNAL_FILTER_KEYS.some((key) => filters[key].length > 0);
}

function readDay(raw: string | null): string {
  const value = raw?.trim() ?? "";
  if (!DAY_SHAPE.test(value)) {
    return "";
  }
  // 2026-02-31 has the right shape and never happens. The native date input cannot
  // produce it; a hand-typed URL can, and passing it through would ask for a window
  // whose upper bound rolled into March.
  const [year, month, dayOfMonth] = value.split("-").map(Number);
  const probe = new Date(year, month - 1, dayOfMonth);
  return probe.getMonth() === month - 1 && probe.getDate() === dayOfMonth ? value : "";
}

function readLink(raw: string | null): JournalLinkFilter {
  const value = raw?.trim().toLowerCase() ?? "";
  // The server would rather 400 (UnknownLinkFilter) than answer a bogus link value with
  // an empty page, which is right for an API and wrong for a hand-typed URL: the inbox
  // degrades to "no link filter" and stays readable.
  return value === "linked" || value === "unlinked" ? value : "";
}

export function parseJournalFilters(source: URLSearchParams | null): JournalStreamFilters {
  return {
    day: readDay(source?.get("day") ?? null),
    tag: source?.get("tag")?.trim() ?? "",
    link: readLink(source?.get("link") ?? null),
    project: source?.get("project")?.trim() ?? "",
  };
}

// Writes in place so params this view does not own (`note` on the detail branch) survive.
export function writeJournalFilters(
  target: URLSearchParams,
  filters: JournalStreamFilters,
): URLSearchParams {
  for (const key of JOURNAL_FILTER_KEYS) {
    const value = filters[key];
    if (value.length > 0) {
      target.set(key, value);
    } else {
      target.delete(key);
    }
  }
  return target;
}

function pad(value: number): string {
  return value < 10 ? `0${value}` : String(value);
}

// The local calendar day of an instant, spelled the way the URL spells it. This is NOT
// `toISOString().slice(0, 10)` — that answers in UTC and the owner lives by a wall clock,
// so a note touched at 23:58 would land on tomorrow's shelf.
export function localDayOf(instant: string): string | null {
  const at = parseInstant(instant);
  if (at === null) {
    return null;
  }
  const date = new Date(at);
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

export function formatJournalStamp(instant: string): string {
  const at = parseInstant(instant);
  if (at === null) {
    return instant;
  }
  const date = new Date(at);
  return `${localDayOf(instant)} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

interface ActivityStamped {
  id: string;
  updatedAt: string;
}

// Newest activity first, restated client-side because the server's own tie order is not
// part of its contract and a refetched page must not visibly shuffle under the reader.
export function sortByActivityDesc<T extends ActivityStamped>(notes: readonly T[]): T[] {
  return [...notes].sort((a, b) => {
    const byUpdated = (parseInstant(b.updatedAt) ?? 0) - (parseInstant(a.updatedAt) ?? 0);
    return byUpdated !== 0 ? byUpdated : a.id.localeCompare(b.id);
  });
}

export function selectJournalDay<T extends ActivityStamped>(
  notes: readonly T[],
  day: string,
): T[] {
  if (day.length === 0) {
    return sortByActivityDesc(notes);
  }
  return sortByActivityDesc(notes.filter((note) => localDayOf(note.updatedAt) === day));
}
