// Spelling and caret math for `[[…]]` markers. This module deliberately owns NO grammar:
// the parser and the resolver live on the Host (spec R3, `WikiLinkParser` /
// `EntryLinkResolver`), and the note view reads their answer off the detail payload (R7).
// What lives here is the two things only the editor can know — where the caret is, and
// what string to put at it.

export const WIKI_TRIGGER = "[[";

export function entryMarker(title: string): string {
  return `${WIKI_TRIGGER}${title.trim()}]]`;
}

// The name is inserted exactly as the registry stores it. That is not laziness: spec R3's
// collision rule gives an ordinal-case match the win outright, so the stored spelling is
// the one spelling the resolver cannot answer "unresolved" to. A lowercased or
// prettified variant would be a coin flip on a board with `Journal` and `journal` rows.
export function projectMarker(name: string): string {
  return `${WIKI_TRIGGER}project:${name}]]`;
}

// Slug form, never the bare number: `feature:<NNN>` resolves across every project on the
// Host, so one more board adding feature 184 turns the marker into a dead end. A full
// slug is resolved by exact match, which is what a picker can actually show a name for.
export function featureMarker(slug: string): string {
  return `${WIKI_TRIGGER}feature:${slug}]]`;
}

export interface WikiTrigger {
  start: number;
  end: number;
  partial: string;
}

// Is the caret sitting inside a half-typed marker? Walking back from the caret stops at the
// first `[[` it finds and refuses to cross a newline or a `]` — a closed `[[foo]]` is a
// finished link, not a thing being typed, and popping a list over it would fight the owner.
export function findWikiTrigger(value: string, caret: number): WikiTrigger | null {
  const from = Math.min(Math.max(caret, 0), value.length);
  for (let at = from - 2; at >= 0; at -= 1) {
    const char = value[at];
    if (char === "\n") {
      return null;
    }
    if (char !== "[" || value[at + 1] !== "[") {
      continue;
    }
    const partial = value.slice(at + 2, from);
    return partial.includes("]") ? null : { start: at, end: from, partial };
  }
  return null;
}

export interface Insertion {
  value: string;
  caret: number;
}

// Replaces the trigger and everything typed after it with the finished marker, plus one
// space so the next `[[` starts clean. Returns the caret the field must land on: a
// controlled textarea jumps to the end of the value when React rewrites it, so without
// this the owner keeps typing into the wrong place after picking a suggestion.
export function insertMarker(
  value: string,
  trigger: WikiTrigger,
  caret: number,
  marker: string,
): Insertion {
  const from = Math.min(Math.max(caret, trigger.start), value.length);
  const next = `${value.slice(0, trigger.start)}${marker} ${value.slice(from)}`;
  return { value: next, caret: trigger.start + marker.length + 1 };
}

export function hardLinesBefore(value: string, caret: number): number {
  const upTo = value.slice(0, Math.max(0, caret));
  const newlines = upTo.match(/\n/g);
  return newlines === null ? 0 : newlines.length;
}

export interface Rect {
  top: number;
  left: number;
  width: number;
  height: number;
}

export interface FieldMetrics {
  paddingTop: number;
  paddingLeft: number;
  lineHeight: number;
  scrollTop: number;
}

export interface Anchor {
  left: number;
  top: number;
  width: number;
}

export const SUGGESTION_MAX_WIDTH = 22 * 16;

// Places the suggestion list under the caret's line, in the coordinate system of the positioned
// wrapper the list is rendered into — not the viewport. The field sits inside a scrolled app
// main, and an absolutely-positioned child of a scrolled ancestor is measured against the padding
// box, so the scroll is subtracted once here (where it belongs) and the two rects supply the rest.
//
// Counted in hard lines: a soft-wrapped line reads as one, so a long paragraph puts the list a
// line or two high. It is clamped into the field's own box, which keeps the worst case "a little
// off" instead of "the list left the editor". The alternative — a mirror div duplicating the
// computed style — buys one pixel of accuracy for a hundred lines of fiddly DOM, and the list is
// keyboard-driven either way.
export function caretAnchor(
  field: Rect,
  shell: Rect,
  metrics: FieldMetrics,
  hardLines: number,
  maxWidth: number = SUGGESTION_MAX_WIDTH,
): Anchor {
  const inField = metrics.paddingTop + hardLines * metrics.lineHeight - metrics.scrollTop;
  const raw = field.top - shell.top + inField;
  const ceiling = Math.max(field.top - shell.top + field.height - metrics.lineHeight, 0);
  const line = Math.min(Math.max(raw, 0), ceiling);
  return {
    left: field.left - shell.left + metrics.paddingLeft,
    top: line + metrics.lineHeight + 2,
    width: Math.min(field.width - metrics.paddingLeft, maxWidth),
  };
}

// Reads what the anchor math needs off a live textarea and its positioned wrapper.
// `line-height: normal` parses to NaN, and the shared Textarea sets no leading of its own, so the
// fallback is the browser's own rough ratio rather than a guess pinned to one font.
export function readAnchorFrames(
  el: HTMLTextAreaElement,
  shellEl: HTMLElement,
): { field: Rect; shell: Rect; metrics: FieldMetrics } {
  const computed = window.getComputedStyle(el);
  const fontSize = parseFloat(computed.fontSize);
  const lineHeight = parseFloat(computed.lineHeight);
  return {
    field: rectOf(el),
    shell: rectOf(shellEl),
    metrics: {
      paddingTop: numberOr(parseFloat(computed.paddingTop), 0),
      paddingLeft: numberOr(parseFloat(computed.paddingLeft), 0),
      lineHeight: Number.isFinite(lineHeight) ? lineHeight : numberOr(fontSize * 1.4, 20),
      scrollTop: el.scrollTop,
    },
  };
}

function rectOf(el: HTMLElement): Rect {
  const box = el.getBoundingClientRect();
  return { top: box.top, left: box.left, width: box.width, height: box.height };
}

function numberOr(value: number, fallback: number): number {
  return Number.isFinite(value) ? value : fallback;
}
