"use client";

import { useState } from "react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { Select } from "@/shared/ui/select";
import { Textarea } from "@/shared/ui/textarea";
import {
  EMPTY_PROMOTION_FORM,
  promotedCardHref,
  promotedLabel,
  promotionBlocker,
  promotionConfirmLabel,
  promotionNameLabel,
  promotionRefusal,
  promotionTargetLabel,
  type PromotionCacheView,
} from "@/features/journal/lib/note-promotion";
import { useFeatureCandidates, usePromoteNote } from "@/features/journal/lib/use-promote-note";
import { useProjectList } from "@/features/projects";
import type { PromotionForm } from "@/features/journal/types";

interface PromoteNoteProps {
  /** The note the act is performed on. */
  entryId: string;
  /** Seeds the card name, so the owner is re-reading a thought rather than retyping it. */
  noteTitle: string;
  /** The mounting surface's view of its own caches: which graph field it is drawing, if any. */
  graphFilters?: PromotionCacheView["graphFilters"];
  size?: "sm" | "md";
}

/**
 * The exit door of the orphan pile, as a form: choose what the thought becomes, name it, confirm —
 * or cancel, which sends nothing at all.
 *
 * One component used from both surfaces (the note view and the graph context panel) rather than two
 * near-copies, because the states are the interesting part and a drift between two copies of a
 * refusal is exactly how a 409 ends up reading as a crash on one of them.
 */
export function PromoteNote({ entryId, noteTitle, graphFilters, size = "sm" }: PromoteNoteProps) {
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState<PromotionForm>(() => seedForm(noteTitle));
  const projects = useProjectList();
  const promotion = usePromoteNote();

  const busy = promotion.pending;
  const blocker = promotionBlocker(form);
  // A refusal is a fact about the note ("this one is already a card"), so it is read off the
  // refusal code before the generic banner gets its chance to call that a failure.
  const refusal = promotionRefusal(promotion.error);
  const promoted = promotion.promotion;
  const candidates = useFeatureCandidates(form.projectId);

  if (!open) {
    return (
      <Button size={size} variant="secondary" disabled={busy} onClick={() => setOpen(true)}>
        Sort into board work
      </Button>
    );
  }

  // Cancel is local by construction: nothing has been asked of the door until Confirm, so closing
  // the form cannot leave a draft, an edge or a card behind. `reset` only drops the answer the
  // panel is showing from a previous attempt in this mount.
  const close = () => {
    setOpen(false);
    setForm(seedForm(noteTitle));
    promotion.reset();
  };

  const patch = (next: Partial<PromotionForm>) => setForm((current) => ({ ...current, ...next }));

  const confirm = () => {
    promotion.submit(entryId, form, { boardProjectId: form.projectId, graphFilters });
  };

  return (
    <form
      className="space-y-2"
      onSubmit={(event) => {
        event.preventDefault();
        confirm();
      }}
    >
      <fieldset className="space-y-2" disabled={busy}>
        <legend className="text-xs font-semibold uppercase tracking-wide text-muted">
          Sort into board work
        </legend>

        <div className="flex flex-wrap gap-2">
          {(["feature", "task"] as const).map((target) => (
            <Button
              key={target}
              size={size}
              variant={form.target === target ? "primary" : "secondary"}
              aria-pressed={form.target === target}
              onClick={() => patch({ target })}
            >
              {promotionTargetLabel(target)}
            </Button>
          ))}
        </div>

        <Field label="Board" htmlFor={`promote-project-${entryId}`}>
          <Select
            id={`promote-project-${entryId}`}
            size="sm"
            value={form.projectId}
            onChange={(event) => patch({ projectId: event.target.value, featureId: "" })}
          >
            <option value="">
              {projects.loading ? "Loading projects…" : "Pick the board this lands on"}
            </option>
            {projects.data.map((project) => (
              <option key={project.id} value={project.id}>
                {project.name}
              </option>
            ))}
          </Select>
        </Field>

        {form.target === "task" ? (
          <Field label="Existing feature" htmlFor={`promote-feature-${entryId}`}>
            <Select
              id={`promote-feature-${entryId}`}
              size="sm"
              value={form.featureId}
              onChange={(event) => patch({ featureId: event.target.value })}
            >
              <option value="">
                {form.projectId.length === 0
                  ? "Pick a board first"
                  : candidates.loading
                    ? "Loading features…"
                    : "Pick the feature this hangs under"}
              </option>
              {candidates.candidates.map((candidate) => (
                <option key={candidate.id} value={candidate.id}>
                  {candidate.done ? `${candidate.label} (done)` : candidate.label}
                </option>
              ))}
            </Select>
          </Field>
        ) : null}

        <Field label={promotionNameLabel(form.target)} htmlFor={`promote-name-${entryId}`}>
          <Input
            id={`promote-name-${entryId}`}
            value={form.name}
            onChange={(event) => patch({ name: event.target.value })}
            placeholder={form.target === "feature" ? "A card worth reading later" : "One line"}
          />
        </Field>

        <Field label="What the card is for" htmlFor={`promote-body-${entryId}`}>
          <Textarea
            id={`promote-body-${entryId}`}
            rows={3}
            value={form.body}
            onChange={(event) => patch({ body: event.target.value })}
            placeholder="The note keeps its own text — this is the card's."
          />
        </Field>

        <div className="flex flex-wrap items-center gap-2">
          <Button type="submit" size={size} disabled={busy || blocker !== null}>
            {busy ? "Sorting…" : promotionConfirmLabel(form.target)}
          </Button>
          <Button type="button" size={size} variant="ghost" onClick={close}>
            Cancel
          </Button>
          {blocker !== null ? <span className="text-xs text-warning">{blocker}</span> : null}
        </div>
      </fieldset>

      {refusal !== null ? (
        // The door named the card the note already became; the surface says so in its own voice and
        // keeps the server sentence underneath, because that sentence is the evidence.
        <div
          role="status"
          aria-live="polite"
          className="space-y-1 rounded-md border border-border bg-surface-raised px-3 py-2"
        >
          <p className="text-sm text-foreground">{refusal.headline}</p>
          {refusal.boardSlug !== null ? (
            <a
              href={promotedCardHref(form.projectId, refusal.boardSlug)}
              className="block truncate text-xs text-primary underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
            >
              Open {refusal.boardSlug}
            </a>
          ) : null}
          <p className="text-xs text-muted">{refusal.detail}</p>
        </div>
      ) : promoted !== null ? (
        <div
          role="status"
          aria-live="polite"
          className="space-y-1 rounded-md border border-border bg-surface-raised px-3 py-2"
        >
          <p className="text-sm text-foreground">
            <a
              href={promotedCardHref(form.projectId, promoted.featureSlug)}
              className="text-primary underline-offset-2 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
            >
              {promotedLabel(promoted)}
            </a>
          </p>
          <p className="text-xs text-muted">
            The edge is what says this note is sorted. The note itself was not rewritten.
          </p>
        </div>
      ) : (
        // Anything without a refusal code is a genuine failure, and wears the banner.
        <ErrorText error={promotion.error} />
      )}
    </form>
  );
}

function seedForm(noteTitle: string): PromotionForm {
  return { ...EMPTY_PROMOTION_FORM, name: noteTitle.trim() };
}
