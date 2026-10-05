import { formatBacklogAge } from "@/shared/lib/relative-time";
import { issueHref } from "@/features/issues/lib/issue-href";
import { IssueStatusChip } from "@/features/issues/components/issue-status-chip";
import type { IssueRow } from "@/features/issues/types";

interface IssueListRowProps {
  projectId: string;
  issue: IssueRow;
  authorName: string;
}

function shortProjectId(id: string): string {
  return id.slice(0, 8);
}

export function IssueListRow({ projectId, issue, authorName }: IssueListRowProps) {
  return (
    <a
      href={issueHref(projectId, issue.id)}
      className="flex items-center gap-3 rounded-md border border-border bg-surface px-4 py-3 transition-colors hover:border-border-strong hover:bg-surface-raised focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
    >
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium text-foreground">{issue.title}</p>
        <p className="mt-0.5 text-xs text-muted">
          {authorName !== "" ? authorName : shortProjectId(issue.authorProjectId)} ·{" "}
          {formatBacklogAge(issue.createdAt)}
        </p>
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
