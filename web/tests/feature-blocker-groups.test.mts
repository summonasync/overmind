import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  blockerChipClasses,
  CLEARED_BY_HEADING,
  splitBlockersByClosure,
  WAITS_FOR_HEADING,
  waitsForPlaceholder,
} from "../src/features/planning/lib/feature-blocker-groups.ts";
import type { PlanningFeatureDependencyEntry } from "../src/features/planning/types.ts";

const entry = (overrides: Partial<PlanningFeatureDependencyEntry>): PlanningFeatureDependencyEntry => ({
  slug: "042-demo",
  status: "done",
  closed: true,
  ...overrides,
});

test("group headings pin the wait and cleared wording", () => {
  assert.equal(WAITS_FOR_HEADING, "Waits for");
  assert.equal(CLEARED_BY_HEADING, "Cleared by");
});

test("split keeps open blockers waiting and moves closed ones out", () => {
  const { waiting, cleared } = splitBlockersByClosure([
    entry({ slug: "028-open", status: "inProgress", closed: false }),
    entry({ slug: "027-done", status: "done", closed: true }),
    entry({ slug: "026-review", status: "review", closed: false }),
    entry({ slug: "025-done", status: "done", closed: true }),
  ]);

  assert.deepEqual(waiting.map((item) => item.slug), ["028-open", "026-review"]);
  assert.deepEqual(cleared.map((item) => item.slug), ["027-done", "025-done"]);
});

test("open badge counts only unclosed blockers", () => {
  const { waiting } = splitBlockersByClosure([
    entry({ slug: "a", status: "review", closed: false }),
    entry({ slug: "b", status: "done", closed: true }),
    entry({ slug: "c", status: "done", closed: true }),
  ]);

  assert.equal(waiting.length, 1);
});

test("empty blocker list splits into two empty groups", () => {
  assert.deepEqual(splitBlockersByClosure([]), { waiting: [], cleared: [] });
});

test("open blockers keep the danger chip styling", () => {
  const classes = blockerChipClasses(false);
  assert.ok(classes.includes("border-danger/40"));
  assert.ok(classes.includes("bg-danger/10"));
  assert.ok(classes.includes("text-danger"));
  assert.ok(!classes.includes("border-border/50"));
  assert.ok(!classes.includes("muted"));
});

test("cleared blockers drop the danger tone for a muted chip", () => {
  const classes = blockerChipClasses(true);
  assert.ok(classes.includes("border-border/50"));
  assert.ok(classes.includes("text-muted/70"));
  assert.ok(!classes.includes("danger"));
});

test("no blockers at all reads nothing yet", () => {
  assert.equal(waitsForPlaceholder(0, 0), "nothing yet");
});

test("fully cleared waits-for reads nothing open", () => {
  assert.equal(waitsForPlaceholder(3, 0), "nothing open");
});

test("any open blocker suppresses the placeholder", () => {
  assert.equal(waitsForPlaceholder(2, 2), null);
  assert.equal(waitsForPlaceholder(3, 1), null);
});
