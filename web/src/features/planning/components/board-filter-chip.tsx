"use client";

import type { ReactNode } from "react";
import { cn } from "@/shared/lib/cn";

interface BoardFilterChipProps {
  active: boolean;
  onClick: () => void;
  children: ReactNode;
}

export function BoardFilterChip({ active, onClick, children }: BoardFilterChipProps) {
  return (
    <button
      type="button"
      aria-pressed={active}
      onClick={onClick}
      className={cn(
        "inline-flex items-center gap-1 rounded-md border px-2 py-1 text-xs font-medium capitalize transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
        active
          ? "border-primary bg-primary/10 text-primary"
          : "border-border text-muted hover:border-border-strong hover:text-foreground",
      )}
    >
      {children}
    </button>
  );
}
