"use client";

import {
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ChangeEvent,
  type KeyboardEvent,
} from "react";
import { MarkdownEditor } from "@/shared/ui/markdown-editor";
import {
  WikiLinkAutocomplete,
  wikilinkOptionId,
  WIKILINK_LIST_ID,
} from "@/features/journal/components/wikilink-autocomplete";
import {
  caretAnchor,
  findWikiTrigger,
  hardLinesBefore,
  insertMarker,
  readAnchorFrames,
  type Anchor,
  type WikiTrigger,
} from "@/features/journal/lib/note-links";
import { buildLinkSuggestions, type CandidateSet } from "@/features/journal/lib/link-targets";

export const NOTE_BODY_HINT_ID = "journal-note-body-hint";
export const NOTE_BODY_INPUT_ID = "journal-note-body";

const NOTE_BODY_PLACEHOLDER = "Add the thought — [[ links a note, a project or a feature.";

interface NoteEditorProps {
  value: string;
  onValueChange: (value: string) => void;
  candidates: CandidateSet;
  currentNoteId: string;
  onSaveShortcut: () => void;
}

// The passport's v1 editor shape (§10 option A): one markdown editor with an overlay list hung
// off its caret. `MarkdownEditor` is the shared write/preview pair that
// tests/markdown-adoption-sweep.test.mts pins to a single owner across the tree, so this feature
// does not mount a second Textarea-plus-toggle of its own — it takes the field's keystrokes
// through the primitive's `bodyProps` door and owns only the autocomplete above it.
//
// No ref is pushed into `shared/ui/textarea.tsx` either: that primitive types its props without
// one, and `ref` on it is a TS2322. The node comes back on every handler's `event.currentTarget`
// instead — every path that needs imperative focus starts as a keystroke inside that field, so
// the capture is already there and the ten modules importing the primitive stay as they were.
export function NoteEditor({
  value,
  onValueChange,
  candidates,
  currentNoteId,
  onSaveShortcut,
}: NoteEditorProps) {
  const [query, setQuery] = useState("");
  const [anchor, setAnchor] = useState<Anchor | null>(null);
  const [active, setActive] = useState(0);
  const [open, setOpen] = useState(false);

  const field = useRef<HTMLTextAreaElement | null>(null);
  const shell = useRef<HTMLDivElement | null>(null);
  const trigger = useRef<WikiTrigger | null>(null);
  const pendingCaret = useRef<number | null>(null);

  const rows = useMemo(
    () => buildLinkSuggestions(candidates, { query, currentNoteId }),
    [candidates, currentNoteId, query],
  );
  const listed = open && anchor !== null && rows.length > 0;
  const activeIndex = listed ? Math.min(active, rows.length - 1) : 0;

  useLayoutEffect(() => {
    const at = pendingCaret.current;
    if (at === null) {
      return;
    }
    pendingCaret.current = null;
    // React rewrites a controlled textarea's value and drops the caret at the end. The insert
    // above knows exactly where the caret belongs, so it goes back after the commit — before it,
    // and the restore would be stamped on the old value.
    const el = field.current;
    if (el === null) {
      return;
    }
    el.focus();
    el.setSelectionRange(at, at);
  }, [value]);

  const close = () => {
    setOpen(false);
    setAnchor(null);
    setQuery("");
    trigger.current = null;
  };

  const handleChange = (event: ChangeEvent<HTMLTextAreaElement>) => {
    const el = event.currentTarget;
    field.current = el;
    // bodyProps.onChange takes the field's change wiring over from the primitive, so the value
    // has to be pushed up here explicitly — dropping this line silently freezes the editor.
    onValueChange(el.value);
    const caret = el.selectionStart ?? el.value.length;
    const found = findWikiTrigger(el.value, caret);
    const shellNode = shell.current;
    if (found === null || shellNode === null) {
      close();
      return;
    }
    if (trigger.current === null || found.start !== trigger.current.start) {
      setActive(0);
    }
    trigger.current = found;
    setQuery(found.partial);
    const frames = readAnchorFrames(el, shellNode);
    setAnchor(
      caretAnchor(frames.field, frames.shell, frames.metrics, hardLinesBefore(el.value, caret)),
    );
    setOpen(true);
  };

  const pick = (marker: string) => {
    const el = field.current;
    const at = trigger.current;
    if (el === null || at === null) {
      close();
      return;
    }
    const caret = el.selectionStart ?? at.end;
    const insertion = insertMarker(el.value, at, caret, marker);
    pendingCaret.current = insertion.caret;
    onValueChange(insertion.value);
    close();
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (listed) {
      if (event.key === "ArrowDown") {
        event.preventDefault();
        setActive((index) => (index + 1) % rows.length);
        return;
      }
      if (event.key === "ArrowUp") {
        event.preventDefault();
        setActive((index) => (index - 1 + rows.length) % rows.length);
        return;
      }
      // Tab and Enter both complete the selection. Tab has to be swallowed here: its default
      // action moves focus out of the field, which would strand the caret the insert needs.
      if (event.key === "Enter" || event.key === "Tab") {
        event.preventDefault();
        pick(rows[activeIndex].marker);
        return;
      }
      if (event.key === "Escape") {
        event.preventDefault();
        // stopPropagation is the stacked-dialog half of this. `shared/ui/modal.tsx` listens for
        // Escape on `window`, so without it closing the list would also close whatever dialog the
        // editor is mounted under. No second window-level listener is added anywhere here.
        event.stopPropagation();
        close();
        return;
      }
    }
    if ((event.metaKey || event.ctrlKey) && event.key === "Enter") {
      event.preventDefault();
      onSaveShortcut();
    }
  };

  // Clicking a row keeps focus inside the field (the option cancels mousedown), so anything that
  // pulls focus away is the owner leaving the editor for real.
  const handleBlur = () => {
    if (listed) {
      close();
    }
  };

  return (
    <div ref={shell} className="relative">
      <MarkdownEditor
        value={value}
        onValueChange={onValueChange}
        placeholder={NOTE_BODY_PLACEHOLDER}
        className="min-h-56"
        bodyProps={{
          id: NOTE_BODY_INPUT_ID,
          "aria-label": "Note body",
          "aria-describedby": NOTE_BODY_HINT_ID,
          role: "combobox",
          "aria-expanded": listed,
          "aria-haspopup": "listbox",
          "aria-controls": listed ? WIKILINK_LIST_ID : undefined,
          "aria-autocomplete": "list",
          "aria-activedescendant": listed ? wikilinkOptionId(activeIndex) : undefined,
          onChange: handleChange,
          onKeyDown: handleKeyDown,
          onBlur: handleBlur,
          // Passport §8: the body is reading matter, so it takes the base step and a wider
          // leading than the surrounding UI. A `text-base` class loses to the primitive's own
          // `text-sm` — web's cn() concatenates and the built sheet emits the primitive last —
          // so the token goes inline, the same way the capture field solves it.
          style: { fontSize: "var(--text-base)", lineHeight: 1.6 },
        }}
      />
      <WikiLinkAutocomplete
        anchor={listed ? anchor : null}
        rows={rows}
        activeIndex={activeIndex}
        onPick={(row) => pick(row.marker)}
        onHover={setActive}
      />
      <p id={NOTE_BODY_HINT_ID} className="mt-1 text-xs text-muted">
        <code className="rounded bg-surface-raised px-1 font-mono">[[</code> links a note, a
        project or a feature. Arrows walk the list, Tab completes it, Esc drops it.
      </p>
    </div>
  );
}
