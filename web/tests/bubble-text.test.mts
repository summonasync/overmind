import { strict as assert } from "node:assert";
import { test } from "node:test";
import { BUBBLE_TEXT_LIMIT, bubbleText } from "../src/widgets/app-shell/lib/bubble-text.ts";

test("texts past the bound are cut at the limit and end with an ellipsis", () => {
  const long = "a".repeat(BUBBLE_TEXT_LIMIT + 200);
  const shown = bubbleText(long);
  assert.equal(shown.length, BUBBLE_TEXT_LIMIT + 1);
  assert.ok(shown.endsWith("…"));
  assert.equal(shown.slice(0, BUBBLE_TEXT_LIMIT), long.slice(0, BUBBLE_TEXT_LIMIT));
});

test("short texts pass through without an ellipsis", () => {
  const short = "approve the 028 wave closure";
  assert.equal(bubbleText(short), short);
});

test("a text exactly at the bound keeps its tail", () => {
  const edge = "b".repeat(BUBBLE_TEXT_LIMIT);
  assert.equal(bubbleText(edge), edge);
});

test("the limit never lands inside a grapheme cluster", () => {
  const tornPair = "a".repeat(799) + "👍";
  assert.equal(bubbleText(tornPair), "a".repeat(799) + "…");

  const family = "a".repeat(799) + "👨‍👩‍👧";
  assert.equal(bubbleText(family), "a".repeat(799) + "…");
});
