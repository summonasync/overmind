"use client";

import type { CSSProperties, ReactNode } from "react";
import { useDraggable } from "@dnd-kit/core";
import {
  laneDragTransform,
  laneFeatureDragData,
  laneFeatureDragId,
} from "@/features/lanes/lib/lane-dnd-data";

interface LaneDraggableCardState {
  isDragging: boolean;
}

interface LaneDraggableCardProps {
  slug: string;
  fromLane: string | null;
  disabled?: boolean;
  className?: string;
  children: (state: LaneDraggableCardState) => ReactNode;
}

export function LaneDraggableCard({
  slug,
  fromLane,
  disabled = false,
  className,
  children,
}: LaneDraggableCardProps) {
  const { attributes, isDragging, listeners, setNodeRef, transform } = useDraggable({
    id: laneFeatureDragId(slug),
    data: laneFeatureDragData(slug, fromLane),
    disabled,
  });
  const style: CSSProperties | undefined = transform === null ? undefined : { transform: laneDragTransform(transform) };

  return (
    <div ref={setNodeRef} {...listeners} {...attributes} style={style} className={className}>
      {children({ isDragging })}
    </div>
  );
}
