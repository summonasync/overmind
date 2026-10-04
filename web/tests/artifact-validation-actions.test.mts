import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const workspace = () =>
  readFileSync(
    new URL("../src/features/planning/components/feature-workspace.tsx", import.meta.url),
    "utf8",
  );
const planningApi = () =>
  readFileSync(new URL("../src/features/planning/api.ts", import.meta.url), "utf8");
const planningMutations = () =>
  readFileSync(
    new URL("../src/features/planning/lib/use-planning.ts", import.meta.url),
    "utf8",
  );

test("document views derive frozen purely from artifact status", () => {
  const source = workspace();
  assert.equal(
    source.split(`artifact.status === "validated"`).length - 1,
    1,
    "single status derivation drives the validation actions",
  );
  assert.ok(source.includes("const frozen = artifact.status === \"validated\";"));
});

test("draft document views host Approve and Decline next to Edit", () => {
  const source = workspace();
  const gate = source.indexOf("{!frozen ? (");
  assert.notEqual(gate, -1, "validation actions are gated on the frozen derivation");
  const gateEnd = source.indexOf(") : null}", gate);
  const gated = source.slice(gate, gateEnd);

  assert.ok(gated.includes(`{approvePending ? "Approving…" : "Approve"}`));
  assert.ok(gated.includes(`{declinePending ? "Declining…" : "Decline"}`));
  assert.ok(gated.includes(`variant="accent"`));
  assert.ok(gated.includes(`variant="secondary"`));
});

test("view-level Approve reuses the human validate mutation without the editor", () => {
  const source = workspace();
  assert.ok(source.includes(`onApprove={() =>`));
  assert.ok(source.includes(`validateArtifact.mutate({ artifactId: documentArtifact.id, slug })`));
  assert.ok(source.includes(`approvePending={validateArtifact.isPending}`));
  assert.ok(source.includes(`approveError={validateArtifact.error as Error | null}`));
});

test("Decline prompt flows the optional reason into the decline mutation", () => {
  const source = workspace();
  assert.ok(source.includes(`declineArtifact.mutate(`));
  assert.ok(source.includes(`{ artifactId: declineTarget.id, slug, reason }`));
  assert.ok(source.includes(`declinePending={declineArtifact.isPending}`));

  const dialog = readFileSync(
    new URL("../src/features/planning/components/artifact-decline-dialog.tsx", import.meta.url),
    "utf8",
  );
  assert.ok(dialog.includes("Reason (optional)"));
  assert.ok(dialog.includes("maxLength={500}"));
  assert.ok(dialog.includes("onConfirm(trimmed.length > 0 ? trimmed : undefined)"));
});

test("decline targets the task-01 human route", () => {
  const source = planningApi();
  assert.ok(source.includes("/planning/artifacts/${artifactId}/decline"));

  const mutations = planningMutations();
  assert.ok(mutations.includes("declinePlanningArtifact(input.artifactId, input.reason)"));
  assert.ok(mutations.includes("declineArtifact,"));
});

test("frozen views lose both actions and the editor keeps its own Approve", () => {
  const source = workspace();
  const gate = source.indexOf("{!frozen ? (");
  const gateEnd = source.indexOf(") : null}", gate);
  const gated = source.slice(gate, gateEnd);
  assert.ok(gated.includes("Approve") && gated.includes("Decline"));

  const editor = readFileSync(
    new URL("../src/features/planning/components/artifact-editor.tsx", import.meta.url),
    "utf8",
  );
  assert.ok(editor.includes("{initial && !frozen ? ("));
  assert.ok(editor.includes(`approvePending ? "Approving…" : "Approve"`));
});

test("task views stay out of the validation flow", () => {
  const board = readFileSync(
    new URL("../src/features/planning/components/task-board.tsx", import.meta.url),
    "utf8",
  );
  assert.ok(!board.toLowerCase().includes("decline"));
  assert.ok(!board.toLowerCase().includes("approve"));
});
