function sessionStore(): Storage | null {
  return typeof window === "undefined" ? null : window.sessionStorage;
}

export function readJson<T>(key: string, fallback: T): T {
  const store = sessionStore();
  if (store === null) {
    return fallback;
  }

  const raw = store.getItem(key);
  if (raw === null) {
    return fallback;
  }

  try {
    return JSON.parse(raw) as T;
  } catch {
    return fallback;
  }
}

export function writeJson(key: string, value: unknown): void {
  const store = sessionStore();
  if (store === null) {
    return;
  }

  store.setItem(key, JSON.stringify(value));
}
