"use client";

import { ListChecks } from "lucide-react";
import { InfoPanelSection, InfoPanelStat } from "@/widgets/app-shell";
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
      <InfoPanelSection title="Dashboard Overview" icon={ListChecks}>
        <div className="grid grid-cols-2 gap-2">
          <InfoPanelStat value={features.length} label="Features" />
          <InfoPanelStat value={`${tasksDone}/${tasksTotal}`} label="Tasks done" />
          <InfoPanelStat value={validatedSpecs} label="Specs validated" />
          <InfoPanelStat value={validatedPlans} label="Plans validated" />
        </div>
      </InfoPanelSection>

      <InfoPanelSection title="Status breakdown">
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
      </InfoPanelSection>

      <InfoPanelSection title="Ceremony">
        <p className="text-xs leading-relaxed text-muted">
          Each feature carries spec, plan, task and result artifacts. Specs and plans must be
          validated before work continues; tasks move from todo to done through review.
        </p>
      </InfoPanelSection>
    </div>
  );
}
