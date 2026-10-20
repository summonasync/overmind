"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { Modal } from "@/shared/ui/modal";
import { lanePriorityToValue } from "@/features/lanes/lib/lane-priority";
import { useLaneMutations } from "@/features/lanes/lib/use-lane-mutations";
import {
  LANE_NAME_MAX_LENGTH,
  LANE_NAME_SHAPE_HINT,
  validateLaneName,
} from "@/features/lanes/lib/lane-roster";
import { LanePriorityPicker } from "@/features/lanes/components/lane-priority-picker";
import type { LanePriorityChoice } from "@/features/lanes/components/lane-priority-picker";
import type { LaneRow } from "@/features/lanes/types";

interface LaneAddDialogProps {
  projectId: string;
  roster: LaneRow[];
  onClose: () => void;
}

export function LaneAddDialog({ projectId, roster, onClose }: LaneAddDialogProps) {
  const [name, setName] = useState("");
  const [contour, setContour] = useState("");
  const [priorityChoice, setPriorityChoice] = useState<LanePriorityChoice>("none");
  const { addLane } = useLaneMutations(projectId);

  const validationError = validateLaneName(name, roster);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (validationError !== null || addLane.isPending) {
      return;
    }
    const trimmedContour = contour.trim();
    addLane.mutate(
      {
        name: name.trim(),
        contour: trimmedContour.length > 0 ? trimmedContour : null,
        serialOrder: null,
        priority: priorityChoice === "none" ? null : lanePriorityToValue(priorityChoice),
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
            maxLength={LANE_NAME_MAX_LENGTH}
          />
          <span className="mt-1 block text-xs text-muted">{LANE_NAME_SHAPE_HINT}</span>
        </Field>
        <Field label="Contour (optional)" htmlFor="lane-contour">
          <Input
            id="lane-contour"
            value={contour}
            onChange={(event) => setContour(event.target.value)}
            placeholder="Files or area this lane owns"
          />
        </Field>
        <LanePriorityPicker value={priorityChoice} onChange={setPriorityChoice} />
        {validationError !== null ? (
          <p className="text-xs text-danger" role="alert">
            {validationError}
          </p>
        ) : (
          <ErrorText error={addLane.error} />
        )}
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={validationError !== null || addLane.isPending}>
            {addLane.isPending ? "Adding…" : "Add lane"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
