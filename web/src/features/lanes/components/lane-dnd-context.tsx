"use client";

import type { ReactNode } from "react";
import {
  DndContext,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
  type Announcements,
} from "@dnd-kit/core";
import { sortableKeyboardCoordinates } from "@dnd-kit/sortable";
import {
  describeLaneDropTarget,
  parseLaneDropTarget,
  parseLaneFeatureDrag,
} from "@/features/lanes/lib/lane-dnd-data";
import type {
  LaneDropTargetData,
  LaneFeatureDragData,
} from "@/features/lanes/lib/lane-dnd-data";

interface LaneDndProviderProps {
  children: ReactNode;
  onDrop?: (drag: LaneFeatureDragData, target: LaneDropTargetData | null) => void;
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
    const target = over === null ? null : parseLaneDropTarget(over.data.current);
    return target === null
      ? `Feature ${drag.slug} is not over a drop target.`
      : `Feature ${drag.slug} is over ${describeLaneDropTarget(target)}.`;
  },
  onDragEnd: ({ active, over }) => {
    const drag = parseLaneFeatureDrag(active.data.current);
    if (drag === null) {
      return "Drag finished.";
    }
    const target = over === null ? null : parseLaneDropTarget(over.data.current);
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

export function LaneDndProvider({ children, onDrop }: LaneDndProviderProps) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      accessibility={{ announcements, screenReaderInstructions: { draggable: LANE_DND_INSTRUCTIONS } }}
      onDragEnd={(event) => {
        const drag = parseLaneFeatureDrag(event.active.data.current);
        if (drag === null) {
          return;
        }
        const target = event.over === null ? null : parseLaneDropTarget(event.over.data.current);
        onDrop?.(drag, target);
      }}
    >
      {children}
    </DndContext>
  );
}
