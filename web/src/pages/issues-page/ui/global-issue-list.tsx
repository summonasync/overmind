"use client";

import { useState } from "react";
import { ErrorText } from "@/shared/ui/error-text";
import { EmptyState } from "@/shared/ui/empty-state";
import { LoadingState } from "@/shared/ui/loading-state";
import { IssueKindFilter, useCrossProjectIssues } from "@/features/issues";
import type { IssueKind } from "@/features/issues";
import { GlobalIssueRow } from "@/pages/issues-page/ui/global-issue-row";

export function GlobalIssueList() {
  const [kind, setKind] = useState<IssueKind | null>(null);
  const { data: issues, loading, error } = useCrossProjectIssues(kind);

  return (
    <>
      <ErrorText error={error} />
      <IssueKindFilter kind={kind} onChange={setKind} />
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
