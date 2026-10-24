import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import {
  decideDoneGate,
  doneGateRefusal,
  doneGateDialogTitle,
  featureResultTabHref,
  missingResultCopy,
  unvalidatedResultCopy,
} from "../src/features/planning/lib/done-gate.ts";
import type { PlanningArtifactResponse } from "../src/features/planning/types.ts";

const board = () =>
  readFileSync(
    new URL("../src/features/planning/components/feature-board.tsx", import.meta.url),
    "utf8",
  );
const dialog = () =>
  readFileSync(
    new URL("../src/features/planning/components/feature-done-dialog.tsx", import.meta.url),
    "utf8",
  );
const approveHook = () =>
  readFileSync(
    new URL("../src/features/planning/lib/use-approve-and-done.ts", import.meta.url),
    "utf8",
  );
const apiClient = () =>
  readFileSync(new URL("../src/shared/api/client.ts", import.meta.url), "utf8");

const artifact = (overrides: Partial<PlanningArtifactResponse>): PlanningArtifactResponse => ({
  id: "a1",
  kind: "task",
  status: "todo",
  review: "light",
  order: 0,
  body: "",
  frozenAt: null,
  version: 1,
  createdAt: "2026-10-24T00:00:00Z",
  updatedAt: "2026-10-24T00:00:00Z",
  log: [],
  ...overrides,
});

const withCode = (code: string, message: string): Error =>
  Object.assign(new Error(message), { code });

test("no result artifact decides the missing-result info scenario", () => {
  assert.deepEqual(decideDoneGate([artifact({ kind: "spec", status: "validated" })]), {
    kind: "missing-result",
  });
  assert.deepEqual(decideDoneGate([]), { kind: "missing-result" });
});

test("draft result decides the unvalidated confirm scenario and carries the artifact id", () => {
  assert.deepEqual(
    decideDoneGate([artifact({ kind: "result", status: "draft", id: "res-7" })]),
    { kind: "unvalidated-result", resultArtifactId: "res-7" },
  );
});

test("validated result stays a plain done tap", () => {
  assert.deepEqual(decideDoneGate([artifact({ kind: "result", status: "validated" })]), {
    kind: "ready",
  });
});

test("non-result artifacts never decide the gate", () => {
  const tasks = [
    artifact({ kind: "task", status: "done", id: "t1" }),
    artifact({ kind: "task", status: "draft", id: "t2" }),
  ];
  assert.deepEqual(decideDoneGate(tasks), { kind: "missing-result" });
});

test("the result-tab link lands on the workspace result tab", () => {
  assert.equal(
    featureResultTabHref("p-guid", "160-done-gate-feedback"),
    "/dashboard?project=p-guid&feature=160-done-gate-feedback&tab=result",
  );
});

test("info copy names the missing result, confirm copy offers the one-go approve", () => {
  assert.match(missingResultCopy("Demo feature"), /no result artifact/);
  assert.match(missingResultCopy("Demo feature"), /Demo feature/);
  assert.match(unvalidatedResultCopy("Demo feature"), /still draft/);
  assert.match(unvalidatedResultCopy("Demo feature"), /one go/);
});

test("refusal mapping answers only the two server done-gate codes", () => {
  const message = "Feature '042-demo' has no result artifact.";
  assert.equal(
    doneGateRefusal(withCode("FeatureResultRequired", message)),
    message,
  );
  assert.equal(
    doneGateRefusal(withCode("FeatureDoneTapRequired", message)),
    message,
  );
  assert.equal(doneGateRefusal(withCode("InvalidFeatureTransition", message)), null);
  assert.equal(doneGateRefusal(new Error(message)), null);
  assert.equal(doneGateRefusal("FeatureResultRequired"), null);
  assert.equal(doneGateRefusal(null), null);
});

test("dialog titles stay distinct per scenario", () => {
  assert.equal(doneGateDialogTitle("missing-result"), "Done needs a result");
  assert.equal(doneGateDialogTitle("unvalidated-result"), "Approve & done");
  assert.equal(doneGateDialogTitle("refused"), "Done was refused");
});

test("done selection prefetches the feature and routes the gate decision", () => {
  const source = board();
  assert.ok(source.includes("queryClient.fetchQuery(featureQueryOptions(projectId, item.slug))"));
  assert.ok(source.includes("decideDoneGate(feature.artifacts)"));
  const requestDone = source.slice(source.indexOf("const requestDone"), source.indexOf("const handleStatusChange"));
  assert.ok(requestDone.includes(`setStatus.mutate("done")`));
  assert.ok(requestDone.includes(`scenario: "missing-result"`));
  assert.ok(requestDone.includes(`scenario: "unvalidated-result"`));
});

test("non-done status changes bypass the gate entirely", () => {
  const source = board();
  const handler = source.slice(
    source.indexOf("const handleStatusChange"),
    source.indexOf("const approve = () => {"),
  );
  assert.ok(handler.includes(`if (status !== "done")`));
  assert.ok(handler.includes("setStatus.mutate(status)"));
});

test("Approve & done validates the result first, then flips done", () => {
  const source = approveHook();
  const validate = source.indexOf("await validatePlanningArtifact(resultArtifactId);");
  const done = source.indexOf(`await updatePlanningFeature(projectId, slug, { status: "done" });`);
  assert.notEqual(validate, -1, "the validate door fires first");
  assert.notEqual(done, -1, "the done door fires second");
  assert.ok(validate < done, "validate precedes done inside one interaction");
});

test("approve success invalidates the board and the feature cache", () => {
  const source = approveHook();
  assert.ok(source.includes("queryClient.invalidateQueries({ queryKey: planningKeys.lists() })"));
  assert.ok(
    source.includes("queryClient.invalidateQueries({ queryKey: planningKeys.feature(projectId, slug) })"),
  );
});

test("the approve button is the primary action and disables while pending", () => {
  const source = dialog();
  const button = source.slice(source.indexOf(`scenario === "unvalidated-result" ? (`));
  assert.ok(button.includes(`variant="primary"`));
  assert.ok(button.includes(`disabled={pending}`));
  assert.ok(button.includes(`{pending ? "Approving & done…" : "Approve & done"}`));
});

test("cancel and dismissal only reset the gate state", () => {
  const source = board();
  const closeGate = source.slice(
    source.indexOf("const closeGate"),
    source.indexOf("const requestDone"),
  );
  assert.ok(closeGate.includes("setGate(null)"));
  assert.ok(closeGate.includes("setStatus.reset()"));
  assert.ok(closeGate.includes("approveAndDone.reset()"));
  assert.ok(!closeGate.includes(".mutate("), "closing the gate never mutates");

  const dialogSource = dialog();
  const cancel = dialogSource.slice(
    dialogSource.indexOf("flex justify-end gap-2"),
    dialogSource.indexOf("</div>\n    </div>"),
  );
  assert.ok(cancel.includes(`variant="secondary"`));
  assert.ok(cancel.includes("onClick={onCancel}"));
});

test("a refusal race reopens the gate dialog with the server text", () => {
  const source = board();
  const onError = source.slice(
    source.indexOf("onError: (error, _status, context) => {"),
    source.indexOf("onSettled:"),
  );
  assert.ok(onError.includes("doneGateRefusal(error)"));
  assert.ok(onError.includes(`scenario: "refused"`));

  const dialogSource = dialog();
  const refused = dialogSource.slice(
    dialogSource.indexOf(`scenario === "refused"`),
    dialogSource.indexOf("<ErrorText error={scenario === \"refused\" ? null : error} />"),
  );
  assert.ok(refused.includes("<ErrorText error={new Error(refusal)} />"));
});

test("the dialog link points at the result tab of the same feature", () => {
  const source = dialog();
  assert.ok(source.includes("href={featureResultTabHref(projectId, slug)}"));
});

test("ApiError carries the ProblemDetails title as the failure code", () => {
  const source = apiClient();
  assert.ok(source.includes("readonly code: string | null;"));
  assert.ok(source.includes("const code = typeof record.title === \"string\" && record.title.length > 0 ? record.title : null;"));
  assert.ok(source.includes("new ApiError(response.status, record.detail, code)"));
});

test("the gate dialog stays a popup, not a toast", () => {
  const source = board();
  assert.ok(source.includes("<Modal"));
  assert.ok(source.includes("<FeatureDoneDialog"));
  assert.ok(!source.includes("toast"));
});
