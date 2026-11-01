"use client";

import type { CSSProperties, ReactNode } from "react";
import { useSortable } from "@dnd-kit/sortable";
import { cn } from "@/shared/lib/cn";
import {
  laneDragTransform,
  laneFeatureDragData,
  laneFeatureDragId,
} from "@/features/lanes/lib/lane-dnd-data";

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
  const { attributes, isDragging, isOver, listeners, setNodeRef, setActivatorNodeRef, transform, transition } =
    useSortable({
      id: laneFeatureDragId(slug),
      data: laneFeatureDragData(slug, laneName),
      disabled,
    });
  const style: CSSProperties | undefined =
    transform === null ? undefined : { transform: laneDragTransform(transform), transition };

  return (
    <div
      ref={(element) => {
        setNodeRef(element);
        setActivatorNodeRef(element);
      }}
      {...listeners}
      {...attributes}
      style={style}
      className={cn(disabled ? undefined : "cursor-grab active:cursor-grabbing", className)}
    >
      {children({ isDragging, isOver })}
    </div>
  );
}
