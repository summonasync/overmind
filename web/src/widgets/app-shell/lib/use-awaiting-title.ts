"use client";

import { useEffect, useRef } from "react";
import { awaitingTitle } from "@/widgets/app-shell/lib/awaiting-title";

export function useAwaitingTitle(count: number) {
  const baseTitleRef = useRef<string | null>(null);

  useEffect(() => {
    if (baseTitleRef.current === null) {
      baseTitleRef.current = document.title;
    }
    document.title = awaitingTitle(baseTitleRef.current, count);
    return () => {
      if (baseTitleRef.current !== null) {
        document.title = baseTitleRef.current;
      }
    };
  }, [count]);
}
