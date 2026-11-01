"use client";

import { useState, type FormEvent } from "react";
import { Badge } from "@/shared/ui/badge";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { MarkdownEditor } from "@/shared/ui/markdown-editor";
import { ArtifactLog } from "@/features/planning/components/artifact-log";
import { artifactStatusTone } from "@/features/planning/lib/status-tone";
import type { PlanningArtifactResponse } from "@/features/planning/types";

export interface TaskEditorValues {
  body: string;
  brief: string;
}

interface TaskEditorProps {
  initial?: PlanningArtifactResponse;
  pending: boolean;
  logPending: boolean;
  error: Error | null;
  onSubmit: (values: TaskEditorValues) => void;
  onAppendLog: (note: string) => void;
  onCancel: () => void;
}

export function TaskEditor({ initial, pending, logPending, error, onSubmit, onAppendLog, onCancel }: TaskEditorProps) {
  const [body, setBody] = useState(initial?.body ?? "");
  const [brief, setBrief] = useState(initial?.brief ?? "");
  const [note, setNote] = useState("");
  const [formError, setFormError] = useState<Error | null>(null);

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (body.trim().length === 0) {
      setFormError(new Error("Body cannot be empty."));
      return;
    }
    if (brief.length > 500) {
      setFormError(new Error("Brief must be at most 500 characters."));
      return;
    }
    setFormError(null);
    onSubmit({ body, brief: brief.trim() });
  };

  const handleAppend = () => {
    if (note.trim().length === 0) {
      return;
    }
    onAppendLog(note.trim());
    setNote("");
  };

  return (
    <form onSubmit={handleSubmit} className="flex h-full flex-col gap-3">
      {initial ? (
        <div className="flex flex-wrap items-center gap-1.5">
          <Badge tone={artifactStatusTone(initial.status)}>{initial.status}</Badge>
          <Badge>#{initial.order}</Badge>
          <Badge tone={initial.review === "hard" ? "warning" : "neutral"}>{initial.review}</Badge>
          <span className="font-mono text-xs text-muted">v{initial.version}</span>
        </div>
      ) : null}

      <Field label="Brief" htmlFor="task-brief">
        <Input
          id="task-brief"
          value={brief}
          onChange={(event) => setBrief(event.target.value)}
          placeholder="One-liner shown on the board card — clear it to remove."
        />
      </Field>

      <MarkdownEditor
        className="min-h-0 flex-1"
        value={body}
        onValueChange={setBody}
        placeholder={"# Task heading\n\nGoal, scope, acceptance criteria in markdown."}
      />

      {initial ? (
        <div className="space-y-2 border-t border-border pt-3">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-muted">Log</h3>
          <div className="max-h-44 overflow-y-auto">
            <ArtifactLog log={initial.log} />
          </div>
          <div className="flex gap-2">
            <Input
              aria-label="Append log note"
              value={note}
              onChange={(event) => setNote(event.target.value)}
              placeholder="Append a log note…"
              onKeyDown={(event) => {
                if (event.key === "Enter") {
                  event.preventDefault();
                  handleAppend();
                }
              }}
            />
            <Button variant="secondary" disabled={note.trim().length === 0 || logPending} onClick={handleAppend}>
              {logPending ? "Adding…" : "Append"}
            </Button>
          </div>
        </div>
      ) : null}

      <ErrorText error={formError ?? error} />
      <div className="flex justify-end gap-2">
        <Button variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" disabled={pending}>
          {pending ? "Saving…" : initial ? "Save changes" : "Create task"}
        </Button>
      </div>
    </form>
  );
}
