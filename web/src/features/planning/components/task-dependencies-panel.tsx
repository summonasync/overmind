import { cn } from "@/shared/lib/cn";
import { Badge } from "@/shared/ui/badge";
import { FeatureNumber } from "@/features/planning/components/feature-number";
import {
  taskBlockerLabel,
  taskBlockers,
  type TaskBlockerRef,
} from "@/features/planning/lib/task-blockers";
import { taskHeading } from "@/features/planning/lib/task-heading";
import type { PlanningArtifactResponse } from "@/features/planning/types";

interface TaskDependenciesPanelProps {
  featureSlug: string;
  tasks: PlanningArtifactResponse[];
}

export function TaskDependenciesPanel({ featureSlug, tasks }: TaskDependenciesPanelProps) {
  const edges = tasks
    .map((task) => ({ task, blockers: taskBlockers(task, tasks) }))
    .filter((edge) => edge.blockers.length > 0);

  if (edges.length === 0) {
    return null;
  }

  const openCount = edges.reduce(
    (total, edge) => total + edge.blockers.filter((blocker) => !blocker.closed).length,
    0,
  );

  return (
    <section
      className="space-y-2 rounded-lg border border-border bg-surface p-4"
      aria-label="Task dependencies"
    >
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted">Task dependencies</h3>
        {openCount > 0 ? <Badge tone="danger">{openCount} open</Badge> : null}
      </div>
      <ul className="space-y-1.5">
        {edges.map(({ task, blockers }) => (
          <li key={task.id} className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
            <span className="font-medium text-foreground">
              <FeatureNumber slug={featureSlug} />#{task.order} {taskHeading(task.body, task.order)}
            </span>
            <span aria-hidden className="text-muted">
              →
            </span>
            {blockers.map((blocker) => (
              <BlockerRefChip key={blocker.order} blocker={blocker} />
            ))}
          </li>
        ))}
      </ul>
    </section>
  );
}

function BlockerRefChip({ blocker }: { blocker: TaskBlockerRef }) {
  return (
    <span
      title={`${blocker.closed ? "Closed" : "Open"} blocker — ${taskBlockerLabel(blocker)}`}
      className={cn(
        "rounded-md border px-2 py-0.5 font-mono text-xs font-medium",
        blocker.closed
          ? "border-border/50 text-muted/70 line-through"
          : "border-danger/40 bg-danger/10 text-danger",
      )}
    >
      {taskBlockerLabel(blocker)}
    </span>
  );
}
