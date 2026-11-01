import { readJson, writeJson } from "../../../shared/lib/session-json.ts";

export const MARSHAL_HINT_DISMISSED_KEY = "overmind.hints.marshalDismissed";
const DISMISS_EVENT = "overmind:marshal-hint-dismissed";

let cache: boolean | null = null;

export function marshalHintDismissed(): boolean {
  if (cache === null) {
    cache = readJson<boolean>(MARSHAL_HINT_DISMISSED_KEY, false) === true;
  }
  return cache;
}

export function dismissMarshalHint(): void {
  if (typeof window === "undefined") {
    return;
  }

  writeJson(MARSHAL_HINT_DISMISSED_KEY, true);
  cache = true;
  window.dispatchEvent(new Event(DISMISS_EVENT));
}

export function subscribeMarshalHint(listener: () => void): () => void {
  if (typeof window === "undefined") {
    return () => undefined;
  }

  const notify = () => {
    cache = null;
    listener();
  };
  const onStorage = (event: StorageEvent) => {
    if (event.key === null || event.key === MARSHAL_HINT_DISMISSED_KEY) {
      notify();
    }
  };

  window.addEventListener(DISMISS_EVENT, notify);
  window.addEventListener("storage", onStorage);
  return () => {
    window.removeEventListener(DISMISS_EVENT, notify);
    window.removeEventListener("storage", onStorage);
  };
}
