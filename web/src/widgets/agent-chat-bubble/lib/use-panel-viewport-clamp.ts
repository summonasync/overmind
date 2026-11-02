"use client";

import { useEffect } from "react";
import { clampPanelGeometryToViewport } from "@/widgets/agent-chat-bubble/lib/panel-geometry-store";

export function usePanelViewportClamp(): void {
  useEffect(() => {
    clampPanelGeometryToViewport();
    const handleResize = () => {
      clampPanelGeometryToViewport();
    };
    window.addEventListener("resize", handleResize);
    return () => {
      window.removeEventListener("resize", handleResize);
    };
  }, []);
}
