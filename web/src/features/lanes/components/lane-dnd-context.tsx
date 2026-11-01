"use client";

import { useState, type ReactNode } from "react";
import {
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
  type Announcements,
} from "@dnd-kit/core";
import { sortableKeyboardCoordinates } from "@dnd-kit/sortable";
import { LaneDragGhost } from "@/features/lanes/components/lane-drag-ghost";
import type { LaneDragGhostCard } from "@/features/lanes/components/lane-drag-ghost";
import {
  describeLaneDropTarget,
  laneDropTargetFromOver,
  parseLaneFeatureDrag,
} from "@/features/lanes/lib/lane-dnd-data";

import type {
  LaneDropTargetData,
  LaneFeatureDragData,
} from "@/features/lanes/lib/lane-dnd-data";

interface LaneDndProviderProps {
  children: ReactNode;
  onDrop?: (
    drag: LaneFeatureDragData,
    target: LaneDropTargetData | null,
    overId: unknown,
  ) => void;
  onDragChange?: (drag: LaneFeatureDragData | null) => void;
  resolveGhostCard?: (slug: string) => LaneDragGhostCard | null;
}

const LANE_DND_INSTRUCTIONS =
  "To pick up a feature card, focus it and press Space or Enter. While dragging, use the arrow keys to move it between lanes, press Space or Enter to drop it, or press Escape to cancel.";

const announcements: Announcements = {
  onDragStart: ({ active }) => {
    const drag = parseLaneFeatureDrag(active.data.current);
    if (drag === null) {
      return "Picked up a draggable item.";
    }
    const origin = drag.fromLane === null ? "the unassigned pool" : `lane ${drag.fromLane}`;
    return `Picked up feature ${drag.slug} from ${origin}.`;
  },
  onDragOver: ({ active, over }) => {
    const drag = parseLaneFeatureDrag(active.data.current);
    if (drag === null) {
      return "Dragging an item.";
    }
    const target = over === null ? null : laneDropTargetFromOver(over.data.current);
    return target === null
      ? `Feature ${drag.slug} is not over a drop target.`
      : `Feature ${drag.slug} is over ${describeLaneDropTarget(target)}.`;
  },
  onDragEnd: ({ active, over }) => {
    const drag = parseLaneFeatureDrag(active.data.current);
    if (drag === null) {
      return "Drag finished.";
    }
    const target = over === null ? null : laneDropTargetFromOver(over.data.current);
    return target === null
      ? `Feature ${drag.slug} was dropped outside any target and stays where it was.`
      : `Feature ${drag.slug} dropped onto ${describeLaneDropTarget(target)}.`;
  },
  onDragCancel: ({ active }) => {
    const drag = parseLaneFeatureDrag(active.data.current);
    return drag === null
      ? "Drag cancelled."
      : `Drag cancelled, feature ${drag.slug} stays where it was.`;
  },
};

export function LaneDndProvider({
  children,
  onDrop,
  onDragChange,
  resolveGhostCard,
}: LaneDndProviderProps) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );
  const [dragActive, setDragActive] = useState(false);
  const [ghost, setGhost] = useState<LaneDragGhostCard | null>(null);

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      accessibility={{ announcements, screenReaderInstructions: { draggable: LANE_DND_INSTRUCTIONS } }}
      onDragStart={(event) => {
        const drag = parseLaneFeatureDrag(event.active.data.current);
        if (drag !== null) {
          setDragActive(true);
          setGhost(resolveGhostCard?.(drag.slug) ?? null);
          onDragChange?.(drag);
        }
      }}
      onDragEnd={(event) => {
        const drag = parseLaneFeatureDrag(event.active.data.current);
        setDragActive(false);
        setGhost(null);
        onDragChange?.(null);
        if (drag === null) {
          return;
        }
        const over = event.over;
        const target = over === null ? null : laneDropTargetFromOver(over.data.current);
        onDrop?.(drag, target, over?.id ?? null);
      }}
      onDragCancel={() => {
        setDragActive(false);
        setGhost(null);
        onDragChange?.(null);
      }}
    >
      <div className={dragActive ? "cursor-grabbing" : undefined}>{children}</div>
      <DragOverlay dropAnimation={null}>
        {ghost === null ? null : <LaneDragGhost card={ghost} />}
      </DragOverlay>
    </DndContext>
  );
}
