import { useSyncExternalStore } from "react";

let open = false;
const listeners = new Set<() => void>();

function emit(): void {
  for (const listener of listeners) {
    listener();
  }
}

export function subscribeBubbleOpen(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function bubbleOpenSnapshot(): boolean {
  return open;
}

export function useBubbleOpen(): boolean {
  return useSyncExternalStore(subscribeBubbleOpen, bubbleOpenSnapshot, bubbleOpenSnapshot);
}

export function setBubbleOpen(next: boolean): void {
  if (open === next) {
    return;
  }
  open = next;
  emit();
}

export function toggleBubbleOpen(): void {
  setBubbleOpen(!open);
}
