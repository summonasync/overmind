"use client";

import { useEffect, useRef } from "react";
import {
  createBellAutoRead,
  type BellAutoRead,
} from "@/features/notifications/lib/bell-auto-read";

export function useBellAutoRead(
  open: boolean,
  ids: readonly string[],
  read: (id: string) => void,
): void {
  const controllerRef = useRef<BellAutoRead | null>(null);
  const armedRef = useRef<Set<string>>(new Set());
  const readRef = useRef(read);

  useEffect(() => {
    readRef.current = read;
  }, [read]);

  useEffect(() => {
    if (!open) {
      controllerRef.current?.cancel();
      controllerRef.current = null;
      armedRef.current = new Set();
      return;
    }
    controllerRef.current ??= createBellAutoRead((id) => readRef.current(id));
    for (const id of ids) {
      if (armedRef.current.has(id)) {
        continue;
      }
      armedRef.current.add(id);
      controllerRef.current.arm(id);
    }
  }, [open, ids]);

  useEffect(
    () => () => {
      controllerRef.current?.cancel();
      controllerRef.current = null;
    },
    [],
  );
}
