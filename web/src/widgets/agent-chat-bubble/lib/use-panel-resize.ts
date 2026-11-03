"use client";

import { useCallback, useRef, type PointerEvent as ReactPointerEvent, type RefObject } from "react";
import { clampPanelPosition, clampPanelSize, readViewportSize } from "@/widgets/agent-chat-bubble/lib/panel-geometry";
import {
  commitPanelGeometry,
  ensurePanelGeometryFromRect,
  panelGeometrySnapshot,
  setPanelGeometry,
} from "@/widgets/agent-chat-bubble/lib/panel-geometry-store";

const DESKTOP_QUERY = "(min-width: 640px)";

export type PanelResizeEdge = "left" | "bottom" | "corner";

export interface PanelResizeHandleProps {
  onPointerDown: (event: ReactPointerEvent<HTMLElement>) => void;
}

export function usePanelResize(panelRef: RefObject<HTMLDivElement | null>) {
  const activeRef = useRef(false);

  return useCallback(
    (edge: PanelResizeEdge): PanelResizeHandleProps => ({
      onPointerDown: (event) => {
        if (event.button !== 0 || activeRef.current) {
          return;
        }
        if (!window.matchMedia(DESKTOP_QUERY).matches) {
          return;
        }
        if (panelGeometrySnapshot().maximized) {
          return;
        }
        const panel = panelRef.current;
        if (panel === null) {
          return;
        }
        event.preventDefault();
        const startPoint = { x: event.clientX, y: event.clientY };
        const startGeometry = ensurePanelGeometryFromRect(panel.getBoundingClientRect());
        const viewport = readViewportSize();
        activeRef.current = true;

        const handleMove = (moveEvent: PointerEvent) => {
          const dx = moveEvent.clientX - startPoint.x;
          const dy = moveEvent.clientY - startPoint.y;
          const size = clampPanelSize(
            {
              width: edge === "bottom" ? startGeometry.width : startGeometry.width - dx,
              height: edge === "left" ? startGeometry.height : startGeometry.height + dy,
            },
            viewport,
          );
          const position = clampPanelPosition(
            { x: startGeometry.x + (startGeometry.width - size.width), y: startGeometry.y },
            size,
            viewport,
          );
          setPanelGeometry({ ...position, ...size });
        };
        const handleFinish = () => {
          activeRef.current = false;
          window.removeEventListener("pointermove", handleMove);
          window.removeEventListener("pointerup", handleFinish);
          window.removeEventListener("pointercancel", handleFinish);
          window.removeEventListener("keydown", handleKeyDown);
          commitPanelGeometry();
        };
        const handleKeyDown = (keyEvent: KeyboardEvent) => {
          if (keyEvent.key === "Escape") {
            handleFinish();
          }
        };
        window.addEventListener("pointermove", handleMove);
        window.addEventListener("pointerup", handleFinish);
        window.addEventListener("pointercancel", handleFinish);
        window.addEventListener("keydown", handleKeyDown);
      },
    }),
    [panelRef],
  );
}
