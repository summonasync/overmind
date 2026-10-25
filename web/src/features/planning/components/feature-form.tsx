"use client";

import { useState, type FormEvent } from "react";
import { Markdown } from "@/shared/lib/markdown";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { Select } from "@/shared/ui/select";
import { Textarea } from "@/shared/ui/textarea";
import { WritePreviewToggle } from "@/shared/ui/write-preview-toggle";
import { slugPreview } from "@/features/planning/lib/slug-preview";
import { FEATURE_PRIORITIES } from "@/features/planning/types";
import type {
  PlanningFeatureFormValues,
  PlanningFeaturePriority,
  PlanningFeatureResponse,
} from "@/features/planning/types";

const SLUG_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;

const PRIORITY_LABELS: Record<PlanningFeaturePriority, string> = {
  p0: "P0 — Critical",
  p1: "P1 — High",
  p2: "P2 — Normal",
  p3: "P3 — Low",
};

interface FeatureFormProps {
  initial?: PlanningFeatureResponse;
  pending: boolean;
  error: Error | null;
  takenSlugs?: readonly string[];
  onSubmit: (values: PlanningFeatureFormValues) => void;
  onCancel: () => void;
}

export function FeatureForm({ initial, pending, error, takenSlugs = [], onSubmit, onCancel }: FeatureFormProps) {
  const [slug, setSlug] = useState(initial?.slug ?? "");
  const [title, setTitle] = useState(initial?.title ?? "");
  const [body, setBody] = useState(initial?.body ?? "");
  const [priority, setPriority] = useState<PlanningFeaturePriority>(initial?.priority ?? "p2");
  const [showPreview, setShowPreview] = useState(false);
  const [validationError, setValidationError] = useState<Error | null>(null);

  const preview = slugPreview({ title, slug, takenSlugs });

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    const nextSlug = slug.trim();
    const nextTitle = title.trim();

    if (nextSlug.length > 0 && (!SLUG_PATTERN.test(nextSlug) || nextSlug.length > 64)) {
      setValidationError(new Error("Slug must be lowercase kebab-case (1-64 chars), e.g. 007-web-planning-board."));
      return;
    }
    if (nextTitle.length === 0 || nextTitle.length > 200) {
      setValidationError(new Error("Title is required (1-200 chars)."));
      return;
    }

    setValidationError(null);
    onSubmit({ slug: nextSlug, title: nextTitle, body, priority });
  };

  return (
    <form onSubmit={handleSubmit} className="flex h-full flex-col gap-3">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-[220px_1fr]">
        <Field label="Slug" htmlFor="feature-slug">
          <Input
            id="feature-slug"
            value={slug}
            onChange={(event) => setSlug(event.target.value)}
            disabled={initial !== undefined}
            placeholder="nnn-slug"
            className="font-mono"
          />
        </Field>
        <Field label="Title" htmlFor="feature-title">
          <Input
            id="feature-title"
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            maxLength={200}
            placeholder="Short feature name"
          />
        </Field>
        <Field label="Priority" htmlFor="feature-priority">
          <Select
            id="feature-priority"
            value={priority}
            onChange={(event) => setPriority(event.target.value as PlanningFeaturePriority)}
          >
            {FEATURE_PRIORITIES.map((candidate) => (
              <option key={candidate} value={candidate}>
                {PRIORITY_LABELS[candidate]}
              </option>
            ))}
          </Select>
        </Field>
      </div>
      {initial === undefined && preview !== null ? (
        <p aria-live="polite" className="text-xs text-muted">
          {preview}
        </p>
      ) : null}
      <p className="text-xs text-muted">
        Lowercase kebab-case, e.g. 007-web-planning-board. Blank auto-generates from the title on create. Becomes the
        feature folder name under BACKLOG/.
      </p>
      <div className="flex min-h-0 flex-1 flex-col">
        <div className="flex items-center justify-between">
          <label htmlFor="feature-body" className="mb-1 text-sm font-medium text-foreground">
            Body
          </label>
          <WritePreviewToggle showPreview={showPreview} onShowPreviewChange={setShowPreview} />
        </div>
        {showPreview ? (
          <div className="min-h-32 flex-1 overflow-y-auto rounded-md border border-border bg-surface px-3 py-2">
            {body.trim() ? <Markdown content={body} /> : <p className="text-sm text-muted">Nothing to preview.</p>}
          </div>
        ) : (
          <Textarea
            id="feature-body"
            rows={20}
            className="min-h-0 flex-1 resize-none"
            value={body}
            onChange={(event) => setBody(event.target.value)}
            placeholder="Optional markdown — goal, context, notes for the feature."
          />
        )}
      </div>
      <ErrorText error={validationError ?? error} />
      <div className="flex justify-end gap-2">
        <Button variant="secondary" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" disabled={pending}>
          {pending ? "Saving…" : initial ? "Save changes" : "Create feature"}
        </Button>
      </div>
    </form>
  );
}
