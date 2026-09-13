"use client";

import { Badge } from "@/shared/ui/badge";
import { EmptyState } from "@/shared/ui/empty-state";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import { Markdown } from "@/shared/lib/markdown";
import { usePlanningFeature } from "@/features/planning/lib/use-planning";
import { artifactStatusTone, featureStatusTone } from "@/features/planning/lib/status-tone";
import { formatRelativeTime } from "@/features/planning/lib/relative-time";
import type { PlanningArtifactResponse } from "@/features/planning/types";

interface FeatureWorkspaceProps {
  projectId: string;
  slug: string;
}

function ArtifactCard({ artifact }: { artifact: PlanningArtifactResponse }) {
  return (
    <li className="rounded-lg border border-border bg-surface p-4">
      <div className="mb-3 flex flex-wrap items-center gap-1.5">
        <Badge>{artifact.kind}</Badge>
        <Badge tone={artifactStatusTone(artifact.status)}>{artifact.status}</Badge>
        {artifact.kind === "task" ? <Badge>#{artifact.order}</Badge> : null}
        <Badge>{artifact.review}</Badge>
        {artifact.frozenAt !== null ? <Badge tone="warning">frozen</Badge> : null}
        <span className="ml-auto font-mono text-xs text-muted">v{artifact.version}</span>
      </div>
      <div className="max-h-96 overflow-y-auto text-sm">
        <Markdown content={artifact.body} />
      </div>
    </li>
  );
}

export function FeatureWorkspace({ projectId, slug }: FeatureWorkspaceProps) {
  const { data: feature, loading, error } = usePlanningFeature(projectId, slug);
  const boardHref = `/planning?project=${projectId}`;

  if (loading) {
    return <LoadingState label="Loading feature…" />;
  }

  if (feature === null) {
    return (
      <div className="space-y-4">
        <PageHeader
          title={slug}
          description="Read-only preview of the feature workspace."
          actions={
            <a
              href={boardHref}
              className="text-xs text-muted underline-offset-2 hover:text-foreground hover:underline"
            >
              ← Planning board
            </a>
          }
        />
        <ErrorText error={error ?? new Error(`Feature '${slug}' was not found.`)} />
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={feature.title}
        description={`${feature.slug} — read-only preview of the feature workspace.`}
        actions={
          <a
            href={boardHref}
            className="text-xs text-muted underline-offset-2 hover:text-foreground hover:underline"
          >
            ← Planning board
          </a>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <Badge tone={featureStatusTone(feature.status)}>{feature.status}</Badge>
        <Badge>v{feature.version}</Badge>
        <span className="text-xs text-muted">updated {formatRelativeTime(feature.updatedAt)}</span>
      </div>

      {feature.body.trim().length > 0 ? (
        <div className="rounded-lg border border-border bg-surface p-4">
          <Markdown content={feature.body} />
        </div>
      ) : null}

      {feature.artifacts.length === 0 ? (
        <EmptyState
          title="No artifacts yet"
          hint="Spec, plan, tasks and result artifacts created for this feature will appear here."
        />
      ) : (
        <ul className="space-y-3">
          {feature.artifacts.map((artifact) => (
            <ArtifactCard key={artifact.id} artifact={artifact} />
          ))}
        </ul>
      )}
    </div>
  );
}
