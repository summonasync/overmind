"use client";

import type { ReactNode } from "react";
import { useDraggable } from "@dnd-kit/core";
import { laneFeatureDragData, laneFeatureDragId } from "@/features/lanes/lib/lane-dnd-data";

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
  const { attributes, isDragging, listeners, setNodeRef } = useDraggable({
    id: laneFeatureDragId(slug),
    data: laneFeatureDragData(slug, fromLane),
    disabled,
  });

  return (
    <div ref={setNodeRef} {...listeners} {...attributes} className={className}>
      {children({ isDragging })}
    </div>
  );
}
