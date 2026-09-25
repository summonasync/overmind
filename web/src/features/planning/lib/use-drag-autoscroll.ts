"use client";

import { useCallback, useEffect, useRef } from "react";
import type { DragEvent, RefObject } from "react";

const EDGE_ZONE_PX = 72;
const MAX_SPEED_PX_PER_FRAME = 16;

interface ScrollTarget {
  top: number;
  bottom: number;
  scrollBy(deltaY: number): void;
}

function isScrollableElement(element: Element): boolean {
  const style = window.getComputedStyle(element);
  return /(auto|scroll|overlay)/.test(style.overflowY) && element.scrollHeight > element.clientHeight;
}

function nearestScrollableAncestor(start: Element): Element | null {
  let node: Element | null = start;
  while (node !== null) {
    if (isScrollableElement(node)) {
      return node;
    }
    node = node.parentElement;
  }
  return null;
}

function resolveScrollTarget(container: Element): ScrollTarget | null {
  const element = nearestScrollableAncestor(container);
  if (element !== null) {
    const rect = element.getBoundingClientRect();
    return {
      top: rect.top,
      bottom: rect.bottom,
      scrollBy: (deltaY) => element.scrollBy({ top: deltaY }),
    };
  }
  if (document.documentElement.scrollHeight > window.innerHeight) {
    return {
      top: 0,
      bottom: window.innerHeight,
      scrollBy: (deltaY) => window.scrollBy({ top: deltaY }),
    };
  }
  return null;
}

function runTick(
  containerRef: RefObject<HTMLDivElement | null>,
  pointerYRef: RefObject<number | null>,
  frameRef: RefObject<number | null>,
): void {
  frameRef.current = null;
  const container = containerRef.current;
  const pointerY = pointerYRef.current;
  if (container === null || pointerY === null) {
    return;
  }
  const target = resolveScrollTarget(container);
  if (target === null) {
    return;
  }
  const fromTop = pointerY - target.top;
  const fromBottom = target.bottom - pointerY;
  let deltaY = 0;
  if (fromTop >= 0 && fromTop < EDGE_ZONE_PX) {
    deltaY = -Math.ceil((MAX_SPEED_PX_PER_FRAME * (EDGE_ZONE_PX - fromTop)) / EDGE_ZONE_PX);
  } else if (fromBottom >= 0 && fromBottom < EDGE_ZONE_PX) {
    deltaY = Math.ceil((MAX_SPEED_PX_PER_FRAME * (EDGE_ZONE_PX - fromBottom)) / EDGE_ZONE_PX);
  }
  if (deltaY !== 0) {
    target.scrollBy(deltaY);
    frameRef.current = requestAnimationFrame(() => runTick(containerRef, pointerYRef, frameRef));
  }
}

interface DragAutoscrollOptions {
  active: boolean;
  containerRef: RefObject<HTMLDivElement | null>;
}

export function useDragAutoscroll({ active, containerRef }: DragAutoscrollOptions) {
  const pointerYRef = useRef<number | null>(null);
  const frameRef = useRef<number | null>(null);

  const stop = useCallback(() => {
    if (frameRef.current !== null) {
      cancelAnimationFrame(frameRef.current);
      frameRef.current = null;
    }
    pointerYRef.current = null;
  }, []);

  const onDragOver = useCallback(
    (event: DragEvent<HTMLElement>) => {
      pointerYRef.current = event.clientY;
      if (active && frameRef.current === null) {
        frameRef.current = requestAnimationFrame(() => runTick(containerRef, pointerYRef, frameRef));
      }
    },
    [active, containerRef],
  );

  const onDragLeave = useCallback(
    (event: DragEvent<HTMLElement>) => {
      const related = event.relatedTarget as Node | null;
      if (related === null || containerRef.current?.contains(related) !== true) {
        stop();
      }
    },
    [containerRef, stop],
  );

  useEffect(() => {
    if (!active) {
      stop();
    }
    return stop;
  }, [active, stop]);

  return { onDragOver, onDragLeave };
}
