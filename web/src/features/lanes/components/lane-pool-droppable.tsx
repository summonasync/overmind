"use client";

import type { ReactNode } from "react";
import { useDroppable } from "@dnd-kit/core";
import { cn } from "@/shared/lib/cn";
import { POOL_DROPPABLE_ID, poolDroppableData } from "@/features/lanes/lib/lane-dnd-data";

interface LanePoolDroppableState {
  isDropActive: boolean;
}

interface LanePoolDroppableProps {
  className?: string;
  dropClassName?: string;
  disabled?: boolean;
  children: (state: LanePoolDroppableState) => ReactNode;
}

export function LanePoolDroppable({
  className,
  dropClassName,
  disabled = false,
  children,
}: LanePoolDroppableProps) {
  const { isOver, setNodeRef } = useDroppable({
    id: POOL_DROPPABLE_ID,
    data: poolDroppableData(),
    disabled,
  });
  const isDropActive = isOver && !disabled;

  return (
    <div ref={setNodeRef} className={cn(className, isDropActive && dropClassName)}>
      {children({ isDropActive })}
    </div>
  );
}
