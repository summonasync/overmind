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
const addDialog = () =>
  readFileSync(
    new URL("../src/features/lanes/components/lane-add-dialog.tsx", import.meta.url),
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

test("create posts the single lane onto the collection route", () => {
  const body = functionBody(lanesApi(), "createLane");
  assert.ok(body.includes("apiPost<LanesRoster>"));
  assert.ok(
    body.includes("`/projects/${projectId}/planning/lanes`"),
    "POST must land on the collection route, never on a {name} segment",
  );
  assert.ok(body.includes(", lane)"), "the whole lane, name included, is the create body");
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
  const mutations = laneMutations();
  assert.ok(!mutations.includes("setLanes"), "the wholesale PUT is out of the mutation path");
  assert.ok(!mutations.includes("lane-roster"));
  assert.ok(mutations.includes("createLane(projectId, lane)"));
  assert.ok(mutations.includes("updateLane(projectId, lane)"));
  assert.ok(mutations.includes("deleteLane(projectId, name)"));
});

test("a lane mutation invalidates the board cache, not only the roster", () => {
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
    3,
    "create, update and delete each run the same invalidation",
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
  const roster = laneRoster();
  assert.ok(roster.includes('trimmed.includes("/")'));
  assert.ok(roster.includes('trimmed.includes("\\\\")'));
  assert.ok(roster.includes("isAddressableLaneName"));
  assert.ok(addDialog().includes("LANE_NAME_SHAPE_HINT"), "the rule must be visible in the form");
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
  const withChips = [addDialog(), editDialog(), priorityPicker()].filter((source) =>
    source.includes("aria-pressed"),
  );
  assert.equal(withChips.length, 1, "the chip group lives in lane-priority-picker only");
  assert.ok(addDialog().includes("<LanePriorityPicker"));
  assert.ok(editDialog().includes("<LanePriorityPicker"));
});
