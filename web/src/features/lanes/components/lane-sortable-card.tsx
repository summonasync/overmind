"use client";

import type { ReactNode } from "react";
import { useSortable } from "@dnd-kit/sortable";
import { laneFeatureDragData, laneFeatureDragId } from "@/features/lanes/lib/lane-dnd-data";

interface LaneSortableCardState {
  isDragging: boolean;
  isOver: boolean;
}

interface LaneSortableCardProps {
  slug: string;
  laneName: string;
  disabled?: boolean;
  className?: string;
  children: (state: LaneSortableCardState) => ReactNode;
}

export function LaneSortableCard({
  slug,
  laneName,
  disabled = false,
  className,
  children,
}: LaneSortableCardProps) {
  const { attributes, isDragging, isOver, listeners, setNodeRef } = useSortable({
    id: laneFeatureDragId(slug),
    data: laneFeatureDragData(slug, laneName),
    disabled,
  });

  return (
    <div ref={setNodeRef} {...listeners} {...attributes} className={className}>
      {children({ isDragging, isOver })}
    </div>
  );
}
