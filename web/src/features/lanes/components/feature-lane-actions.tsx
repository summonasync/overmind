"use client";

import { Unlink } from "lucide-react";
import { cn } from "@/shared/lib/cn";
import { ErrorText } from "@/shared/ui/error-text";
import { Select } from "@/shared/ui/select";
import { useLanes } from "@/features/lanes/lib/use-lanes";
import { UNASSIGNED_LANE, useFeatureLaneAssignment } from "@/features/lanes/lib/use-feature-lane";

interface FeatureLaneActionsProps {
  projectId: string;
  slug: string;
  currentLane: string | null;
}

const pickerClasses = cn(
  "min-w-0 max-w-44 truncate font-medium",
  "disabled:cursor-not-allowed disabled:opacity-50",
);

const removeClasses = cn(
  "shrink-0 rounded p-1 text-muted transition-colors",
  "hover:bg-danger/10 hover:text-danger",
  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
  "disabled:cursor-not-allowed disabled:opacity-50",
);

const hintClasses = cn("min-w-0 truncate text-xs text-muted");

export function FeatureLaneActions({ projectId, slug, currentLane }: FeatureLaneActionsProps) {
  const { data: roster } = useLanes(projectId);
  const assignment = useFeatureLaneAssignment(projectId);
  const lanes = roster === null ? [] : roster.lanes;
  const targets = lanes
    .map((lane) => lane.name)
    .filter((name) => name !== currentLane);

  if (roster === null && currentLane === null) {
    return null;
  }

  return (
    <div className="flex min-w-0 flex-wrap items-center gap-1">
      {targets.length > 0 ? (
        <Select
          size="sm"
          aria-label={`Move ${slug} to lane`}
          className={pickerClasses}
          value=""
          disabled={assignment.isPending}
          onChange={(event) => assignment.mutate({ slug, lane: event.target.value })}
        >
          <option value="" disabled>
            Move to lane
          </option>
          {targets.map((name) => (
            <option key={name} value={name}>
              {name}
            </option>
          ))}
        </Select>
      ) : roster !== null && lanes.length === 0 ? (
        <span className={hintClasses}>No lanes yet — add one to move features in</span>
      ) : null}

      {currentLane !== null ? (
        <button
          type="button"
          aria-label={`Remove ${slug} from ${currentLane}`}
          title="Remove from lane"
          disabled={assignment.isPending}
          onClick={() => assignment.mutate({ slug, lane: UNASSIGNED_LANE })}
          className={removeClasses}
        >
          <Unlink size={12} aria-hidden />
        </button>
      ) : null}

      <ErrorText error={assignment.error} />
    </div>
  );
}
