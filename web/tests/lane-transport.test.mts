import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const planningApi = () =>
  readFileSync(new URL("../src/features/planning/api.ts", import.meta.url), "utf8");
const laneAssignment = () =>
  readFileSync(
    new URL("../src/features/lanes/lib/use-feature-lane.ts", import.meta.url),
    "utf8",
  );
const laneActions = () =>
  readFileSync(
    new URL("../src/features/lanes/components/feature-lane-actions.tsx", import.meta.url),
    "utf8",
  );
const selectPrimitive = () => readFileSync(new URL("../src/shared/ui/select.tsx", import.meta.url), "utf8");
const planningTypes = () =>
  readFileSync(new URL("../src/features/planning/types.ts", import.meta.url), "utf8");

function interfaceBody(name: string): string {
  const source = planningTypes();
  const start = source.indexOf(`export interface ${name} {`);
  assert.notEqual(start, -1, `types.ts must still declare ${name}`);
  return source.slice(start, source.indexOf("\n}", start));
}

test("the PUT transport folds an absent lane into the server no-op with ??, never ||", () => {
  const source = planningApi();
  assert.ok(
    source.includes("lane: values.lane ?? null"),
    "lane must reach the wire as null when the caller omitted it",
  );
  assert.ok(!source.includes("lane: values.lane || null"));
});

test("the value that clears a lane assignment is the empty string", () => {
  const source = laneAssignment();
  assert.ok(
    source.includes('export const UNASSIGNED_LANE = "";'),
    "null means leave-it-alone on the server, so clearing needs the empty string",
  );
});

test("the assignment mutation forwards the lane verbatim, without coercing the sentinel", () => {
  const source = laneAssignment();
  assert.ok(source.includes("updatePlanningFeature(projectId, slug, { lane })"));
});

test("remove from lane sends the sentinel and move sends the picked roster name", () => {
  const source = laneActions();
  assert.ok(source.includes("assignment.mutate({ slug, lane: UNASSIGNED_LANE })"));
  assert.ok(source.includes("assignment.mutate({ slug, lane: event.target.value })"));
});

test("no lane call-site hands the transport the no-op literal", () => {
  for (const source of [laneAssignment(), laneActions()]) {
    assert.ok(!source.includes("lane: null"));
  }
});

test("the lane picker asks the primitive for the compact size, never overrides density", () => {
  const source = laneActions();
  const picker = source.slice(source.indexOf("<Select"), source.indexOf("</Select>"));
  assert.ok(picker.includes('size="sm"'), "the compact density must come from the Select size prop");
  assert.ok(!source.includes("px-1.5"), "cn() concatenates, a density override here is dead code");
  assert.ok(!source.includes("py-0.5"), "cn() concatenates, a density override here is dead code");
});

test("the select primitive owns density in SIZE_CLASSES with md as the default", () => {
  const source = selectPrimitive();
  assert.ok(source.includes("const SIZE_CLASSES = {"));
  assert.ok(source.includes('sm: "px-1.5 py-0.5 text-xs",'));
  assert.ok(source.includes('md: "px-2.5 py-1.5 text-sm",'));
  assert.ok(source.includes('size = "md"'), "existing md consumers must keep their density by default");
  assert.ok(source.includes("SIZE_CLASSES[size]"), "the base cn() must not carry hardcoded density");
  assert.equal(
    source.split("px-2.5").length - 1,
    1,
    "px-2.5 belongs to SIZE_CLASSES.md only, so md keeps its exact token set",
  );
});

test("the update contract cannot express the wire no-op in a lane field", () => {
  const body = interfaceBody("UpdatePlanningFeatureValues");
  assert.ok(!body.includes("lane?: string | null"), "null means leave-it-alone on the wire");
  assert.ok(body.includes("lane?: string;"));
});

test("the board read model keeps a null lane because the server sends one", () => {
  assert.ok(interfaceBody("PlanningFeatureBoardItem").includes("lane?: string | null"));
});
