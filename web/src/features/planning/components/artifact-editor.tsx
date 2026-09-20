"use client";

import { useState, type FormEvent } from "react";
import { Markdown } from "@/shared/lib/markdown";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { Textarea } from "@/shared/ui/textarea";
import { WritePreviewToggle } from "@/shared/ui/write-preview-toggle";
import { artifactStatusTone } from "@/features/planning/lib/status-tone";
import type { PlanningArtifactResponse, PlanningDocumentKind } from "@/features/planning/types";

export interface ArtifactEditorValues {
  body: string;
  logNote?: string;
}

interface ArtifactEditorProps {
  kind: PlanningDocumentKind;
  initial?: PlanningArtifactResponse;
  pending: boolean;
  approvePending: boolean;
  error: Error | null;
  onSubmit: (values: ArtifactEditorValues) => void;
  onApprove: () => void;
  onCancel: () => void;
}

export function ArtifactEditor({
  kind,
  initial,
  pending,
  approvePending,
  error,
  onSubmit,
  onApprove,
  onCancel,
}: ArtifactEditorProps) {
  const [body, setBody] = useState(initial?.body ?? "");
  const [changeNote, setChangeNote] = useState("");
  const [showPreview, setShowPreview] = useState(false);
  const [formError, setFormError] = useState<Error | null>(null);

  const frozen = initial?.status === "validated";
  const frozenAt = frozen && initial !== undefined ? initial.frozenAt : null;
  const bodyDirty = body !== (initial?.body ?? "");

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (body.trim().length === 0) {
      setFormError(new Error("Body cannot be empty."));
      return;
    }
    if (frozen && changeNote.trim().length === 0) {
      setFormError(new Error("A change note is required to modify a validated artifact."));
      return;
    }
    setFormError(null);
    onSubmit({ body, logNote: frozen ? changeNote.trim() : undefined });
  };

  return (
    <form onSubmit={handleSubmit} className="flex h-full flex-col gap-3">
      <div className="flex flex-wrap items-center gap-1.5">
        <Badge>{kind}</Badge>
        <Badge tone={artifactStatusTone(initial?.status ?? "draft")}>
          {initial?.status ?? "draft"}
        </Badge>
        {initial ? <span className="font-mono text-xs text-muted">v{initial.version}</span> : null}
      </div>

      {frozenAt !== null ? (
        <p className="rounded-md border border-success/40 bg-success/10 px-3 py-2 text-xs text-success">
          Validated {new Date(frozenAt).toLocaleDateString()} — editing resets it to draft.
        </p>
      ) : null}

      <div className="flex min-h-0 flex-1 flex-col">
        <WritePreviewToggle showPreview={showPreview} onShowPreviewChange={setShowPreview} />
        {showPreview ? (
          <div className="min-h-32 flex-1 overflow-y-auto rounded-md border border-border bg-surface px-3 py-2">
            {body.trim() ? <Markdown content={body} /> : <p className="text-sm text-muted">Nothing to preview.</p>}
          </div>
        ) : (
          <Textarea
            rows={12}
            className="min-h-0 flex-1 resize-none"
            value={body}
            onChange={(event) => setBody(event.target.value)}
            placeholder="Markdown — what and why for this artifact."
          />
        )}
      </div>

      {frozen ? (
        <div>
          <Field label="Change note" htmlFor="artifact-change-note">
            <Input
              id="artifact-change-note"
              value={changeNote}
              onChange={(event) => setChangeNote(event.target.value)}
              placeholder="What changed since validation?"
            />
          </Field>
          <p className="mt-1 text-xs text-muted">
            Saving resets the artifact to draft and it needs a new approval round.
          </p>
        </div>
      ) : null}

      <ErrorText error={formError ?? error} />
      <div className="flex justify-end gap-2">
        <Button variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
        {initial && !frozen ? (
          <Button
            variant="accent"
            disabled={approvePending || bodyDirty}
            title={bodyDirty ? "Save your changes before approving." : undefined}
            onClick={onApprove}
          >
            {approvePending ? "Approving…" : "Approve"}
          </Button>
        ) : null}
        <Button type="submit" disabled={pending}>
          {pending ? "Saving…" : initial ? "Save changes" : `Create ${kind}`}
        </Button>
      </div>
    </form>
  );
}
