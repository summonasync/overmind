import { deriveCaptureTitle } from "./capture.ts";
import type { JournalNote, JournalNoteFormValues } from "../types.ts";

export interface NoteDraft {
  title: string;
  content: string;
  tags: readonly string[];
}

export function draftFromNote(note: JournalNote): NoteDraft {
  return { title: note.title, content: note.content, tags: [...note.tags] };
}

// Tags are a set on the Host and an array on the wire, and nothing promises the order the
// save sent is the order the read returns. Comparing them as sets is what keeps the editor
// from claiming it has unsaved work after a round trip reordered the same four tags.
export function isDraftDirty(draft: NoteDraft, saved: NoteDraft): boolean {
  return (
    draft.title !== saved.title ||
    draft.content !== saved.content ||
    !sameTagSet(draft.tags, saved.tags)
  );
}

export function sameTagSet(a: readonly string[], b: readonly string[]): boolean {
  if (a.length !== b.length) {
    return false;
  }
  const left = [...a].map(normalizeTag).sort();
  const right = [...b].map(normalizeTag).sort();
  return left.every((tag, index) => tag === right[index]);
}

function normalizeTag(tag: string): string {
  return tag.trim().toLowerCase();
}

// The title is not optional on the wire — `CreateEntryRequestValidator` pins Title and
// Content both NotEmpty — so an empty title is filled the way capture fills it: the first
// line of the body, cut at the same 80 code points. A note the owner left untitled keeps
// reading as the same note across a body edit, which is the trade, and the alternative is a
// 400 on Save with no field to blame.
export function resolveTitle(draft: NoteDraft): string {
  const typed = draft.title.trim();
  return typed.length > 0 ? typed : deriveCaptureTitle(draft.content.trim());
}

// One answer shared by the disabled Save button and the message next to it, so the UI can
// never explain a blocker the save path would not have hit.
export function saveBlocker(draft: NoteDraft): string | null {
  if (draft.content.trim().length === 0) {
    return "A note needs a body — the Host stores no empty ones.";
  }
  if (resolveTitle(draft).length === 0) {
    return "A note needs a title or a first line to take one from.";
  }
  return null;
}

export function toFormValues(draft: NoteDraft): JournalNoteFormValues | null {
  if (saveBlocker(draft) !== null) {
    return null;
  }
  return { title: resolveTitle(draft), content: draft.content, tags: [...draft.tags] };
}

export interface TagEdit {
  draft: NoteDraft;
  error: string | null;
}

// `EntryValidators` rejects a tag containing a comma, and the message it sends is a 400 the
// owner cannot map to the box they typed in. The rule is enforced here so the field says it
// while typing; the server stays the authority and still gets the last word.
export const TAG_COMMA_ERROR = "A tag cannot contain a comma.";

export function addTag(draft: NoteDraft, raw: string): TagEdit {
  const tag = raw.trim();
  if (tag.length === 0) {
    return { draft, error: "Nothing to add." };
  }
  if (tag.includes(",")) {
    return { draft, error: TAG_COMMA_ERROR };
  }
  if (draft.tags.some((existing) => normalizeTag(existing) === normalizeTag(tag))) {
    return { draft, error: "That tag is already on this note." };
  }
  return { draft: { ...draft, tags: [...draft.tags, tag] }, error: null };
}

export function removeTag(draft: NoteDraft, tag: string): NoteDraft {
  return { ...draft, tags: draft.tags.filter((existing) => existing !== tag) };
}

export const UNNAMED_NOTE = "Untitled note";

export function railLabel(title: string): string {
  return title.trim().length > 0 ? title.trim() : UNNAMED_NOTE;
}
