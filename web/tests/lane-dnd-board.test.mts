import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const laneBoard = () =>
  readFileSync(new URL("../src/features/lanes/components/lane-board.tsx", import.meta.url), "utf8");
const laneCard = () =>
  readFileSync(new URL("../src/features/lanes/components/lane-card.tsx", import.meta.url), "utf8");
const dndContext = () =>
  readFileSync(
    new URL("../src/features/lanes/components/lane-dnd-context.tsx", import.meta.url),
    "utf8",
  );
const poolDroppable = () =>
  readFileSync(
    new URL("../src/features/lanes/components/lane-pool-droppable.tsx", import.meta.url),
    "utf8",
  );
const dropNote = () =>
  readFileSync(new URL("../src/features/lanes/components/lane-drop-note.tsx", import.meta.url), "utf8");
const dropHook = () =>
  readFileSync(new URL("../src/features/lanes/lib/use-lane-drop.ts", import.meta.url), "utf8");
const dropPlan = () =>
  readFileSync(new URL("../src/features/lanes/lib/lane-drop-plan.ts", import.meta.url), "utf8");
const optimistic = () =>
  readFileSync(new URL("../src/features/lanes/lib/lane-optimistic.ts", import.meta.url), "utf8");
const bandPreview = () =>
  readFileSync(new URL("../src/features/lanes/lib/lane-band-preview.ts", import.meta.url), "utf8");
const dndData = () =>
  readFileSync(new URL("../src/features/lanes/lib/lane-dnd-data.ts", import.meta.url), "utf8");

test("the whole lanes board lives inside one drag context that reports drops and drag state", () => {
  const board = laneBoard();
  assert.ok(
    board.includes(
      "<LaneDndProvider onDrop={handleDrop} onDragChange={handleDragChange} resolveGhostCard={resolveGhostCard}>",
    ),
  );
  assert.ok(board.includes("resolveLaneDrop("), "the shared resolver decides reorder vs transfer vs pool");
  assert.ok(board.includes("planLaneDrop("), "the plan layer decides which mutation a drop becomes");
  assert.ok(board.includes("useState<LaneFeatureDragData | null>(null)"), "the board tracks the active drag");
});

test("the context hands the drop the over id and derives the lane from a card target", () => {
  const context = dndContext();
  assert.ok(context.includes("onDrop?.(drag, target, over?.id ?? null)"), "the board needs the over id");
  assert.ok(
    context.includes("laneDropTargetFromOver(over.data.current)"),
    "a card under the cursor is a drop into its own lane, not a dead end",
  );
  assert.ok(context.includes("onDragChange?.(null)"), "drag end and cancel both clear the active drag");
  assert.ok(
    dndData().includes("export function laneDropTargetFromOver") &&
      dndData().includes("export function parseLaneFeatureDragId"),
  );
});

test("a lane column is one sortable list of sortable cards with per-card over feedback", () => {
  const card = laneCard();
  assert.ok(card.includes("<LaneSortableList"), "the column is the drop container");
  assert.ok(card.includes("<LaneSortableCard"), "each feature row is the sortable node");
  assert.ok(card.includes("isDragging && \"opacity-40"), "the dragged card dims instead of vanishing");
  assert.ok(card.includes("isOver && !isDragging && \"bg-primary/10\""), "the landing slot is visible");
  assert.ok(card.includes("sortableDisabled={blockReason !== null && feature.slug !== draggedSlug}"),
    "a busy lane's cards refuse drops, but the dragged card stays alive to leave or come back");
});

test("a busy lane renders an inline reason chip and dims while a drag is active", () => {
  const card = laneCard();
  assert.ok(card.includes("blockChipLabel(lane, blockReason)"));
  assert.ok(card.includes("held by"), "a claim lock names its holder");
  assert.ok(card.includes("serial — one open feature at a time"));
  assert.ok(card.includes("disabled={blockReason !== null}"), "the list itself stops accepting drops");
});

test("the pool is a droppable that assigns from empty and unassigns back into", () => {
  const board = laneBoard();
  assert.ok(board.includes("<LanePoolDroppable"), "the without-lane section is a drop zone");
  assert.ok(board.includes("<LaneDraggableCard slug={item.slug} fromLane={null}"), "pool rows drag with a null origin");
  assert.ok(
    board.includes("poolDisabled = dragged !== null && dragged.fromLane === null"),
    "a pool card hovering its own pool is a no-op, so the pool stops advertising itself",
  );
  assert.ok(poolDroppable().includes("disabled"));
});

test("an empty lane still advertises a drop slot", () => {
  const card = laneCard();
  assert.ok(
    card.includes("Drop a feature here to assign it"),
    "the gesture must survive the last card leaving the lane",
  );
});

test("drop plans become exactly the three existing server contracts", () => {
  const hook = dropHook();
  assert.ok(hook.includes('movePlanningFeatureQueue(projectId, slug, { move: "toPosition", position })'),
    "reorder persists through the same queue-move the arrow buttons use");
  assert.equal(
    hook.split("movePlanningFeatureQueue(").length - 1,
    1,
    "one request per drop: the plan, not the hook, composes gestures",
  );
  assert.ok(hook.includes("updatePlanningFeature(projectId, slug, { lane })"),
    "assign and transfer ride the existing lane assignment");
  assert.ok(hook.includes("lane: UNASSIGNED_LANE"), "unassign sends the empty-string sentinel");
  assert.ok(!hook.includes("lane: null"), "null means leave-it-alone on the wire");
  assert.ok(hook.includes("previewBoardLane(") && hook.includes("previewLaneAssignment("),
    "the roster and board caches both move so the columns redraw instantly");
  assert.ok(hook.includes("previewBandReorder("), "a reorder previews the band, not just the card");
});

test("a failed drop rolls the optimistic caches back and surfaces on the card", () => {
  const hook = dropHook();
  assert.ok(hook.includes("takeSnapshot"), "the pre-drop caches are captured in onMutate");
  assert.ok(hook.includes("queryClient.setQueryData(boardKey, snapshot.board)"));
  assert.ok(hook.includes("queryClient.setQueryData(lanesKey, snapshot.lanes)"));
  assert.ok(hook.includes("onError"), "the rollback lives on the error path, not on a timeout");
  assert.ok(hook.includes("Move rolled back —"), "the message says what happened to the card");
  assert.ok(
    dropNote().includes('role="status"'),
    "the card note is a live region, so a rollback is announced and never silent",
  );
  assert.ok(
    laneBoard().includes("drop.dropError?.slug === item.slug"),
    "the pool row carries its own failure note",
  );
});

test("every gesture keeps its keyboard parity and announcements", () => {
  const context = dndContext();
  assert.ok(context.includes("KeyboardSensor"));
  assert.ok(
    context.includes("sortableKeyboardCoordinates"),
    "the sortable getter walks between cards, lanes and the pool because it reads every enabled droppable",
  );
  assert.ok(context.includes("announcements"), "drag state is announced via the live region set");
  assert.ok(laneCard().includes("<LaneSortableCard"), "lane cards expose the draggable attributes");
  assert.ok(
    laneBoard().includes("<LaneDraggableCard"),
    "pool rows expose the same keyboard handle as lane cards",
  );
  assert.ok(
    laneBoard().includes("LaneFeatureMove") === false,
    "the board does not re-render move controls; the card keeps them for pointer-free fallback",
  );
});

test("the plan layer never invents a write the contracts cannot express", () => {
  const plan = dropPlan();
  assert.ok(plan.includes('kind: "queue-move"') && plan.includes('kind: "assign"') && plan.includes('kind: "unassign"'));
  assert.ok(plan.includes('kind: "refuse"'), "a refusal is a plan outcome, not a thrown error");
  assert.ok(plan.includes("laneBandAnchor"), "cross-priority drops clamp into the dragged feature's own band");
});

test("the reorder preview renumbers the band the way the server does", () => {
  const preview = bandPreview();
  assert.ok(
    preview.includes('import { compareBoardItems } from "@/features/planning";'),
    "the band is read in dashboard order through the slice barrel",
  );
  assert.ok(preview.includes("index + 1"), "slots are 1-based, the numbering toPosition validates against");
  assert.ok(
    optimistic().includes("next.splice(to, 0, moved);"),
    "the splice matches the server's remove-then-insert at the anchor's pre-move slot",
  );
  assert.ok(
    optimistic().includes('import type { PlanningFeatureBoardItem } from "@/features/planning";'),
    "the node-imported module carries the slice types, never slice values",
  );
});
