"use client";

import { Suspense, useMemo, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Button } from "@/shared/ui/button";
import { EmptyState } from "@/shared/ui/empty-state";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";
import { PageHeader } from "@/shared/ui/page-header";
import { useProjectList } from "@/features/projects";
import { usePlanningBoard } from "@/features/planning";
import { IssueDetail, IssueFilters, IssueListRow, useIssueList } from "@/features/issues";
import type { IssueListFilter } from "@/features/issues";

const ISSUES_DESCRIPTION =
  "Cross-project bug intake. Agents file issues from any project; triage, append follow-ups, close, or promote them to a task on this board.";

function authorNameMap(projects: { id: string; name: string }[]): Record<string, string> {
  const map: Record<string, string> = {};
  for (const project of projects) {
    map[project.id] = project.name;
  }
  return map;
}

function IssuesPageContent() {
  const searchParams = useSearchParams();
  const projectId = searchParams?.get("project") ?? "";
  const issueId = searchParams?.get("issue") ?? "";

  const { data: projects } = useProjectList();
  const { data: features } = usePlanningBoard(projectId);

  const [filter, setFilter] = useState<IssueListFilter>({
    status: null,
    authorProjectId: null,
  });
  const { data: issues, loading, error } = useIssueList(projectId, filter);
  const names = useMemo(() => authorNameMap(projects), [projects]);

  const project = projects.find((item) => item.id === projectId) ?? null;

  if (!projectId) {
    return (
      <div className="space-y-4">
        <PageHeader title="Issues Board" description={ISSUES_DESCRIPTION} />
        <p className="text-sm text-muted">
          Select a project first.{" "}
          <a
            href="/"
            className="text-primary underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
          >
            Go to Projects
          </a>
        </p>
      </div>
    );
  }

  const pickerFeatures = features.map((feature) => ({
    slug: feature.slug,
    title: feature.title,
  }));

  return (
    <div className="space-y-4">
      <PageHeader
        title={project !== null ? `Issues Board — ${project.name}` : "Issues Board"}
        description={ISSUES_DESCRIPTION}
        actions={
          issueId ? (
            <a
              href={`/issues?project=${encodeURIComponent(projectId)}`}
              className="text-xs text-muted underline-offset-2 hover:text-foreground hover:underline"
            >
              ← All issues
            </a>
          ) : (
            <a
              href="/"
              className="text-xs text-muted underline-offset-2 hover:text-foreground hover:underline"
            >
              ← All projects
            </a>
          )
        }
      />

      {issueId ? (
        <IssueDetail
          projectId={projectId}
          issueId={issueId}
          features={pickerFeatures}
          authorNames={names}
        />
      ) : (
        <>
          <ErrorText error={error} />
          <IssueFilters
            filter={filter}
            projects={projects.map((item) => ({ id: item.id, name: item.name }))}
            onChange={setFilter}
          />
          {loading ? (
            <LoadingState label="Loading issues…" />
          ) : issues.length === 0 ? (
            <EmptyState
              title="No issues filed"
              hint={
                error
                  ? undefined
                  : "Agents file issues through the agent API — foreign-created ones land here with a notification."
              }
              action={
                filter.status !== null || filter.authorProjectId !== null ? (
                  <Button
                    variant="secondary"
                    onClick={() => setFilter({ status: null, authorProjectId: null })}
                  >
                    Clear filters
                  </Button>
                ) : undefined
              }
            />
          ) : (
            <div className="space-y-2">
              {issues.map((issue) => (
                <IssueListRow
                  key={issue.id}
                  projectId={projectId}
                  issue={issue}
                  authorName={names[issue.authorProjectId] ?? ""}
                />
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}

export function IssuesPage() {
  return (
    <Suspense fallback={<LoadingState label="Loading…" />}>
      <IssuesPageContent />
    </Suspense>
  );
}
