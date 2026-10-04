import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  artifactDeleteLabel,
  artifactDeletePreview,
} from "../src/features/planning/lib/artifact-deletion.ts";
import type { PlanningArtifactResponse } from "../src/features/planning/types.ts";

let sequence = 0;
const task = (overrides: Partial<PlanningArtifactResponse>): PlanningArtifactResponse => ({
  id: `artifact-${sequence++}`,
  kind: "task",
  status: "todo",
  review: "light",
  order: 1,
  body: "# Task one",
  brief: null,
  frozenAt: null,
  version: 1,
  createdAt: "2026-10-04T09:00:00Z",
  updatedAt: "2026-10-04T09:00:00Z",
  log: [],
  ...overrides,
});

test("task preview names the task and lists the removal consequences", () => {
  const target = task({ order: 2, body: "# Plan the wave" });
  const siblings = [target, task({ order: 3 })];

  const preview = artifactDeletePreview(target, siblings);

  assert.equal(preview.target, 'task #2 "Plan the wave"');
  assert.equal(preview.target, artifactDeleteLabel(target));
  assert.equal(preview.consequences.length, 3);
  assert.ok(
    preview.consequences.some((line) => line.includes("disappear from the feature page")),
  );
  assert.ok(preview.consequences.some((line) => line.includes("version history")));
  assert.ok(preview.consequences.some((line) => line.includes("BACKLOG file")));
  assert.deepEqual(preview.warnings, []);
});

test("document preview uses the bare kind and never reports task dependents", () => {
  const spec = task({ kind: "spec", order: 0 });
  const dependent = task({ order: 4, blockedBy: [0] });

  const preview = artifactDeletePreview(spec, [spec, dependent]);

  assert.equal(preview.target, "spec");
  assert.deepEqual(preview.warnings, []);
});

test("blocked dependents produce the dependencies-first warning", () => {
  const blocker = task({ order: 1 });
  const dependent = task({ order: 2, body: "# Wire the dialog", blockedBy: [1] });

  const preview = artifactDeletePreview(blocker, [blocker, dependent]);

  assert.equal(preview.warnings.length, 1);
  assert.match(preview.warnings[0], /Clean up dependencies first/);
  assert.match(preview.warnings[0], /#2 Wire the dialog/);
});

test("validated artifacts carry a permanence warning", () => {
  const spec = task({ kind: "spec", order: 0, frozenAt: "2026-10-03T20:00:00Z" });

  const preview = artifactDeletePreview(spec, [spec]);

  assert.equal(preview.warnings.length, 1);
  assert.match(preview.warnings[0], /validated/);
  assert.match(preview.warnings[0], /permanent/);
});
