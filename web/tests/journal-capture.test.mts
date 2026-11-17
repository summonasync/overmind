import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  CAPTURE_DRAFT_KEY,
  CAPTURE_TITLE_MAX,
  clearCaptureDraft,
  deriveCaptureTitle,
  formatCaptureShortcut,
  isCaptureHotkey,
  isEditableTarget,
  isQuickCaptureKey,
  persistCaptureDraft,
  readCaptureDraft,
  type CaptureKeyProbe,
} from "../src/features/journal/lib/capture.ts";

const key = (over: Partial<CaptureKeyProbe> = {}): CaptureKeyProbe => ({
  key: " ",
  code: "Space",
  metaKey: false,
  ctrlKey: false,
  shiftKey: true,
  altKey: false,
  ...over,
});

const memoryDraft = () => {
  const rows = new Map<string, string>();
  return {
    getItem: (name: string) => rows.get(name) ?? null,
    setItem: (name: string, value: string) => void rows.set(name, value),
    removeItem: (name: string) => void rows.delete(name),
    rows,
  };
};

test("title falls back to the first line of the body", () => {
  assert.equal(deriveCaptureTitle("Deep dark fantasy\nsecond line\nthird"), "Deep dark fantasy");
});

test("title fallback trims the line it cuts", () => {
  assert.equal(deriveCaptureTitle("  йобнешься  \nmore"), "йобнешься");
});

test("title fallback is capped at 80 characters", () => {
  const long = "х".repeat(CAPTURE_TITLE_MAX + 40);
  assert.equal(deriveCaptureTitle(long).length, CAPTURE_TITLE_MAX);
});

test("title fallback cuts between code points, not inside a surrogate pair", () => {
  const boundary = `a${"🎉".repeat(80)}`;
  assert.equal(Array.from(boundary).length, CAPTURE_TITLE_MAX + 1);
  const title = deriveCaptureTitle(boundary);
  assert.equal(Array.from(title).length, CAPTURE_TITLE_MAX);
  assert.equal(title, `a${"🎉".repeat(79)}`);
  assert.equal(/[\uD800-\uDBFF]$/.test(title), false);
});

test("a short astral body keeps every emoji even when its units exceed the cap", () => {
  const body = `a${"🎉".repeat(40)}`;
  assert.equal(body.length, CAPTURE_TITLE_MAX + 1);
  assert.equal(deriveCaptureTitle(body), body);
});

test("title fallback survives a body that is only whitespace", () => {
  assert.equal(deriveCaptureTitle("   \n\t "), "");
});

test("draft round-trips through the storage seam", () => {
  const storage = memoryDraft();
  persistCaptureDraft(storage, "carry this across the route change");
  assert.equal(readCaptureDraft(storage), "carry this across the route change");
  assert.equal(storage.rows.get(CAPTURE_DRAFT_KEY), "carry this across the route change");
});

test("an emptied draft removes the key instead of storing an empty string", () => {
  const storage = memoryDraft();
  persistCaptureDraft(storage, "one thought");
  persistCaptureDraft(storage, "");
  assert.equal(storage.rows.has(CAPTURE_DRAFT_KEY), false);
  assert.equal(readCaptureDraft(storage), "");
});

test("clearing the draft is what a successful save owes the stream", () => {
  const storage = memoryDraft();
  persistCaptureDraft(storage, "saved already");
  clearCaptureDraft(storage);
  assert.equal(readCaptureDraft(storage), "");
});

test("no storage means no draft, not a crash", () => {
  persistCaptureDraft(null, "prerender");
  assert.equal(readCaptureDraft(null), "");
  clearCaptureDraft(null);
});

test("the capture chord is shift+space with either command key", () => {
  assert.equal(isCaptureHotkey(key({ metaKey: true })), true);
  assert.equal(isCaptureHotkey(key({ ctrlKey: true })), true);
});

test("the capture chord needs shift and nothing else", () => {
  assert.equal(isCaptureHotkey(key({ metaKey: true, shiftKey: false })), false);
  assert.equal(isCaptureHotkey(key({ metaKey: true, altKey: true })), false);
  assert.equal(isCaptureHotkey(key({ metaKey: true, ctrlKey: true })), true);
  assert.equal(isCaptureHotkey(key({ metaKey: false, ctrlKey: false })), false);
});

test("the chord matches on code even when the key string is dead", () => {
  assert.equal(isCaptureHotkey({ ...key({ metaKey: true }), key: "Dead" }), true);
  assert.equal(isCaptureHotkey({ ...key({ ctrlKey: true }), code: undefined, key: " " }), true);
});

test("plain N opens capture only on the journal route", () => {
  assert.equal(isQuickCaptureKey(key({ key: "n", shiftKey: false }), "/journal"), true);
  assert.equal(isQuickCaptureKey(key({ key: "N", shiftKey: false }), "/journal"), true);
  assert.equal(isQuickCaptureKey(key({ key: "n", shiftKey: false }), "/planning"), false);
});

test("shift+N is a typed capital, not the quick key", () => {
  assert.equal(isQuickCaptureKey(key({ key: "N", shiftKey: true }), "/journal"), false);
});

test("typing never triggers the quick key", () => {
  const inTextarea = { tagName: "textarea" };
  const inEditableDiv = { tagName: "div", isContentEditable: true };
  assert.equal(isQuickCaptureKey(key({ key: "n", shiftKey: false, target: inTextarea }), "/journal"), false);
  assert.equal(isQuickCaptureKey(key({ key: "n", shiftKey: false, target: inEditableDiv }), "/journal"), false);
  assert.equal(isEditableTarget({ tagName: "BODY" }), false);
  assert.equal(isEditableTarget(null), false);
});

test("the shortcut glyph follows the platform", () => {
  assert.deepEqual(formatCaptureShortcut("Mozilla/5.0 (Macintosh; Intel Mac OS X)"), {
    open: "⌘⇧Space",
    save: "⌘↵",
  });
  assert.deepEqual(formatCaptureShortcut("Mozilla/5.0 (X11; Linux x86_64)"), {
    open: "⌃⇧Space",
    save: "Ctrl+↵",
  });
});
