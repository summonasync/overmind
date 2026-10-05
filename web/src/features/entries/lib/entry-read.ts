import type { CSSProperties } from "react";
import type { EntryType } from "@/shared/api/types";

export type ExpandedEntries = ReadonlySet<string>;

export function toggleEntryExpanded(expanded: ExpandedEntries, id: string): Set<string> {
  const next = new Set(expanded);
  if (next.has(id)) {
    next.delete(id);
  } else {
    next.add(id);
  }
  return next;
}

export function entryReadBody(content: string): string | null {
  return content.trim().length > 0 ? content : null;
}

export function entryReadMetaLine(version: number, createdLabel: string, updatedLabel: string): string {
  return `v${version} · created ${createdLabel} · updated ${updatedLabel}`;
}

export function entryTagChipStyle(color: string | null): CSSProperties | undefined {
  if (color === null) {
    return undefined;
  }
  return { backgroundColor: `${color}1A`, color, borderColor: `${color}40` };
}

export function entryRowClasses(type: EntryType): string {
  const base = "px-4 py-3 transition-colors hover:bg-surface-raised";
  return type === "tombstone" ? `${base} bg-surface-raised/40 hover:bg-surface-raised/70` : base;
}

export function entryTitleClasses(type: EntryType): string {
  return type === "tombstone" ? "text-muted" : "text-foreground";
}
