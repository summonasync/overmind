"use client";

import { useEffect, useRef, useState } from "react";
import { Search } from "lucide-react";
import avatar from "@/shared/assets/brand/kerrigan-avatar.png";
import { cn } from "@/shared/lib/cn";
import { StatusChip } from "@/widgets/app-shell/ui/status-chip";

export function AppTopbar() {
  const searchRef = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState("");

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        searchRef.current?.focus();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, []);

  return (
    <header className="sticky top-0 z-20 flex h-14 items-center gap-4 border-b border-border/60 bg-background/80 px-4 backdrop-blur">
      <div
        className={cn(
          "flex h-9 w-full max-w-md items-center gap-2 rounded-md border border-border bg-surface px-3 text-sm text-muted",
          "transition-colors hover:border-border-strong",
          "focus-within:border-border-strong focus-within:ring-2 focus-within:ring-primary/40 focus-within:ring-offset-2 focus-within:ring-offset-background",
        )}
      >
        <Search size={14} aria-hidden />
        <input
          ref={searchRef}
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              window.location.assign("/search");
            }
          }}
          placeholder="Search projects, entries, kits…"
          aria-label="Global search"
          className="min-w-0 flex-1 bg-transparent text-foreground outline-none placeholder:text-muted"
        />
        <kbd className="rounded border border-border bg-surface-raised px-1.5 font-mono text-[10px] text-muted">⌘K</kbd>
      </div>
      <div className="ml-auto flex shrink-0 items-center gap-3">
        <StatusChip />
        <div className="flex items-center gap-2 rounded-full border border-border bg-surface py-1 pl-1 pr-3">
          <img src={avatar.src} alt="Kerrigan avatar" className="h-7 w-7 rounded-full border border-primary/40 object-cover" />
          <div className="leading-tight">
            <p className="text-xs font-medium text-foreground">Humanity Evolves</p>
            <p className="text-[10px] text-muted">Overmind</p>
          </div>
        </div>
      </div>
    </header>
  );
}
