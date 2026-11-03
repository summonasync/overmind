"use client";

import { useCallback, useRef, useState, type PointerEvent as ReactPointerEvent, type RefObject } from "react";
import { clampPanelPosition, readViewportSize } from "@/widgets/agent-chat-bubble/lib/panel-geometry";
import {
  commitPanelGeometry,
  ensurePanelGeometryFromRect,
  panelGeometrySnapshot,
  setPanelGeometry,
} from "@/widgets/agent-chat-bubble/lib/panel-geometry-store";

const DESKTOP_QUERY = "(min-width: 640px)";

export function usePanelDrag(panelRef: RefObject<HTMLDivElement | null>) {
  const [dragging, setDragging] = useState(false);
  const activeRef = useRef(false);

  const onPointerDown = useCallback(
    (event: ReactPointerEvent<HTMLElement>) => {
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
      if (event.target instanceof Element && event.target.closest("button") !== null) {
        return;
      }
      event.preventDefault();
      const startPoint = { x: event.clientX, y: event.clientY };
      const startGeometry = ensurePanelGeometryFromRect(panel.getBoundingClientRect());
      const viewport = readViewportSize();
      activeRef.current = true;
      setDragging(true);

      const handleMove = (moveEvent: PointerEvent) => {
        const position = clampPanelPosition(
          {
            x: startGeometry.x + (moveEvent.clientX - startPoint.x),
            y: startGeometry.y + (moveEvent.clientY - startPoint.y),
          },
          startGeometry,
          viewport,
        );
        setPanelGeometry({ ...startGeometry, ...position });
      };
      const handleFinish = () => {
        activeRef.current = false;
        setDragging(false);
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
    [panelRef],
  );

  return { dragging, onPointerDown };
}
