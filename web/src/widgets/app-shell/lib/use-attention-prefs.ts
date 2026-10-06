"use client";

import { useCallback, useSyncExternalStore } from "react";
import {
  attentionPrefsSnapshot,
  subscribeAttentionPrefs,
  writeAttentionPrefs,
  type AttentionPrefs,
} from "@/widgets/app-shell/lib/attention-prefs";

export function useAttentionPrefs(): {
  prefs: AttentionPrefs;
  update: (next: AttentionPrefs) => void;
} {
  const prefs = useSyncExternalStore(
    subscribeAttentionPrefs,
    attentionPrefsSnapshot,
    attentionPrefsSnapshot,
  );
  const update = useCallback((next: AttentionPrefs) => {
    writeAttentionPrefs(next);
  }, []);
  return { prefs, update };
}
