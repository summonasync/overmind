import type { CSSProperties } from "react";

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
