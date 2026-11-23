"use client";

import { useCallback, useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from "react";
import { Button } from "@/shared/ui/button";
import { ErrorText } from "@/shared/ui/error-text";
import { Field } from "@/shared/ui/field";
import { Input } from "@/shared/ui/input";
import { Modal } from "@/shared/ui/modal";
import { Textarea } from "@/shared/ui/textarea";
import { Zerling, ZERLING_SIZES } from "@/shared/ui/zerling";
import { useJournalContainer, useJournalMutations } from "@/features/journal/lib/use-journal";
import {
  clearCaptureDraft,
  deriveCaptureTitle,
  formatCaptureShortcut,
  persistCaptureDraft,
  readCaptureDraft,
  type DraftStorage,
} from "@/features/journal/lib/capture";

const SAVE_TOAST_MS = 2600;

// Mirrors what the browser considers tabbable inside the dialog; the save button
// drops out while disabled, so the ring walks the fields and the close X only.
const FOCUSABLE =
  'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])';

function sessionDrafts(): DraftStorage | null {
  return typeof window === "undefined" ? null : window.sessionStorage;
}

interface CaptureOverlayProps {
  open: boolean;
  onClose: () => void;
}

export function CaptureOverlay({ open, onClose }: CaptureOverlayProps) {
  // The draft is read at mount, not in an effect: this app navigates with plain
  // <a href>, so a route change is a fresh mount. Modal renders nothing until it
  // opens, so the static export never sees this value.
  const [body, setBody] = useState(() => readCaptureDraft(sessionDrafts()));
  const [title, setTitle] = useState("");
  const [toast, setToast] = useState<{ count: number } | null>(null);
  const captured = useRef(0);
  const formRef = useRef<HTMLFormElement>(null);

  const { containerId, loading: containerLoading } = useJournalContainer();
  const { create } = useJournalMutations(containerId);
  const shortcut = formatCaptureShortcut(typeof navigator === "undefined" ? "" : navigator.userAgent);

  useEffect(() => {
    if (!open) {
      // The batch count belongs to one stretch of the overlay being open, not to
      // the page: without this the first save after a reopen says "2 thoughts".
      captured.current = 0;
      return;
    }
    const dialog = formRef.current?.closest<HTMLElement>('[role="dialog"]') ?? null;
    if (dialog === null) {
      return;
    }
    const keepFocusInside = (event: globalThis.KeyboardEvent) => {
      if (event.key !== "Tab") {
        return;
      }
      const stops = Array.from(dialog.querySelectorAll<HTMLElement>(FOCUSABLE));
      if (stops.length === 0) {
        return;
      }
      const head = stops[0];
      const tail = stops[stops.length - 1];
      const active = document.activeElement;
      const escaped = !dialog.contains(active);
      if (event.shiftKey ? active === head || escaped : active === tail || escaped) {
        event.preventDefault();
        (event.shiftKey ? tail : head).focus();
      }
    };
    window.addEventListener("keydown", keepFocusInside);
    return () => window.removeEventListener("keydown", keepFocusInside);
  }, [open]);

  useEffect(() => {
    if (toast === null) {
      return;
    }
    const timer = window.setTimeout(() => setToast(null), SAVE_TOAST_MS);
    return () => window.clearTimeout(timer);
  }, [toast]);

  const save = useCallback(() => {
    const content = body.trim();
    if (content.length === 0) {
      return;
    }
    create.mutate(
      { title: title.trim() || deriveCaptureTitle(content), content, tags: [] },
      {
        onSuccess: () => {
          // Batch capture: the overlay outlives the save, the field and the draft
          // do not.
          captured.current += 1;
          setBody("");
          setTitle("");
          setToast({ count: captured.current });
          clearCaptureDraft(sessionDrafts());
        },
      },
    );
  }, [body, create, title]);

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    save();
  };

  const handleBodyKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === "Enter" && (event.metaKey || event.ctrlKey)) {
      event.preventDefault();
      save();
    }
  };

  // No container is not a blocked capture: the first thought is what brings the
  // reserved journal into existence, the Host resolves it on the write.
  const startsTheJournal = containerId.length === 0;
  const saveDisabled = body.trim().length === 0 || create.isPending;

  return (
    <>
      <Modal title="Capture a thought" open={open} onClose={onClose} size="xl">
        <form ref={formRef} onSubmit={handleSubmit} className="flex h-full flex-col gap-3" aria-describedby={toast ? "journal-capture-status" : undefined}>
          <Field label="Thought" htmlFor="journal-capture-body" fill>
            <Textarea
              id="journal-capture-body"
              autoFocus
              className="min-h-0 flex-1 resize-none"
              value={body}
              onChange={(event) => {
                setBody(event.target.value);
                persistCaptureDraft(sessionDrafts(), event.target.value);
              }}
              onKeyDown={handleBodyKeyDown}
              placeholder="Write it down — tags and links come later."
              // Passport §5 wants this field a step above the app's textarea default,
              // and a text-base class loses to the primitive's text-sm (cn() is a plain
              // join, and the built CSS emits .text-sm after .text-base). Token inline
              // is the only form that actually applies.
              style={{ fontSize: "var(--text-base)" }}
              aria-describedby={toast ? "journal-capture-hint journal-capture-status" : "journal-capture-hint"}
            />
          </Field>
          <p id="journal-capture-hint" className="text-xs text-muted">
            {shortcut.save} saves and clears the field, the overlay stays open for the next thought. Esc or Cancel drops
            you out — text you abandon waits for {shortcut.open} on this tab.
          </p>
          <Field label="Title (optional)" htmlFor="journal-capture-title">
            <Input
              id="journal-capture-title"
              value={title}
              onChange={(event) => setTitle(event.target.value)}
              placeholder="Empty title takes the first line, cut at 80 characters"
            />
          </Field>
          {startsTheJournal && !containerLoading ? (
            <p className="text-sm text-muted">Your journal does not exist yet — this thought opens it.</p>
          ) : null}
          <ErrorText error={create.error} />
          <div className="flex items-center justify-end gap-2">
            {create.error ? (
              <Button variant="secondary" onClick={save}>
                Retry
              </Button>
            ) : null}
            <Button variant="secondary" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={saveDisabled}>
              {create.isPending ? "Saving…" : "Save"}
            </Button>
          </div>
        </form>
      </Modal>
      {/* Kept outside the dialog on purpose: the region has to survive the dialog
          closing during a save, and the aria-describedby above is what points AT
          back into it while the chip is up. */}
      <div
        id="journal-capture-status"
        role="status"
        aria-live="polite"
        className="pointer-events-none fixed bottom-6 left-1/2 z-[60] -translate-x-1/2"
      >
        {toast ? (
          <span className="pointer-events-auto flex animate-bubble-pop items-center gap-2 rounded-md border border-success/40 bg-surface-raised px-3 py-2 text-sm text-success shadow-xl motion-reduce:animate-none">
            <Zerling pose="celebrating" size={ZERLING_SIZES.chip} />
            {toast.count > 1 ? `${toast.count} thoughts captured` : "Captured to the stream"}
          </span>
        ) : null}
      </div>
    </>
  );
}
