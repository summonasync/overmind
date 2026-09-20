"use client";

import { useState } from "react";
import { X } from "lucide-react";
import { ApiError } from "@/shared/api/client";
import { Zerling, ZERLING_SIZES } from "@/shared/ui/zerling";

interface SearchErrorProps {
  error: Error;
}

export function SearchError({ error }: SearchErrorProps) {
  const [dismissed, setDismissed] = useState(false);

  if (dismissed) {
    return null;
  }

  const embeddingSettingsFailure =
    error instanceof ApiError && error.status === 503 && error.message.includes("embedding settings");

  return (
    <div
      className="flex flex-wrap items-center gap-x-2.5 gap-y-1 rounded-md border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger"
      role="alert"
    >
      <Zerling pose="yelling" size={ZERLING_SIZES.banner} />
      <span>{error.message}</span>
      {embeddingSettingsFailure ? (
        <a href="/settings" className="font-medium underline underline-offset-2 hover:text-danger/80">
          Open settings
        </a>
      ) : null}
      <button
        type="button"
        onClick={() => setDismissed(true)}
        aria-label="Dismiss search error"
        className="rounded p-0.5 text-danger/70 transition-colors hover:text-danger focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
      >
        <X size={14} aria-hidden />
      </button>
    </div>
  );
}
