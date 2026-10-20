"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { Modal } from "@/shared/ui/modal";
import { lanePriorityFromValue, lanePriorityToValue } from "@/features/lanes/lib/lane-priority";
import { useLaneMutations } from "@/features/lanes/lib/use-lane-mutations";
import { LANE_NAME_MAX_LENGTH } from "@/features/lanes/lib/lane-roster";
import { LanePriorityPicker } from "@/features/lanes/components/lane-priority-picker";
import type { LanePriorityChoice } from "@/features/lanes/components/lane-priority-picker";
import type { LaneRow } from "@/features/lanes/types";

interface LaneEditDialogProps {
  projectId: string;
  lane: LaneRow;
  onClose: () => void;
}

function parseSerialOrder(value: string): number | null | undefined {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return null;
  }
  const parsed = Number(trimmed);
  if (!Number.isInteger(parsed) || parsed < 0) {
    return undefined;
  }
  return parsed;
}

export function LaneEditDialog({ projectId, lane, onClose }: LaneEditDialogProps) {
  const [contour, setContour] = useState(lane.contour ?? "");
  const [serialOrder, setSerialOrder] = useState(lane.serialOrder === null ? "" : String(lane.serialOrder));
  const [priorityChoice, setPriorityChoice] = useState<LanePriorityChoice>(
    lane.priority === null ? "none" : (lanePriorityFromValue(lane.priority) ?? "none"),
  );
  const { editLane } = useLaneMutations(projectId);

  const parsedSerialOrder = parseSerialOrder(serialOrder);
  const serialOrderError =
    parsedSerialOrder === undefined ? "Serial order must be a whole number of 0 or greater." : null;

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (serialOrderError !== null || editLane.isPending) {
      return;
    }
    const trimmedContour = contour.trim();
    editLane.mutate(
      {
        name: lane.name,
        contour: trimmedContour.length > 0 ? trimmedContour : null,
        serialOrder: parsedSerialOrder ?? null,
        priority: priorityChoice === "none" ? null : lanePriorityToValue(priorityChoice),
      },
      { onSuccess: onClose },
    );
  }

  return (
    <Modal title={`Edit lane ${lane.name}`} open onClose={onClose}>
      <form onSubmit={handleSubmit} className="space-y-3">
        <Field label="Name" htmlFor="lane-edit-name">
          <Input
            id="lane-edit-name"
            value={lane.name}
            readOnly
            aria-readonly="true"
            maxLength={LANE_NAME_MAX_LENGTH}
            className="cursor-not-allowed"
          />
          <span className="mt-1 block text-xs text-muted">
            The name is the lane identity and cannot change — to rename a lane, delete it and add a
            new one, then move its features back.
          </span>
        </Field>
        <Field label="Contour (optional)" htmlFor="lane-edit-contour">
          <Input
            id="lane-edit-contour"
            value={contour}
            onChange={(event) => setContour(event.target.value)}
            placeholder="Files or area this lane owns"
          />
        </Field>
        <Field label="Serial order (optional)" htmlFor="lane-edit-serial">
          <Input
            id="lane-edit-serial"
            type="number"
            min={0}
            step={1}
            value={serialOrder}
            onChange={(event) => setSerialOrder(event.target.value)}
            placeholder="Empty means the lane is not serial"
          />
        </Field>
        <LanePriorityPicker value={priorityChoice} onChange={setPriorityChoice} />
        {serialOrderError !== null ? (
          <p className="text-xs text-danger" role="alert">
            {serialOrderError}
          </p>
        ) : (
          <ErrorText error={editLane.error} />
        )}
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" disabled={serialOrderError !== null || editLane.isPending}>
            {editLane.isPending ? "Saving…" : "Save lane"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
