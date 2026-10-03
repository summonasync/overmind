import { cn } from "@/shared/lib/cn";
import { issueStatusTone } from "@/features/issues/lib/status-tone";
import type { IssueStatus } from "@/features/issues/types";

interface IssueStatusChipProps {
  status: IssueStatus;
}

export function IssueStatusChip({ status }: IssueStatusChipProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded px-1.5 py-0.5 text-xs font-medium border",
        issueStatusTone(status),
      )}
    >
      {status}
    </span>
  );
}
