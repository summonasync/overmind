import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  entryReadBody,
  entryReadMetaLine,
  entryTagChipStyle,
  toggleEntryExpanded,
} from "../src/features/entries/lib/entry-read.ts";

test("toggle expands a closed entry", () => {
  assert.deepEqual(toggleEntryExpanded(new Set(), "e1"), new Set(["e1"]));
});

test("toggle collapses an open entry", () => {
  assert.deepEqual(toggleEntryExpanded(new Set(["e1"]), "e1"), new Set());
});

test("expand toggles stay independent between entries", () => {
  const withFirst = toggleEntryExpanded(new Set(), "e1");
  const withBoth = toggleEntryExpanded(withFirst, "e2");
  assert.deepEqual(withBoth, new Set(["e1", "e2"]));

  const afterCollapse = toggleEntryExpanded(withBoth, "e1");
  assert.deepEqual(afterCollapse, new Set(["e2"]));
});

test("toggling never mutates the previous expanded set", () => {
  const current = new Set(["e1"]);
  const next = toggleEntryExpanded(current, "e2");
  assert.deepEqual(current, new Set(["e1"]));
  assert.notEqual(current, next);
});

test("toggling the same id twice lands back on the closed state", () => {
  const opened = toggleEntryExpanded(new Set(), "e1");
  assert.deepEqual(toggleEntryExpanded(opened, "e1"), new Set());
});

test("blank content reads as no body", () => {
  assert.equal(entryReadBody(""), null);
  assert.equal(entryReadBody("   \n\t  "), null);
});

test("read body keeps the exact markdown the editor prefill gets", () => {
  const content = "\n# Heading\n\n- one\n- two `code`\n\n  trailing  \n";
  assert.equal(entryReadBody(content), content);
});

test("meta line carries version and both date labels", () => {
  assert.equal(
    entryReadMetaLine(3, "Oct 1, 2026", "2h ago"),
    "v3 · created Oct 1, 2026 · updated 2h ago",
  );
});

test("tag chip style stays undefined without a color", () => {
  assert.equal(entryTagChipStyle(null), undefined);
});

test("tag chip style blends the color into background and border", () => {
  const style = entryTagChipStyle("#ff8800");
  assert.ok(style !== undefined);
  assert.equal(style.color, "#ff8800");
  assert.equal(style.backgroundColor, "#ff88001A");
  assert.equal(style.borderColor, "#ff880040");
});
