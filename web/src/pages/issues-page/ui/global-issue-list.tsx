"use client";

import { ErrorText } from "@/shared/ui/error-text";
import { EmptyState } from "@/shared/ui/empty-state";
import { LoadingState } from "@/shared/ui/loading-state";
import { useCrossProjectIssues } from "@/features/issues";
import { GlobalIssueRow } from "@/pages/issues-page/ui/global-issue-row";

export function GlobalIssueList() {
  const { data: issues, loading, error } = useCrossProjectIssues();

  return (
    <>
      <ErrorText error={error} />
      {loading ? (
        <LoadingState label="Loading issues…" />
      ) : issues.length === 0 ? (
        <EmptyState
          title="No issues filed"
          hint={
            error
              ? undefined
              : "Agents file issues through the agent API — issues from every project land here."
          }
        />
      ) : (
        <div className="space-y-2">
          {issues.map((issue) => (
            <GlobalIssueRow key={issue.id} issue={issue} />
          ))}
        </div>
      )}
    </>
  );
}
