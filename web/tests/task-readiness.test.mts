import { strict as assert } from "node:assert";
import { test } from "node:test";
import { taskReadiness } from "../src/features/planning/lib/task-readiness.ts";
import type { PlanningFeatureBoardItem } from "../src/features/planning/types.ts";

const item = (overrides: Partial<PlanningFeatureBoardItem>): PlanningFeatureBoardItem => ({
  slug: "044-demo",
  title: "Demo feature",
  briefExcerpt: null,
  status: "inProgress",
  phase: "in progress",
  priority: "p2",
  queuePosition: 1,
  isBlocked: false,
  version: 1,
  specStatus: "validated",
  planStatus: "validated",
  taskTotal: 10,
  taskDone: 4,
  taskCounts: { todo: 2, inProgress: 3, review: 1, done: 4, blocked: 0 },
  updatedAt: "2026-10-21T12:00:00Z",
  claimedByAlias: null,
  claimedByVendor: null,
  claimedAt: null,
  claimState: null,
  claimedByHuman: false,
  ...overrides,
});

test("full counts yield five ordered segments with the status tones and proportional fractions", () => {
  const readiness = taskReadiness(item({}));
  assert.ok(readiness !== null);
  assert.deepEqual(readiness.segments, [
    { tone: "neutral", count: 2, fraction: 0.2 },
    { tone: "warning", count: 3, fraction: 0.3 },
    { tone: "success", count: 1, fraction: 0.1 },
    { tone: "primary", count: 4, fraction: 0.4 },
    { tone: "danger", count: 0, fraction: 0 },
  ]);
});

test("counter line omits blocked when there is none", () => {
  const readiness = taskReadiness(item({}));
  assert.ok(readiness !== null);
  assert.equal(readiness.countersLine, "todo 2|10 · in-progress 3|10 · review 1|10 · done 4|10");
});

test("aria value text names every count including zero blocked", () => {
  const readiness = taskReadiness(item({}));
  assert.ok(readiness !== null);
  assert.equal(readiness.ariaValueText, "2 todo, 3 in progress, 1 in review, 4 done, 0 blocked");
});

test("counter line appends blocked only when blocked is above zero", () => {
  const readiness = taskReadiness(item({ taskCounts: { todo: 1, inProgress: 2, review: 3, done: 2, blocked: 2 } }));
  assert.ok(readiness !== null);
  assert.equal(
    readiness.countersLine,
    "todo 1|10 · in-progress 2|10 · review 3|10 · done 2|10 · blocked 2|10",
  );
});

test("zero taskTotal yields no readiness", () => {
  assert.equal(
    taskReadiness(item({ taskTotal: 0, taskDone: 0, taskCounts: { todo: 0, inProgress: 0, review: 0, done: 0, blocked: 0 } })),
    null,
  );
});

test("missing counts fall back to done vs rest with fractions of the total", () => {
  const readiness = taskReadiness(item({ taskCounts: undefined }));
  assert.ok(readiness !== null);
  assert.deepEqual(readiness.segments, [
    { tone: "primary", count: 4, fraction: 0.4 },
    { tone: "neutral", count: 6, fraction: 0.6 },
  ]);
  assert.equal(readiness.countersLine, "done 4|10 · rest 6|10");
  assert.equal(readiness.ariaValueText, "4 done, 6 remaining");
});

test("fully done feature renders a full done segment over an empty rest", () => {
  const readiness = taskReadiness(item({ taskDone: 10, taskCounts: undefined }));
  assert.ok(readiness !== null);
  assert.deepEqual(readiness.segments, [
    { tone: "primary", count: 10, fraction: 1 },
    { tone: "neutral", count: 0, fraction: 0 },
  ]);
});
