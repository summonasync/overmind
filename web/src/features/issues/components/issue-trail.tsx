import { formatRelativeTime } from "@/shared/lib/relative-time";
import type { IssueAppendItem } from "@/features/issues/types";

interface IssueTrailProps {
  appends: IssueAppendItem[];
  authorNames: Record<string, string>;
}

export function IssueTrail({ appends, authorNames }: IssueTrailProps) {
  if (appends.length === 0) {
    return <p className="text-sm text-muted">No follow-ups yet.</p>;
  }

  return (
    <ol className="space-y-3">
      {appends.map((append) => (
        <li
          key={append.id}
          className="rounded-md border border-border bg-surface-raised px-3 py-2"
        >
          <p className="text-xs text-muted">
            {authorNames[append.authorProjectId] ?? append.authorProjectId.slice(0, 8)} ·{" "}
            {formatRelativeTime(append.createdAt)}
          </p>
          <p className="mt-1 whitespace-pre-wrap text-sm text-foreground">{append.text}</p>
        </li>
      ))}
    </ol>
  );
}
