"use client";

import { useEffect } from "react";
import { isCaptureHotkey, isQuickCaptureKey } from "@/features/journal/lib/capture";

interface CaptureHotkeysOptions {
  onOpen: () => void;
  pathname: string;
}

export function useCaptureHotkeys({ onOpen, pathname }: CaptureHotkeysOptions) {
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (!isCaptureHotkey(event) && !isQuickCaptureKey(event, pathname)) {
        return;
      }
      // Stacked dialogs are broken in this app: every Modal listens for Escape on
      // window, so one tap closes both. Opening capture never adds a second layer.
      if (document.querySelector('[role="dialog"]') !== null) {
        return;
      }
      event.preventDefault();
      onOpen();
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onOpen, pathname]);
}
