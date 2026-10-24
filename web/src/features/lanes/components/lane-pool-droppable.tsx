"use client";

import type { ReactNode } from "react";
import { useDroppable } from "@dnd-kit/core";
import { POOL_DROPPABLE_ID, poolDroppableData } from "@/features/lanes/lib/lane-dnd-data";

interface LanePoolDroppableState {
  isDropActive: boolean;
}

interface LanePoolDroppableProps {
  className?: string;
  children: (state: LanePoolDroppableState) => ReactNode;
}

export function LanePoolDroppable({ className, children }: LanePoolDroppableProps) {
  const { isOver, setNodeRef } = useDroppable({
    id: POOL_DROPPABLE_ID,
    data: poolDroppableData(),
  });

  return (
    <div ref={setNodeRef} className={className}>
      {children({ isDropActive: isOver })}
    </div>
  );
}
