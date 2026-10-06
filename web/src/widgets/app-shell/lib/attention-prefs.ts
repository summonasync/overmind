"use client";

export interface AttentionPrefs {
  desktop: boolean;
  chirp: boolean;
}

export const DEFAULT_ATTENTION_PREFS: AttentionPrefs = { desktop: false, chirp: true };

const DESKTOP_KEY = "overmind.attention.desktop";
const CHIRP_KEY = "overmind.attention.chirp";
const PREFS_EVENT = "overmind:attention-prefs";

let cache: AttentionPrefs | null = null;

function readFlag(key: string): boolean | null {
  if (typeof window === "undefined") {
    return null;
  }
  let raw: string | null;
  try {
    raw = window.localStorage.getItem(key);
  } catch {
    return null;
  }
  return raw === "1" ? true : raw === "0" ? false : null;
}

function readPrefs(): AttentionPrefs {
  return {
    desktop: readFlag(DESKTOP_KEY) ?? DEFAULT_ATTENTION_PREFS.desktop,
    chirp: readFlag(CHIRP_KEY) ?? DEFAULT_ATTENTION_PREFS.chirp,
  };
}

export function attentionPrefsSnapshot(): AttentionPrefs {
  if (cache === null) {
    cache = readPrefs();
  }
  return cache;
}

export function subscribeAttentionPrefs(listener: () => void): () => void {
  if (typeof window === "undefined") {
    return () => undefined;
  }
  const notify = () => {
    cache = null;
    listener();
  };
  const onStorage = (event: StorageEvent) => {
    if (event.key === null || event.key === DESKTOP_KEY || event.key === CHIRP_KEY) {
      notify();
    }
  };
  window.addEventListener(PREFS_EVENT, notify);
  window.addEventListener("storage", onStorage);
  return () => {
    window.removeEventListener(PREFS_EVENT, notify);
    window.removeEventListener("storage", onStorage);
  };
}

export function writeAttentionPrefs(prefs: AttentionPrefs): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.localStorage.setItem(DESKTOP_KEY, prefs.desktop ? "1" : "0");
    window.localStorage.setItem(CHIRP_KEY, prefs.chirp ? "1" : "0");
  } catch {
    return;
  }
  cache = null;
  window.dispatchEvent(new Event(PREFS_EVENT));
}
