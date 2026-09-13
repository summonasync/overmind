"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { Textarea } from "@/shared/ui/textarea";
import type { PlanningFeatureFormValues, PlanningFeatureResponse } from "@/features/planning/types";

const SLUG_PATTERN = /^[a-z0-9]+(-[a-z0-9]+)*$/;

interface FeatureFormProps {
  initial?: PlanningFeatureResponse;
  pending: boolean;
  error: Error | null;
  onSubmit: (values: PlanningFeatureFormValues) => void;
  onCancel: () => void;
}

export function FeatureForm({ initial, pending, error, onSubmit, onCancel }: FeatureFormProps) {
  const [slug, setSlug] = useState(initial?.slug ?? "");
  const [title, setTitle] = useState(initial?.title ?? "");
  const [body, setBody] = useState(initial?.body ?? "");
  const [validationError, setValidationError] = useState<Error | null>(null);

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    const nextSlug = slug.trim();
    const nextTitle = title.trim();

    if (!SLUG_PATTERN.test(nextSlug) || nextSlug.length > 64) {
      setValidationError(new Error("Slug must be lowercase kebab-case (1-64 chars), e.g. 007-web-planning-board."));
      return;
    }
    if (nextTitle.length === 0 || nextTitle.length > 200) {
      setValidationError(new Error("Title is required (1-200 chars)."));
      return;
    }

    setValidationError(null);
    onSubmit({ slug: nextSlug, title: nextTitle, body });
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
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
      </div>
      <p className="text-xs text-muted">
        Lowercase kebab-case, e.g. 007-web-planning-board. Becomes the feature folder name under BACKLOG/.
      </p>
      <Field label="Body" htmlFor="feature-body">
        <Textarea
          id="feature-body"
          rows={8}
          value={body}
          onChange={(event) => setBody(event.target.value)}
          placeholder="Optional markdown — goal, context, notes for the feature."
        />
      </Field>
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
