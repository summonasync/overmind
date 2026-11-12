import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const lanesApi = () => readFileSync(new URL("../src/features/lanes/api.ts", import.meta.url), "utf8");
const laneMutations = () =>
  readFileSync(
    new URL("../src/features/lanes/lib/use-lane-mutations.ts", import.meta.url),
    "utf8",
  );
const laneRoster = () =>
  readFileSync(new URL("../src/features/lanes/lib/lane-roster.ts", import.meta.url), "utf8");
// 32dbb47 deleted lane-add-dialog; lane birth lives in feature-lane-actions now.
const laneActions = () =>
  readFileSync(
    new URL("../src/features/lanes/components/feature-lane-actions.tsx", import.meta.url),
    "utf8",
  );
const laneAssignment = () =>
  readFileSync(
    new URL("../src/features/lanes/lib/use-feature-lane.ts", import.meta.url),
    "utf8",
  );
const editDialog = () =>
  readFileSync(
    new URL("../src/features/lanes/components/lane-edit-dialog.tsx", import.meta.url),
    "utf8",
  );
const deleteDialog = () =>
  readFileSync(
    new URL("../src/features/lanes/components/lane-delete-dialog.tsx", import.meta.url),
    "utf8",
  );
const laneCard = () =>
  readFileSync(new URL("../src/features/lanes/components/lane-card.tsx", import.meta.url), "utf8");
const laneBoard = () =>
  readFileSync(new URL("../src/features/lanes/components/lane-board.tsx", import.meta.url), "utf8");
const priorityPicker = () =>
  readFileSync(
    new URL("../src/features/lanes/components/lane-priority-picker.tsx", import.meta.url),
    "utf8",
  );

function functionBody(source: string, name: string): string {
  const start = source.indexOf(`function ${name}(`);
  assert.notEqual(start, -1, `source must still declare ${name}`);
  return source.slice(start, source.indexOf("\n}", start) + 2);
}

test("a new lane is born through the feature-lane assignment, name pre-shaped by the roster rule", () => {
  // 32dbb47 removed POST lanes and createLane; birth rides the feature update with the lane field.
  const actions = laneActions();
  const assignment = laneAssignment();
  assert.ok(
    actions.includes("validateLaneName(name, lanes)"),
    "the name is shaped by the roster rule before any transport",
  );
  assert.ok(
    actions.includes("assignment.mutate({ slug, lane: name }"),
    "birth rides the feature-lane assignment, not a lanes POST",
  );
  assert.ok(
    assignment.includes("updatePlanningFeature(projectId, slug, { lane })"),
    "the transport is the feature update carrying the lane field only",
  );
});

test("the wholesale roster write lives on its sibling literal", () => {
  const body = functionBody(lanesApi(), "setLanes");
  assert.ok(
    body.includes("`/projects/${projectId}/planning/lanes-roster`"),
    "the collection route answers a typed refusal now; the sibling literal is the only whole-roster door",
  );
});

test("update and delete address the lane by an encoded path segment", () => {
  const api = lanesApi();
  assert.ok(functionBody(api, "lanePath").includes("planning/lanes/${encodeURIComponent(name)}"));
  assert.ok(functionBody(api, "updateLane").includes("apiPut<LanesRoster>(lanePath("));
  assert.ok(functionBody(api, "deleteLane").includes("apiDelete<LanesRoster>(lanePath("));
});

test("the granular PUT replaces the complete trio and never carries the name", () => {
  const fields = functionBody(lanesApi(), "laneFields");
  assert.ok(fields.includes("contour: lane.contour"));
  assert.ok(fields.includes("serialOrder: lane.serialOrder"));
  assert.ok(fields.includes("priority: lane.priority"));
  assert.ok(!fields.includes("name"), "name is the identity in the path, not a body field");
  assert.ok(functionBody(lanesApi(), "updateLane").includes("laneFields(lane)"));
});

test("no lane write rebuilds the roster on the client anymore", () => {
  // 32dbb47 took the create mutation with POST lanes; the mutation path is the two granular writes only.
  const mutations = laneMutations();
  assert.ok(!mutations.includes("setLanes"), "the wholesale PUT is out of the mutation path");
  assert.ok(!mutations.includes("lane-roster"));
  assert.ok(!mutations.includes("createLane"), "birth no longer goes through the lanes API");
  assert.ok(mutations.includes("updateLane(projectId, lane)"));
  assert.ok(mutations.includes("deleteLane(projectId, name)"));
});

test("a lane mutation invalidates the board cache, not only the roster", () => {
  // 32dbb47 dropped the create mutation; the shared invalidate now covers the two granular writes.
  const source = laneMutations();
  const start = source.indexOf("const invalidate = () => {");
  assert.notEqual(start, -1, "the three lane mutations must share one invalidate closure");
  const closure = source.slice(start, source.indexOf("\n  };", start));
  assert.ok(closure.includes("lanesKeys.project(projectId)"));
  assert.ok(
    closure.includes("planningKeys.board(projectId)"),
    "deleting a lane unassigns features, so the board rows go stale too",
  );
  assert.equal(
    source.split("onSuccess: invalidate").length - 1,
    2,
    "update and delete each run the same invalidation",
  );
});

test("the roster rebuild helpers are gone and the validators stay", () => {
  const roster = laneRoster();
  for (const dead of ["appendLane", "removeLane", "toUpsertRows"]) {
    assert.ok(!roster.includes(dead), `${dead} had one consumer and left with the rewiring`);
  }
  assert.ok(roster.includes("export function validateLaneName"));
});

test("the form mirrors the domain name rule instead of eating a raw 400", () => {
  // 32dbb47 deleted lane-add-dialog; the surviving birth form is NewLaneForm in feature-lane-actions.
  const roster = laneRoster();
  const actions = laneActions();
  assert.ok(roster.includes('trimmed.includes("/")'));
  assert.ok(roster.includes('trimmed.includes("\\\\")'));
  assert.ok(roster.includes("isAddressableLaneName"));
  assert.ok(
    actions.includes("validateLaneName(name, lanes)"),
    "the surviving form still mirrors the rule client-side",
  );
  assert.ok(actions.includes("maxLength={LANE_NAME_MAX_LENGTH}"), "the cap comes from the same rule module");
  assert.ok(actions.includes('role="alert"'), "the rule verdict renders inline, never as a raw 400");
});

test("the edit modal prefills from the roster row and keeps the name immutable", () => {
  const dialog = editDialog();
  assert.ok(dialog.includes('useState(lane.contour ?? "")'));
  assert.ok(dialog.includes('lane.serialOrder === null ? "" : String(lane.serialOrder)'));
  assert.ok(dialog.includes("lanePriorityFromValue(lane.priority)"));
  assert.ok(dialog.includes("readOnly"), "rename is delete-and-recreate, per T0");
  assert.ok(dialog.includes("name: lane.name"), "the path identity comes from the row, not the form");
});

test("the delete confirm still states the unassign consequence", () => {
  const dialog = deleteDialog();
  assert.ok(dialog.includes("become unassigned"));
  assert.ok(dialog.includes("&quot;Without lane&quot;"));
  assert.ok(
    dialog.includes("removeLane.mutate(lane.name,"),
    "the granular DELETE needs the name only, never a rebuilt roster",
  );
  assert.ok(!dialog.includes("roster"), "delete no longer needs the whole roster to remove one lane");
});

test("the lane card carries an edit affordance next to delete", () => {
  const card = laneCard();
  assert.ok(card.includes("aria-label={`Edit lane ${lane.name}`}"));
  assert.ok(card.includes("onEdit(lane)"));
  assert.ok(laneBoard().includes("onEdit={setEditingLane}"));
  assert.ok(laneBoard().includes("<LaneEditDialog"));
});

test("the board copy stopped promising a whole-roster replace", () => {
  const board = laneBoard();
  assert.ok(!board.includes("replace the whole roster"));
  assert.ok(board.includes("edits or deletes its own lane"));
});

test("the priority chip group has exactly one implementation", () => {
  // 32dbb47 deleted lane-add-dialog; the scan guards the surviving dialog against a hand-rolled group.
  const withChips = [editDialog(), priorityPicker()].filter((source) => source.includes("aria-pressed"));
  assert.equal(withChips.length, 1, "the chip group lives in lane-priority-picker only");
  assert.ok(editDialog().includes("<LanePriorityPicker"));
});
