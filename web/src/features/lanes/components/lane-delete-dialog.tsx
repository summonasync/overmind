"use client";

import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Modal } from "@/shared/ui/modal";
import { useLaneMutations } from "@/features/lanes/lib/use-lane-mutations";
import type { LaneRow } from "@/features/lanes/types";

interface LaneDeleteDialogProps {
  projectId: string;
  lane: LaneRow;
  onClose: () => void;
}

export function LaneDeleteDialog({ projectId, lane, onClose }: LaneDeleteDialogProps) {
  const { removeLane } = useLaneMutations(projectId);

  function handleConfirm() {
    removeLane.mutate(lane.name, { onSuccess: onClose });
  }

  return (
    <Modal title={`Delete lane ${lane.name}`} open onClose={onClose}>
      <div className="space-y-4">
        <p className="text-sm text-foreground">
          Delete lane &quot;{lane.name}&quot;? Its features become unassigned and move to the
          &quot;Without lane&quot; pool. This cannot be undone.
        </p>
        <ErrorText error={removeLane.error} />
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="danger" disabled={removeLane.isPending} onClick={handleConfirm}>
            {removeLane.isPending ? "Deleting…" : "Delete"}
          </Button>
        </div>
      </div>
    </Modal>
  );
}
