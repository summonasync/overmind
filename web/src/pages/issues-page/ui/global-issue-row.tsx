import { IssueStatusChip, issueHref } from "@/features/issues";
import type { CrossProjectIssueRow } from "@/features/issues";
import { ProjectNameBadge } from "@/features/projects";
import { formatBacklogAge } from "@/shared/lib/relative-time";

interface GlobalIssueRowProps {
  issue: CrossProjectIssueRow;
}

export function GlobalIssueRow({ issue }: GlobalIssueRowProps) {
  return (
    <a
      href={issueHref(issue.projectId, issue.id)}
      className="flex items-center gap-3 rounded-md border border-border bg-surface px-4 py-3 transition-colors hover:border-border-strong hover:bg-surface-raised focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
    >
      <ProjectNameBadge projectId={issue.projectId} className="shrink-0" />
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium text-foreground">{issue.title}</p>
        <p className="mt-0.5 text-xs text-muted">{formatBacklogAge(issue.createdAt)}</p>
      </div>
      {issue.appendCount > 0 ? (
        <span
          className="shrink-0 rounded border border-border bg-surface-raised px-1.5 py-0.5 text-xs text-muted"
          title={`${issue.appendCount} follow-up${issue.appendCount === 1 ? "" : "s"}`}
        >
          {issue.appendCount} {issue.appendCount === 1 ? "append" : "appends"}
        </span>
      ) : null}
      <IssueStatusChip status={issue.status} />
    </a>
  );
}
