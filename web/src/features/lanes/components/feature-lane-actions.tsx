"use client";

import { useState, type FormEvent } from "react";
import { Plus, Unlink } from "lucide-react";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Input } from "@/shared/ui/input";
import { Select } from "@/shared/ui/select";
import { useLanes } from "@/features/lanes/lib/use-lanes";
import { UNASSIGNED_LANE, useFeatureLaneAssignment } from "@/features/lanes/lib/use-feature-lane";
import { LANE_NAME_MAX_LENGTH, validateLaneName } from "@/features/lanes/lib/lane-roster";
import type { LaneRow } from "@/features/lanes/types";

interface FeatureLaneActionsProps {
  projectId: string;
  slug: string;
  currentLane: string | null;
}

interface NewLaneFormProps {
  lanes: LaneRow[];
  pending: boolean;
  onSubmit: (name: string) => void;
  onCancel: () => void;
}

const CREATE_LANE_OPTION = "/create-new-lane";

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

export function FeatureLaneActions({ projectId, slug, currentLane }: FeatureLaneActionsProps) {
  const { data: roster } = useLanes(projectId);
  const assignment = useFeatureLaneAssignment(projectId);
  const [creating, setCreating] = useState(false);

  if (roster === null && currentLane === null) {
    return null;
  }

  const lanes = roster?.lanes ?? [];
  const targets = lanes.map((lane) => lane.name).filter((name) => name !== currentLane);

  return (
    <div className="flex min-w-0 flex-wrap items-center gap-1">
      {roster !== null ? (
        creating ? (
          <NewLaneForm
            lanes={lanes}
            pending={assignment.isPending}
            onSubmit={(name) =>
              assignment.mutate({ slug, lane: name }, { onSuccess: () => setCreating(false) })
            }
            onCancel={() => setCreating(false)}
          />
        ) : (
          <Select
            size="sm"
            aria-label={`Move ${slug} to lane`}
            className={pickerClasses}
            value=""
            disabled={assignment.isPending}
            onChange={(event) => {
              if (event.target.value === CREATE_LANE_OPTION) {
                setCreating(true);
                return;
              }
              assignment.mutate({ slug, lane: event.target.value });
            }}
          >
            <option value="" disabled>
              Move to lane
            </option>
            {targets.map((name) => (
              <option key={name} value={name}>
                {name}
              </option>
            ))}
            <option value={CREATE_LANE_OPTION}>Create new lane…</option>
          </Select>
        )
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

function NewLaneForm({ lanes, pending, onSubmit, onCancel }: NewLaneFormProps) {
  const [name, setName] = useState("");
  const validationError = validateLaneName(name, lanes);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (validationError !== null) {
      return;
    }
    onSubmit(name.trim());
  }

  return (
    <form onSubmit={handleSubmit} className="flex min-w-0 flex-wrap items-center gap-1">
      <Input
        aria-label="New lane name"
        value={name}
        onChange={(event) => setName(event.target.value)}
        placeholder="New lane name"
        maxLength={LANE_NAME_MAX_LENGTH}
        autoFocus
        disabled={pending}
        className="w-40 px-1.5 py-0.5 text-xs"
      />
      <Button
        type="submit"
        variant="accent"
        disabled={validationError !== null || pending}
        className="px-2 py-0.5 text-xs"
      >
        <Plus size={12} aria-hidden />
        Add
      </Button>
      <Button variant="ghost" onClick={onCancel} className="px-2 py-0.5 text-xs">
        Cancel
      </Button>
      {validationError !== null ? (
        <span className="w-full text-xs text-danger" role="alert">
          {validationError}
        </span>
      ) : null}
    </form>
  );
}
