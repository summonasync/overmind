"use client";

import { cn } from "@/shared/lib/cn";
import { useJournalTagCounts } from "@/features/journal/lib/use-journal";
import type { TagCount } from "@/features/journal/types";

interface TagRailProps {
  containerId: string;
  tagColors: ReadonlyMap<string, string | null>;
  activeTag: string;
  onSelect: (tag: string) => void;
}

export function TagRail({ containerId, tagColors, activeTag, onSelect }: TagRailProps) {
  const { counts, loading, error } = useJournalTagCounts(containerId);
  const rows = counts.filter((tag) => tag.count > 0);
  // A URL can pin a tag the container has no living notes for; the row still has to show,
  // or the filter reads as unset while the server keeps honouring it.
  const pinned =
    activeTag.length > 0 && !rows.some((tag) => tag.name === activeTag)
      ? { tagId: "", name: activeTag, count: 0 }
      : null;

  return (
    <aside aria-label="Tags" className="w-44 shrink-0 space-y-1">
      <p className="text-xs font-medium text-muted">Tags</p>
      {error ? (
        <p className="text-xs text-danger" role="alert">
          Tag counts are unavailable.
        </p>
      ) : loading && containerId.length > 0 ? (
        <p className="px-2 py-1 text-xs text-muted" role="status">
          Loading…
        </p>
      ) : null}
      <ul className="space-y-0.5">
        <li>
          <button
            type="button"
            onClick={() => onSelect("")}
            aria-pressed={activeTag.length === 0}
            className={railRowClasses(activeTag.length === 0)}
          >
            <span className="truncate">All notes</span>
          </button>
        </li>
        {pinned ? (
          <TagRailRow tag={pinned} active colors={tagColors} onSelect={onSelect} />
        ) : null}
        {rows.map((tag) => (
          <TagRailRow
            key={tag.tagId}
            tag={tag}
            active={tag.name === activeTag}
            colors={tagColors}
            onSelect={onSelect}
          />
        ))}
      </ul>
    </aside>
  );
}

const railRowClasses = (active: boolean) =>
  cn(
    "flex w-full items-center justify-between gap-2 rounded-md px-2 py-1 text-left text-sm transition-colors",
    "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
    active ? "bg-primary/10 text-primary" : "text-foreground hover:bg-surface-raised",
  );

interface TagRailRowProps {
  tag: TagCount;
  active: boolean;
  colors: ReadonlyMap<string, string | null>;
  onSelect: (tag: string) => void;
}

function TagRailRow({ tag, active, colors, onSelect }: TagRailRowProps) {
  return (
    <li>
      <button
        type="button"
        onClick={() => onSelect(tag.name)}
        aria-pressed={active}
        className={railRowClasses(active)}
      >
        <span className="flex min-w-0 items-center gap-1.5">
          <span
            aria-hidden
            className="h-2 w-2 shrink-0 rounded-full border border-border-strong"
            style={{ backgroundColor: colors.get(tag.name) ?? "transparent" }}
          />
          <span className="truncate">{tag.name}</span>
        </span>
        <span className="shrink-0 text-xs text-muted">{tag.count}</span>
      </button>
    </li>
  );
}
