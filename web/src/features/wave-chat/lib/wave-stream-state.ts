"use client";

import { useSyncExternalStore } from "react";

export type WaveStreamStatus = "idle" | "open" | "reconnecting";

let status: WaveStreamStatus = "idle";
const listeners = new Set<() => void>();

function emit(): void {
  for (const listener of listeners) {
    listener();
  }
}

export function subscribeWaveStreamStatus(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function waveStreamStatusSnapshot(): WaveStreamStatus {
  return status;
}

export function useWaveStreamStatus(): WaveStreamStatus {
  return useSyncExternalStore(subscribeWaveStreamStatus, waveStreamStatusSnapshot, waveStreamStatusSnapshot);
}

export function setWaveStreamStatus(next: WaveStreamStatus): void {
  if (status === next) {
    return;
  }
  status = next;
  emit();
}
