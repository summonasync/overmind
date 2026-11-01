import type { ReactNode } from "react";
import { cn } from "@/shared/lib/cn";

export type HintAlign = "center" | "left" | "right";

const ALIGN_CLASSES: Record<HintAlign, { bubble: string; arrow: string }> = {
  center: { bubble: "left-1/2 -translate-x-1/2", arrow: "left-1/2 -translate-x-1/2" },
  left: { bubble: "left-0", arrow: "left-3" },
  right: { bubble: "right-0", arrow: "right-3" },
};

export function hintAnchorId(label: string): string {
  const slug = label
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
  return slug ? `hint-${slug}` : "hint";
}

interface HintProps {
  label: string;
  children: ReactNode;
  align?: HintAlign;
  id?: string;
  className?: string;
}

export function Hint({ label, children, align = "center", id, className }: HintProps) {
  const anchorId = id ?? hintAnchorId(label);
  const alignClasses = ALIGN_CLASSES[align];

  return (
    <span className={cn("group relative inline-flex", className)}>
      <button
        type="button"
        aria-describedby={anchorId}
        className="cursor-help rounded text-muted transition-colors hover:text-foreground focus-visible:text-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-primary"
      >
        {label}
      </button>
      <span
        role="tooltip"
        id={anchorId}
        className={cn(
          "pointer-events-none absolute top-full z-30 mt-2 w-64 rounded-md border border-border-strong bg-surface-raised px-3 py-2 text-xs leading-5 text-foreground opacity-0 shadow-xl transition-opacity duration-150",
          "group-hover:opacity-100 group-focus-within:opacity-100",
          alignClasses.bubble,
        )}
      >
        <span
          aria-hidden
          className={cn(
            "absolute -top-1 h-2 w-2 rotate-45 border-l border-t border-border-strong bg-surface-raised",
            alignClasses.arrow,
          )}
        />
        {children}
      </span>
    </span>
  );
}
