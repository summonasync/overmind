import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const hint = () => readFileSync(new URL("../src/shared/ui/hint.tsx", import.meta.url), "utf8");

test("the bubble shows on hover and on keyboard focus, css only", () => {
  const source = hint();
  assert.ok(source.includes("opacity-0"), "the bubble rests hidden");
  assert.ok(
    source.includes("group-hover:opacity-100"),
    "hover reveals the bubble",
  );
  assert.ok(
    source.includes("group-focus-within:opacity-100"),
    "keyboard focus reveals the bubble too",
  );
  assert.ok(source.includes("pointer-events-none"), "the resting bubble never eats clicks");
  assert.ok(!source.includes('"use client"'), "a css tooltip needs no client boundary");
  assert.ok(
    !source.includes("useState") && !source.includes("useEffect"),
    "no tooltip state machine hides in the primitive",
  );
});

test("the bubble sits below the trigger with the contract geometry", () => {
  const source = hint();
  for (const pin of ["relative", "top-full", "mt-2", "z-30", "w-64", "rotate-45"]) {
    assert.ok(source.includes(pin), `missing ${pin}`);
  }
  assert.ok(
    source.includes("border-l border-t") && source.includes("bg-surface-raised"),
    "the arrow is a rotated square sharing the bubble chrome",
  );
});

test("align variants position both the bubble and its arrow", () => {
  const source = hint();
  assert.ok(
    source.includes('center: { bubble: "left-1/2 -translate-x-1/2", arrow: "left-1/2 -translate-x-1/2" }'),
    "center centers bubble and arrow",
  );
  assert.ok(
    source.includes('left: { bubble: "left-0", arrow: "left-3" }'),
    "left pins the bubble to the trigger start",
  );
  assert.ok(
    source.includes('right: { bubble: "right-0", arrow: "right-3" }'),
    "right pins the bubble to the trigger end",
  );
  assert.ok(
    source.includes("ALIGN_CLASSES[align]"),
    "the variant map actually drives the markup",
  );
});

test("the trigger is a labeled button described by the bubble", () => {
  const source = hint();
  assert.ok(source.includes('type="button"'), "the trigger is a real button");
  assert.ok(source.includes("aria-describedby"), "the bubble is announced as the description");
  assert.ok(source.includes('role="tooltip"'), "the bubble carries the tooltip role");
  assert.ok(
    source.includes("focus-visible"),
    "keyboard focus is visibly indicated on the trigger",
  );
});
