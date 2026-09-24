"use client";

import type { KeyboardEvent } from "react";
import { Search } from "lucide-react";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";

interface BoardSearchInputProps {
  value: string;
  applied: boolean;
  onChange: (value: string) => void;
  onApply: (value: string) => void;
  onClear: () => void;
  onClearAll: () => void;
}

export function BoardSearchInput({
  value,
  applied,
  onChange,
  onApply,
  onClear,
  onClearAll,
}: BoardSearchInputProps) {
  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key !== "Escape") {
      return;
    }
    event.preventDefault();
    if (event.shiftKey) {
      onClearAll();
    } else {
      onClear();
    }
  };

  return (
    <form
      role="search"
      onSubmit={(event) => {
        event.preventDefault();
        onApply(value);
      }}
      className="flex items-center gap-1.5"
    >
      <span className="relative">
        <Search
          size={12}
          aria-hidden
          className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 text-muted"
        />
        <input
          type="text"
          value={value}
          placeholder="Search number, slug or title"
          aria-label="Search features"
          onChange={(event) => onChange(event.target.value)}
          onKeyDown={handleKeyDown}
          className={cn(
            "w-56 rounded-md border bg-surface py-1 pl-7 pr-2.5 text-xs text-foreground",
            "placeholder:text-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
            applied
              ? "border-primary font-medium"
              : "border-border hover:border-border-strong",
          )}
        />
      </span>
      <Button type="submit" variant="secondary" className="px-2.5 py-1 text-xs">
        <Search size={12} aria-hidden />
        Search
      </Button>
    </form>
  );
}
