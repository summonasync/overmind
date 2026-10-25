"use client";

import { useEffect, useRef, useState, type SyntheticEvent } from "react";
import { ChevronDown } from "lucide-react";
import { cn } from "@/shared/lib/cn";
import type { LaneClaimState } from "@/features/lanes/types";

interface LaneWidgetMenuProps {
  slug: string;
  href: string;
  claimAlias?: string | null;
  claimState?: LaneClaimState | null;
}

const keepPressLocal = (event: SyntheticEvent) => event.stopPropagation();

const itemClasses = cn(
  "block w-full px-3 py-1.5 text-left text-xs text-foreground transition-colors",
  "hover:bg-primary/10 hover:text-primary",
  "focus-visible:outline-none focus-visible:bg-primary/10 focus-visible:text-primary",
);

export function LaneWidgetMenu({ slug, href, claimAlias, claimState }: LaneWidgetMenuProps) {
  const [open, setOpen] = useState(false);
  const [copied, setCopied] = useState(false);
  const zoneRef = useRef<HTMLSpanElement>(null);

  useEffect(() => {
    if (!open) {
      return;
    }
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
      }
    };
    const handlePointerDown = (event: PointerEvent) => {
      if (
        zoneRef.current !== null &&
        event.target instanceof Node &&
        !zoneRef.current.contains(event.target)
      ) {
        setOpen(false);
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    window.addEventListener("pointerdown", handlePointerDown);
    return () => {
      window.removeEventListener("keydown", handleKeyDown);
      window.removeEventListener("pointerdown", handlePointerDown);
    };
  }, [open]);

  useEffect(() => {
    if (!copied) {
      return;
    }
    const timer = window.setTimeout(() => setCopied(false), 2000);
    return () => window.clearTimeout(timer);
  }, [copied]);

  const copyLink = () => {
    const url = new URL(href, window.location.origin).href;
    navigator.clipboard.writeText(url).then(
      () => setCopied(true),
      () => setCopied(false),
    );
  };

  const claimReadout =
    claimAlias === null || claimAlias === undefined
      ? "unclaimed"
      : `held by ${claimAlias}${claimState === null || claimState === undefined ? "" : ` · ${claimState}`}`;

  return (
    <span ref={zoneRef} onPointerDown={keepPressLocal} className="relative ml-auto shrink-0">
      <button
        type="button"
        aria-label={`Feature actions for ${slug}`}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((current) => !current)}
        className={cn(
          "rounded p-0.5 text-muted transition-colors",
          "hover:bg-primary/10 hover:text-primary",
          "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
          open && "bg-primary/10 text-primary",
        )}
      >
        <ChevronDown
          size={12}
          aria-hidden
          className={cn("transition-transform", open && "rotate-180")}
        />
      </button>
      {open ? (
        <div
          role="menu"
          aria-label={`Feature actions for ${slug}`}
          className="absolute right-0 top-full z-30 mt-1 w-52 rounded-md border border-border-strong bg-surface-raised py-1 shadow-lg"
        >
          <a
            role="menuitem"
            href={href}
            onClick={() => setOpen(false)}
            className={itemClasses}
          >
            Open feature
          </a>
          <button type="button" role="menuitem" onClick={copyLink} className={itemClasses}>
            {copied ? "Copied" : "Copy link"}
          </button>
          <div className="mt-1 border-t border-border px-3 py-1.5 text-xs text-muted">
            {claimReadout}
          </div>
        </div>
      ) : null}
    </span>
  );
}
