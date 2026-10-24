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
const laneOrder = () =>
  readFileSync(new URL("../src/features/lanes/lib/lane-order.ts", import.meta.url), "utf8");
const planningBarrel = () =>
  readFileSync(new URL("../src/features/planning/index.ts", import.meta.url), "utf8");
const laneBoard = () =>
  readFileSync(new URL("../src/features/lanes/components/lane-board.tsx", import.meta.url), "utf8");
const laneCard = () =>
  readFileSync(new URL("../src/features/lanes/components/lane-card.tsx", import.meta.url), "utf8");
const moveControls = () =>
  readFileSync(
    new URL("../src/features/lanes/components/lane-feature-move.tsx", import.meta.url),
    "utf8",
  );
const moveHook = () =>
  readFileSync(new URL("../src/features/lanes/lib/use-feature-queue-move.ts", import.meta.url), "utf8");
const lanesTypes = () =>
  readFileSync(new URL("../src/features/lanes/types.ts", import.meta.url), "utf8");

function interfaceBody(name: string): string {
  const source = planningTypes();
  const start = source.indexOf(`export interface ${name} {`);
  assert.notEqual(start, -1, `types.ts must still declare ${name}`);
  return source.slice(start, source.indexOf("\n}", start));
}

function interfaceBodyOf(source: string, name: string, file: string): string {
  const start = source.indexOf(`export interface ${name} {`);
  assert.notEqual(start, -1, `${file} must still declare ${name}`);
  return source.slice(start, source.indexOf("\n}", start));
}

function functionBody(source: string, signature: string, file: string): string {
  const start = source.indexOf(signature);
  assert.notEqual(start, -1, `${file} must still declare ${signature}`);
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

test("the queue-position move goes through the human door with the shared move literal", () => {
  const source = planningApi();
  assert.ok(
    source.includes("/planning/features/${slug}/queue-move"),
    "one action name on both doors: the agent MCP tool already posts to queue-move",
  );
  assert.ok(
    source.includes("position: values.position ?? null"),
    "the validator forbids position for every move but toPosition, so an absent one must reach the wire as null",
  );
  assert.ok(!source.includes("}/move"), "the pre-rename /move literal must not come back");
});

test("lane columns and the unlaned pool share the dashboard comparator", () => {
  const source = laneOrder();
  assert.ok(
    source.includes('import { compareBoardItems } from "@/features/planning";'),
    "the comparator is reached through the slice barrel, never through its internals",
  );
  assert.ok(!source.includes('from "@/features/planning/lib/'), "deep import into a sibling slice is an FSD breach");
  assert.ok(source.includes("compareBoardItems(left, right)"), "lane features sort by their board item");
  assert.ok(source.includes("[...items].sort(compareBoardItems)"), "the pool sorts by the same single rule");
  assert.ok(!source.includes("featureOrderNumber"), "priority then feature number is the superseded 141 rule");
  assert.ok(!source.includes("feature-number"), "no feature-number ranking may survive next to the comparator");
});

test("the dashboard comparator is exported from the planning barrel", () => {
  assert.ok(
    planningBarrel().includes('export { compareBoardItems } from "./lib/board-order";'),
    "lane-order must consume the slice public API, which is why the barrel carries it",
  );
});

test("queue position is joined from the board, not invented as a roster field", () => {
  const summary = interfaceBodyOf(lanesTypes(), "LaneFeatureSummary", "lanes/types.ts");
  assert.ok(
    !summary.includes("queuePosition"),
    "the Host roster DTO stays untouched; the columns already need the board for priority",
  );
  assert.ok(laneOrder().includes("boardItemsBySlug"), "one join supplies both priority and position");
  assert.ok(laneBoard().includes("boardItemsBySlug(features)"), "the join is built once from the board read");
});

test("the band slot a move addresses is numbered the way the server numbers it", () => {
  const source = laneOrder();
  assert.ok(source.includes('if (item.status === "done")'), "done features are not in the band at all");
  assert.ok(source.includes("band.sort(compareBoardItems)"), "the slot order is the dashboard order");
  assert.ok(source.includes("slots.set(item.slug, index + 1)"), "1-based, the same index toPosition validates against");
});

test("up and down aim at the nearest lane-mate inside the same priority band", () => {
  const source = laneOrder();
  assert.ok(
    source.includes("const bands = new Map<PlanningFeaturePriority, LaneMember[]>();"),
    "a p2 card never aims at a p1 mate: a move does not cross bands",
  );
  assert.ok(
    source.includes("const ordered = band.sort((a, b) => a.position - b.position);"),
    "lane-mates are walked by band slot, so the neighbour is the next one in that direction",
  );
  assert.ok(source.includes("upTo: index > 0 ? ordered[index - 1].position : null"));
  assert.ok(source.includes("downTo: index < ordered.length - 1 ? ordered[index + 1].position : null"));
  assert.ok(
    !source.includes("atBandStart") && !source.includes("atBandEnd"),
    "a band edge is the wrong condition: the mate can live in another column",
  );
});

test("the control disables when the lane has no mate in that direction", () => {
  const source = moveControls();
  assert.ok(source.includes("disabled={queueMove.busy || target.upTo === null}"));
  assert.ok(source.includes("disabled={queueMove.busy || target.downTo === null}"));
  assert.ok(!source.includes(".status ==="), "no refusal drives this: the target is chosen before the call");
  assert.ok(
    !source.includes("atBandStart") && !source.includes("atBandEnd"),
    "the superseded band-edge logic must not survive in the control",
  );
  assert.ok(source.includes("Move onto the"), "the control moves the card onto the mate's band slot");
});

test("a move is one toPosition aimed at the mate's band slot", () => {
  const source = moveHook();
  assert.ok(
    source.includes('movePlanningFeatureQueue(projectId, slug, { move: "toPosition", position })'),
    "the call lives in the planning slice, the invalidation in the lane that asked for it",
  );
  assert.equal(
    source.split("movePlanningFeatureQueue(").length - 1,
    1,
    "one request per click: composing the effect from two calls is a race and an extra write",
  );
  assert.ok(
    !source.includes('move: "up"') && !source.includes('move: "down"'),
    "a band-adjacent up/down leaves this column byte-identical when the mate lives elsewhere",
  );
  assert.ok(source.includes("planningKeys.board(projectId)"));
  assert.ok(source.includes("lanesKeys.project(projectId)"));
  assert.ok(
    !source.includes("setQueryData"),
    "the band re-inserts and renumbers, so a local guess would redraw other columns wrong",
  );
});

test("a lane card offers the move only where a band slot was computed", () => {
  assert.ok(
    laneCard().includes("const reorderable = boardItem !== undefined && target !== undefined;"),
    "a lane feature missing from the board join has no addressable slot",
  );
  assert.ok(
    laneBoard().includes("laneMoveTargetsBySlug(lanes, features, statusFilter)"),
    "the mate walk is fed the status filter, because the column renders the filtered slice",
  );
});

test("mates come from the displayed slice, the slot number comes from the full band", () => {
  const source = laneOrder();
  const targetWalk = functionBody(source, "export function laneMoveTargetsBySlug", "lane-order.ts");
  assert.ok(
    targetWalk.includes("for (const lane of lanes) {"),
    "the walk groups per lane: a cross-lane mate moves a number the column never shows",
  );
  assert.ok(
    targetWalk.includes("!isStatusVisible(statusFilter, feature.status)"),
    "a hidden lane-mate is not a target: aiming at it moves a number, not the column — pinned inside the walk, not somewhere in the file",
  );
  assert.ok(
    source.includes("const slots = bandSlotBySlug(board);"),
    "slot numbering reads the whole board, so the filter never shifts the number on the wire",
  );
  assert.ok(
    !functionBody(source, "function bandSlotBySlug", "lane-order.ts").includes("isStatusVisible"),
    "if the filter reached the numbering, the sent position would address a different slot than the server's",
  );
  assert.ok(
    laneBoard().includes("isStatusVisible(statusFilter, feature.status)"),
    "the column slice is built next to the mate walk, so both share one visibility predicate",
  );
  assert.ok(
    !laneCard().includes("statusFilter"),
    "the card renders the slice it is handed; a second filter read would let column and targets diverge",
  );
});

test("the whole board carries one in-flight move", () => {
  const board = laneBoard();
  assert.equal(
    board.split("useFeatureQueueMove(").length - 1,
    1,
    "the mutation is instantiated once, at the board that owns the columns",
  );
  assert.ok(board.includes("useFeatureQueueMove(projectId)"));
  assert.ok(board.includes("queueMove={queueMove}"), "the card receives it, it does not mint its own");
  assert.ok(
    !moveControls().includes("useFeatureQueueMove("),
    "a mutation per card leaves the neighbouring card enabled with a target numbered before the first move",
  );
  assert.ok(
    laneCard().includes("queueMove={queueMove}"),
    "the control is fed through the card, so busy gates every card at once",
  );
});

test("the badge shows the server slot, not a row count on the screen", () => {
  assert.ok(
    moveControls().includes("{target.position}"),
    "the printed number must be the full-band slot that goes on the wire — a column index would drift under a filter and rot silently",
  );
});

test("the board derives its joins once per input, not once per render", () => {
  const board = laneBoard();
  assert.ok(board.includes("useMemo(() => boardItemsBySlug(features), [features])"));
  assert.ok(
    board.includes("[lanes, features, statusFilter]"),
    "the mate walk sorts every lane band, so its three inputs are its whole dependency set",
  );
  assert.ok(board.includes("[roster]"), "the done-stripped lane list is rebuilt only when the roster moves");
  assert.ok(
    board.includes("[lanes, boardItems, statusFilter]"),
    "one drop re-slicing a column repaints every column, so the slice map is memoized on its three inputs",
  );
});
