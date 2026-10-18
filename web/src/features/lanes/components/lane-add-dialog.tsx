"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/shared/ui/button";
import { cn } from "@/shared/lib/cn";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { Modal } from "@/shared/ui/modal";
import { FEATURE_PRIORITIES } from "@/features/planning";
import type { PlanningFeaturePriority } from "@/features/planning";
import { lanePriorityToValue } from "@/features/lanes/lib/lane-priority";
import { useLaneMutations } from "@/features/lanes/lib/use-lane-mutations";
import { validateLaneName } from "@/features/lanes/lib/lane-roster";
import type { LaneRow } from "@/features/lanes/types";

type LanePriorityChoice = PlanningFeaturePriority | "none";

const PRIORITY_CHOICES: readonly LanePriorityChoice[] = ["none", ...FEATURE_PRIORITIES];

interface LaneAddDialogProps {
  projectId: string;
  roster: LaneRow[];
  onClose: () => void;
}

export function LaneAddDialog({ projectId, roster, onClose }: LaneAddDialogProps) {
  const [name, setName] = useState("");
  const [contour, setContour] = useState("");
  const [priorityChoice, setPriorityChoice] = useState<LanePriorityChoice>("none");
  const { createLane } = useLaneMutations(projectId);

  const validationError = validateLaneName(name, roster);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (validationError !== null || createLane.isPending) {
      return;
    }
    createLane.mutate(
      {
        name,
        contour: contour.trim().length > 0 ? contour : null,
        priority: priorityChoice === "none" ? null : lanePriorityToValue(priorityChoice),
        roster,
      },
      { onSuccess: onClose },
    );
  }

  return (
    <Modal title="Add lane" open onClose={onClose}>
      <form onSubmit={handleSubmit} className="space-y-3">
        <Field label="Name" htmlFor="lane-name">
          <Input
            id="lane-name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="Lane name"
            maxLength={64}
          />
        </Field>
        <Field label="Contour (optional)" htmlFor="lane-contour">
          <Input
            id="lane-contour"
            value={contour}
            onChange={(event) => setContour(event.target.value)}
            placeholder="Files or area this lane owns"
          />
        </Field>
        <div>
          <span className="mb-1 block text-xs font-medium text-muted">Priority</span>
          <div className="flex flex-wrap gap-1" role="group" aria-label="Lane priority">
            {PRIORITY_CHOICES.map((choice) => {
              const selected = priorityChoice === choice;
              return (
                <button
                  key={choice}
                  type="button"
                  onClick={() => setPriorityChoice(choice)}
                  aria-pressed={selected}
                  className={cn(
                    "rounded-md border px-2.5 py-1 text-xs font-medium transition-colors",
                    "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
                    selected
                      ? "border-primary bg-primary/15 text-primary"
                      : "border-border text-muted hover:border-border-strong hover:text-foreground",
                  )}
                >
                  {choice === "none" ? "None" : choice.toUpperCase()}
                </button>
              );
            })}
          </div>
        </div>
        {validationError !== null ? (
          <p className="text-xs text-danger" role="alert">
            {validationError}
          </p>
        ) : (
          <ErrorText error={createLane.error} />
        )}
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={validationError !== null || createLane.isPending}>
            {createLane.isPending ? "Adding…" : "Add lane"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
