import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { PAGE_HINTS, pageHintId } from "../src/shared/lib/page-hints.ts";

const SEEDED = ["/planning", "/lanes", "/wave-chat"];

test("the main surfaces ship hints with real copy", () => {
  for (const path of SEEDED) {
    const hints = PAGE_HINTS[path];
    assert.ok(hints && hints.length > 0, `${path} carries at least one hint`);
    for (const hint of hints) {
      assert.ok(hint.title.trim().length > 0, `${path}: a hint title is never empty`);
      assert.ok(hint.text.trim().length > 0, `${path}: a hint text is never empty`);
    }
  }
});

test("hint titles stay unique inside one page so keys and labels stay sane", () => {
  for (const path of SEEDED) {
    const titles = PAGE_HINTS[path].map((hint) => hint.title);
    assert.equal(new Set(titles).size, titles.length, `${path}: duplicate title`);
  }
});

test("hint ids are stable, slot-scoped and distinct across pages", () => {
  assert.equal(pageHintId("/wave-chat", 0), "page-hint-wave-chat-0");
  assert.equal(pageHintId("/wave-chat", 1), "page-hint-wave-chat-1");
  assert.notEqual(pageHintId("/lanes", 0), pageHintId("/planning", 0));
  assert.equal(pageHintId("/wave-chat", 0), pageHintId("/wave-chat", 0), "stable across calls");
});

test("the renderer consumes the mapping instead of a private copy", () => {
  const source = readFileSync(
    new URL("../src/shared/ui/page-hint-list.tsx", import.meta.url),
    "utf8",
  );
  assert.ok(source.includes("PAGE_HINTS[path]"), "the mapping is the source of truth");
  assert.ok(source.includes("pageHintId(path, index)"), "renderer ids stay deterministic");
  assert.ok(source.includes("<Hint"), "hints render through the primitive");
  assert.ok(
    source.includes("return null"),
    "an unseeded page renders nothing rather than an empty shell",
  );
});
