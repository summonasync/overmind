"use client";

import { CircleHelp, ClipboardList } from "lucide-react";
import { Markdown } from "@/shared/lib/markdown";
import { formatCalendarDate } from "@/shared/lib/relative-time";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { InfoPanelSection } from "@/widgets/app-shell";
import { FeatureClaimChip } from "@/features/planning/components/feature-claim-chip";
import { usePlanningMutations } from "@/features/planning/lib/use-planning";
import {
  artifactStatusTone,
  extractOpenQuestions,
  featureStatusTone,
  formatBacklogAge,
  formatRelativeTime,
  usePlanningBoard,
  usePlanningFeature,
  type PlanningArtifactResponse,
  type PlanningDocumentKind,
} from "@/features/planning";

interface FeatureOverviewPanelProps {
  projectId: string;
  slug: string;
}

export function FeatureOverviewPanel({ projectId, slug }: FeatureOverviewPanelProps) {
  const { data: feature } = usePlanningFeature(projectId, slug);
  const { data: board } = usePlanningBoard(projectId);
  const { claimFeature, unclaimFeature } = usePlanningMutations(projectId);

  if (feature === null) {
    return null;
  }

  const boardItem = board.find((item) => item.slug === slug);
  const tasks = feature.artifacts.filter((artifact) => artifact.kind === "task");
  const tasksDone = tasks.filter((task) => task.status === "done").length;
  const tasksTotal = tasks.length;
  const tasksPercent = tasksTotal > 0 ? Math.round((tasksDone / tasksTotal) * 100) : 0;
  const specArtifact = feature.artifacts.find((artifact) => artifact.kind === "spec") ?? null;
  const openQuestions = specArtifact === null ? null : extractOpenQuestions(specArtifact.body);

  return (
    <div className="space-y-6">
      <InfoPanelSection title="Feature Overview" icon={ClipboardList}>
        <div className="space-y-3">
          <p title={feature.title} className="truncate text-sm font-medium text-foreground">
            {feature.title}
          </p>

          <div className="flex flex-wrap items-center gap-1.5">
            <Badge tone={featureStatusTone(feature.status)}>{feature.status}</Badge>
            <Badge>v{feature.version}</Badge>
            {boardItem !== undefined ? <FeatureClaimChip item={boardItem} /> : null}
            {feature.isBlocked ? <Badge tone="danger">blocked</Badge> : null}
          </div>

          {boardItem !== undefined ? (
            <div className="flex items-center gap-1.5">
              <Button
                variant="ghost"
                className="px-2 py-1 text-xs"
                disabled={claimFeature.isPending || unclaimFeature.isPending}
                onClick={() =>
                  boardItem.claimedByAlias === null
                    ? claimFeature.mutate({ slug: boardItem.slug, alias: "board", vendor: "human" })
                    : unclaimFeature.mutate({ slug: boardItem.slug })
                }
              >
                {boardItem.claimedByAlias === null ? "Claim" : "Unclaim"}
              </Button>
              <ErrorText error={(claimFeature.error ?? unclaimFeature.error) as Error | null} />
            </div>
          ) : null}

          {tasksTotal > 0 ? (
            <div className="space-y-1">
              <div className="flex items-center justify-between text-xs text-muted">
                <span>
                  {tasksDone}/{tasksTotal} done
                </span>
                <span>{tasksPercent}%</span>
              </div>
              <div
                className="h-1 overflow-hidden rounded-full bg-surface-raised"
                role="progressbar"
                aria-valuenow={tasksPercent}
                aria-valuemin={0}
                aria-valuemax={100}
                aria-label={`Tasks done for ${feature.title}`}
              >
                <div className="h-full rounded-full bg-primary" style={{ width: `${tasksPercent}%` }} />
              </div>
            </div>
          ) : (
            <p className="text-xs text-muted">No tasks yet</p>
          )}

          <div className="flex flex-wrap gap-1.5">
            {documentChip("spec", specArtifact)}
            {documentChip(
              "plan",
              feature.artifacts.find((artifact) => artifact.kind === "plan") ?? null,
            )}
            {documentChip(
              "result",
              feature.artifacts.find((artifact) => artifact.kind === "result") ?? null,
            )}
          </div>

          <ul className="space-y-1.5 border-t border-border pt-2">
            <li className="flex items-center justify-between text-xs">
              <span className="text-muted">In backlog</span>
              <span
                className="font-medium text-foreground"
                title={formatCalendarDate(feature.createdAt)}
              >
                {formatBacklogAge(feature.createdAt)}
              </span>
            </li>
            <li className="flex items-center justify-between text-xs">
              <span className="text-muted">Updated</span>
              <span
                className="font-medium text-foreground"
                title={formatCalendarDate(feature.updatedAt)}
              >
                {formatRelativeTime(feature.updatedAt)}
              </span>
            </li>
          </ul>
        </div>
      </InfoPanelSection>

      <InfoPanelSection title="Open questions" icon={CircleHelp}>
        {specArtifact === null ? (
          <p className="text-xs text-muted">No spec yet</p>
        ) : openQuestions === null ? (
          <p className="text-xs text-muted">None</p>
        ) : (
          <Markdown content={openQuestions} />
        )}
      </InfoPanelSection>
    </div>
  );
}

function documentChip(label: PlanningDocumentKind, artifact: PlanningArtifactResponse | null) {
  const tone = artifact === null ? "neutral" : artifactStatusTone(artifact.status);
  return (
    <Badge tone={tone}>
      {label} {artifact === null ? "absent" : artifact.status}
    </Badge>
  );
}
