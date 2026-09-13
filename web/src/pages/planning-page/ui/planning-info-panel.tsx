"use client";

import { ListChecks } from "lucide-react";
import { PLANNING_FEATURE_STATUSES, usePlanningBoard } from "@/features/planning";

interface PlanningInfoPanelProps {
  projectId: string;
}

export function PlanningInfoPanel({ projectId }: PlanningInfoPanelProps) {
  const { data: features, loading } = usePlanningBoard(projectId);

  if (loading) {
    return null;
  }

  const tasksDone = features.reduce((sum, item) => sum + item.taskDone, 0);
  const tasksTotal = features.reduce((sum, item) => sum + item.taskTotal, 0);
  const validatedSpecs = features.filter((item) => item.specStatus === "validated").length;
  const validatedPlans = features.filter((item) => item.planStatus === "validated").length;

  return (
    <div className="space-y-6">
      <section>
        <h2 className="mb-3 flex items-center gap-2 text-sm font-semibold text-foreground">
          <ListChecks size={15} aria-hidden className="text-primary" />
          Planning Overview
        </h2>
        <div className="grid grid-cols-2 gap-2">
          <div className="rounded-lg border border-border bg-surface p-3">
            <p className="text-2xl font-semibold text-foreground">{features.length}</p>
            <p className="text-xs text-muted">Features</p>
          </div>
          <div className="rounded-lg border border-border bg-surface p-3">
            <p className="text-2xl font-semibold text-foreground">
              {tasksDone}/{tasksTotal}
            </p>
            <p className="text-xs text-muted">Tasks done</p>
          </div>
          <div className="rounded-lg border border-border bg-surface p-3">
            <p className="text-2xl font-semibold text-foreground">{validatedSpecs}</p>
            <p className="text-xs text-muted">Specs validated</p>
          </div>
          <div className="rounded-lg border border-border bg-surface p-3">
            <p className="text-2xl font-semibold text-foreground">{validatedPlans}</p>
            <p className="text-xs text-muted">Plans validated</p>
          </div>
        </div>
      </section>

      <section>
        <h2 className="mb-2 text-sm font-semibold text-foreground">Status breakdown</h2>
        <ul className="space-y-1.5">
          {PLANNING_FEATURE_STATUSES.map((status) => (
            <li key={status} className="flex items-center justify-between text-xs">
              <span className="capitalize text-muted">{status}</span>
              <span className="font-medium text-foreground">
                {features.filter((item) => item.status === status).length}
              </span>
            </li>
          ))}
        </ul>
      </section>

      <section>
        <h2 className="mb-2 text-sm font-semibold text-foreground">Ceremony</h2>
        <p className="text-xs leading-relaxed text-muted">
          Each feature carries spec, plan, task and result artifacts. Specs and plans must be
          validated before work continues; tasks move from todo to done through review.
        </p>
      </section>
    </div>
  );
}
