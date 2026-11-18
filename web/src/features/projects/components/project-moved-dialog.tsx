"use client";

import { useState } from "react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { Modal } from "@/shared/ui/modal";
import { FolderBrowser } from "@/features/projects/components/folder-picker-modal";
import { parseMirrorDrift } from "@/features/projects/lib/mirror-drift";
import { useProjectMoved } from "@/features/projects/lib/use-project-moved";
import type { MirrorDrift } from "@/features/projects/lib/mirror-drift";
import type { ProjectResponse } from "@/features/projects/types";

interface ProjectMovedDialogProps {
  project: ProjectResponse;
  onClose: () => void;
}

export function ProjectMovedDialog({ project, onClose }: ProjectMovedDialogProps) {
  const [browsePath, setBrowsePath] = useState<string | null>(null);
  const [candidate, setCandidate] = useState(project.diskPath);
  const move = useProjectMoved(project);

  const trimmed = candidate.trim();
  const unchanged = trimmed === project.diskPath;
  const drift = move.error !== null ? parseMirrorDrift(move.error.message) : null;

  if (move.isSuccess) {
    const companionSteps = move.data.relocation?.companionSteps ?? [];
    return (
      <Modal title="Project moved to…" open onClose={onClose}>
        <div className="space-y-4">
          <p role="status" className="text-sm text-success">
            Project path updated to <span className="font-mono">{move.data.diskPath}</span>. The
            planning mirror was verified at the new location — all files match.
          </p>
          {companionSteps.length > 0 ? <CompanionSteps steps={companionSteps} /> : null}
          <div className="flex justify-end">
            <Button onClick={onClose}>Done</Button>
          </div>
        </div>
      </Modal>
    );
  }

  return (
    <Modal title="Project moved to…" open onClose={onClose}>
      <div className="space-y-4">
        <p className="text-sm text-muted">
          The project folder was moved outside of Overmind? Point the project at the new location —
          its planning mirror is hash-verified there before the path is saved.
        </p>
        <FolderBrowser path={browsePath} onNavigate={setBrowsePath} />
        <div className="flex justify-end">
          <Button
            variant="secondary"
            size="sm"
            disabled={browsePath === null}
            onClick={() => {
              if (browsePath !== null) {
                setCandidate(browsePath);
              }
            }}
          >
            Use selected folder
          </Button>
        </div>
        <Field label="New location" htmlFor="project-moved-path">
          <Input
            id="project-moved-path"
            value={candidate}
            onChange={(event) => setCandidate(event.target.value)}
            placeholder="E:\\Projects\\my-project"
            aria-describedby="project-moved-current"
          />
        </Field>
        <p id="project-moved-current" className="text-xs text-muted">
          Current location: <span className="font-mono">{project.diskPath}</span>
        </p>
        {drift !== null ? <DriftList drift={drift} /> : <ErrorText error={move.error} />}
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button
            variant="accent"
            disabled={move.isPending || trimmed.length === 0 || unchanged}
            onClick={() => move.mutate(trimmed)}
          >
            {move.isPending ? "Saving…" : "Save new location"}
          </Button>
        </div>
      </div>
    </Modal>
  );
}

function CompanionSteps({ steps }: { steps: string[] }) {
  return (
    <section className="rounded-md border border-warning/40 bg-warning/10 p-3">
      <p className="text-sm text-warning">
        {steps.length === 1 ? "One step is still on you" : `${steps.length} steps are still on you`}{" "}
        outside this database:
      </p>
      <ol className="mt-2 space-y-1" aria-label="Manual companion steps">
        {steps.map((step) => (
          <li key={step} className="break-words text-xs text-muted">
            {step}
          </li>
        ))}
      </ol>
    </section>
  );
}

function DriftList({ drift }: { drift: MirrorDrift }) {
  return (
    <div role="alert" className="rounded-md border border-danger/40 bg-danger/10 p-3">
      <p className="text-sm font-medium text-danger">
        Mirror verification failed: {drift.failedCount} of {drift.total} files drifted or missing
        at the candidate path.
      </p>
      <ul className="mt-2 space-y-1" aria-label="Drifted or missing files">
        {drift.files.map((file) => (
          <li key={file} className="break-all font-mono text-xs text-danger">
            {file}
          </li>
        ))}
      </ul>
      {drift.hiddenCount > 0 ? (
        <p className="mt-1 text-xs text-danger">and {drift.hiddenCount} more.</p>
      ) : null}
    </div>
  );
}
