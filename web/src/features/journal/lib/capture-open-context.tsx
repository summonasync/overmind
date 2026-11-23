"use client";

import { createContext, useContext, type ReactNode } from "react";

const CaptureOpenContext = createContext<(() => void) | null>(null);

interface CaptureOpenProviderProps {
  onOpen: () => void;
  children: ReactNode;
}

export function CaptureOpenProvider({ onOpen, children }: CaptureOpenProviderProps) {
  return <CaptureOpenContext.Provider value={onOpen}>{children}</CaptureOpenContext.Provider>;
}

export function useOpenCapture() {
  const open = useContext(CaptureOpenContext);
  if (!open) {
    throw new Error("useOpenCapture must be used within CaptureOpenProvider");
  }
  return open;
}
