"use client";

import { Ban, EyeOff, Pin } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { BoardFilterChip } from "@/features/planning/components/board-filter-chip";
import type { BoardFastChipId, PlanningBoardFilters } from "@/features/planning/lib/board-filters";

interface BoardFastChipDescriptor {
  id: BoardFastChipId;
  label: string;
  icon: LucideIcon;
  hint: string;
}

const FAST_CHIPS: readonly BoardFastChipDescriptor[] = [
  { id: "current", label: "Current", icon: Pin, hint: "Pinned feature only" },
  { id: "hideDone", label: "Hide done", icon: EyeOff, hint: "Hide done features" },
  { id: "blocked", label: "Blocked", icon: Ban, hint: "Blocked features only" },
];

interface BoardFastChipsProps {
  filters: PlanningBoardFilters;
  onToggle: (chip: BoardFastChipId) => void;
}

export function BoardFastChips({ filters, onToggle }: BoardFastChipsProps) {
  return (
    <div role="group" aria-label="Fast board filters" className="flex flex-wrap items-center gap-1.5">
      {FAST_CHIPS.map(({ id, label, icon: Icon, hint }) => (
        <BoardFilterChip
          key={id}
          active={isChipActive(filters, id)}
          onClick={() => onToggle(id)}
        >
          <Icon size={12} aria-hidden />
          {label}
          <span className="sr-only">({hint})</span>
        </BoardFilterChip>
      ))}
    </div>
  );
}

function isChipActive(filters: PlanningBoardFilters, chip: BoardFastChipId): boolean {
  switch (chip) {
    case "current":
      return filters.pinnedOnly;
    case "hideDone":
      return filters.hideDone;
    case "blocked":
      return filters.blockedOnly;
  }
}
