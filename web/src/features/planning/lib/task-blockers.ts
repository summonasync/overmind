import { taskHeading } from "@/features/planning/lib/task-heading";
import type { PlanningArtifactResponse, PlanningArtifactStatus } from "@/features/planning/types";

export interface TaskBlockerRef {
  order: number;
  title: string;
  closed: boolean;
}

export function taskIsClosed(status: PlanningArtifactStatus): boolean {
  return status === "done" || status === "review";
}

export function taskBlockers(
  task: PlanningArtifactResponse,
  siblings: PlanningArtifactResponse[],
): TaskBlockerRef[] {
  if (task.blockedBy === undefined || task.blockedBy.length === 0) {
    return [];
  }

  const byOrder = new Map(siblings.map((item) => [item.order, item]));
  return task.blockedBy.flatMap((order) => {
    const blocker = byOrder.get(order);
    if (blocker === undefined) {
      return [];
    }
    return [
      {
        order: blocker.order,
        title: taskHeading(blocker.body, blocker.order),
        closed: taskIsClosed(blocker.status),
      },
    ];
  });
}

export function taskBlockerLabel(blocker: TaskBlockerRef): string {
  return `#${blocker.order} ${blocker.title}`;
}

export function taskBlockersSummary(blockers: TaskBlockerRef[]): string {
  return blockers.map(taskBlockerLabel).join(", ");
}
