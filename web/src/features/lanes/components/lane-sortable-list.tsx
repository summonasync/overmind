"use client";

import type { ReactNode } from "react";
import { useDroppable } from "@dnd-kit/core";
import { SortableContext, verticalListSortingStrategy } from "@dnd-kit/sortable";
import { laneDroppableData, laneDroppableId } from "@/features/lanes/lib/lane-dnd-data";

interface LaneSortableListState {
  isDropActive: boolean;
}

interface LaneSortableListProps {
  laneName: string;
  slugs: readonly string[];
  disabled?: boolean;
  className?: string;
  children: (state: LaneSortableListState) => ReactNode;
}

export function LaneSortableList({
  laneName,
  slugs,
  disabled = false,
  className,
  children,
}: LaneSortableListProps) {
  const { isOver, setNodeRef } = useDroppable({
    id: laneDroppableId(laneName),
    data: laneDroppableData(laneName),
    disabled,
  });
  const isDropActive = isOver && !disabled;

  return (
    <SortableContext items={[...slugs]} strategy={verticalListSortingStrategy}>
      <ul ref={setNodeRef} className={className}>
        {children({ isDropActive })}
      </ul>
    </SortableContext>
  );
}
