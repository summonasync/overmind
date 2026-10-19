"use client";

import { useEffect, useState } from "react";
import { BookOpen, X } from "lucide-react";
import { showGrimoireBanner } from "@/features/grimoire/lib/grimoire-view";
import { useGrimoireStatus } from "@/features/grimoire";
import { dismissGrimoireBanner, isGrimoireBannerDismissed } from "../lib/banner-dismissal";

export function GrimoireBanner() {
  const { data: status, loading } = useGrimoireStatus();
  const [dismissed, setDismissed] = useState(false);

  useEffect(() => {
    const frame = requestAnimationFrame(() => setDismissed(isGrimoireBannerDismissed()));
    return () => cancelAnimationFrame(frame);
  }, []);

  const handleDismiss = () => {
    setDismissed(true);
    dismissGrimoireBanner();
  };

  if (loading || dismissed || !showGrimoireBanner(status)) {
    return null;
  }

  return (
    <div className="rounded-lg border border-primary/30 bg-primary/5 px-4 py-3" role="status">
      <div className="flex flex-wrap items-center justify-center gap-x-2.5 gap-y-1 text-sm text-foreground">
        <BookOpen size={16} className="text-primary" aria-hidden />
        <span>Grimoire is not wired yet — your agents have no personal-memory layer.</span>
        <a
          href="/settings"
          className="font-medium text-primary underline underline-offset-2 hover:text-primary/80"
        >
          Set it up
        </a>
        <button
          type="button"
          onClick={handleDismiss}
          aria-label="Dismiss Grimoire setup notice"
          className="rounded p-0.5 text-muted transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
        >
          <X size={14} aria-hidden />
        </button>
      </div>
    </div>
  );
}
