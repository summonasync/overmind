"use client";

import { IssueActions } from "@/features/issues/components/issue-actions";
import type { IssueActionPending } from "@/features/issues/components/issue-actions";
import { IssueKindBadge } from "@/features/issues/components/issue-kind-badge";
import { IssueStatusChip } from "@/features/issues/components/issue-status-chip";
import { IssueTrail } from "@/features/issues/components/issue-trail";
import { useIssueCard, useIssueMutations } from "@/features/issues/lib/use-issues";
import { ErrorText } from "@/shared/ui/error-text";
import { LoadingState } from "@/shared/ui/loading-state";

interface IssueDetailProps {
  projectId: string;
  issueId: string;
  features: { slug: string; title: string }[];
  authorNames: Record<string, string>;
}

export function IssueDetail({ projectId, issueId, features, authorNames }: IssueDetailProps) {
  const { data: issue, loading, error } = useIssueCard(projectId, issueId);
  const { triage, close, promote } = useIssueMutations(projectId);

  if (loading) {
    return <LoadingState label="Loading issue…" />;
  }

  if (error !== null) {
    return <ErrorText error={error} />;
  }

  if (issue === null) {
    return <p className="text-sm text-muted">Issue not found.</p>;
  }

  const pending: IssueActionPending = {
    triage: triage.isPending,
    close: close.isPending,
    promote: promote.isPending,
  };
  const actionError =
    (triage.error as Error | null) ??
    (close.error as Error | null) ??
    (promote.error as Error | null);

  return (
    <article className="space-y-4">
      <header className="space-y-1.5">
        <div className="flex flex-wrap items-center gap-2">
          <IssueKindBadge kind={issue.kind} />
          <IssueStatusChip status={issue.status} />
          <span className="text-xs text-muted">
            {authorNames[issue.authorProjectId] ?? issue.authorProjectId.slice(0, 8)}
          </span>
        </div>
        <h2 className="text-lg font-semibold text-foreground">{issue.title}</h2>
      </header>

      <section className="whitespace-pre-wrap rounded-lg border border-border bg-surface px-4 py-3 text-sm text-foreground">
        {issue.body}
      </section>

      <IssueActions
        issue={issue}
        features={features}
        pending={pending}
        error={actionError}
        handlers={{
          onTriage: (id) => triage.mutate(id),
          onClose: (input) => close.mutate(input),
          onPromote: (input) =>
            promote.mutate({
              issueId: input.issueId,
              values: { featureSlug: input.featureSlug },
            }),
        }}
      />

      <section className="space-y-2">
        <h3 className="text-sm font-semibold text-foreground">Follow-ups</h3>
        <IssueTrail appends={issue.appends} authorNames={authorNames} />
      </section>
    </article>
  );
}
