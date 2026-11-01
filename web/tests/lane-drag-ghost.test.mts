import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const dndContext = () =>
  readFileSync(
    new URL("../src/features/lanes/components/lane-dnd-context.tsx", import.meta.url),
    "utf8",
  );
const ghost = () =>
  readFileSync(
    new URL("../src/features/lanes/components/lane-drag-ghost.tsx", import.meta.url),
    "utf8",
  );
const laneBoard = () =>
  readFileSync(new URL("../src/features/lanes/components/lane-board.tsx", import.meta.url), "utf8");

test("an active drag floats a read-only ghost resolved from the board snapshot", () => {
  const context = dndContext();
  assert.ok(
    context.includes("<DragOverlay dropAnimation={null}>"),
    "the overlay lives inside the provider's DndContext",
  );
  assert.ok(
    context.includes("setGhost(resolveGhostCard?.(drag.slug) ?? null);"),
    "the card is resolved once at pick-up, so a refetch mid-drag cannot blank the ghost",
  );
  assert.equal(
    context.split("resolveGhostCard?.(").length - 1,
    1,
    "the lookup never runs per render",
  );
  assert.ok(
    context.includes("ghost === null ? null : <LaneDragGhost card={ghost} />"),
    "the overlay only carries a resolved card",
  );
});

test("the ghost is gone the moment the drag ends or is cancelled", () => {
  const context = dndContext();
  assert.equal(
    context.split("setGhost(null);").length - 1,
    2,
    "drag end and drag cancel each release the ghost",
  );
  assert.ok(
    context.includes("dropAnimation={null}"),
    "the overlay unmounts immediately instead of animating into a board that may still roll back optimistically",
  );
  assert.ok(
    context.includes("useState<LaneDragGhostCard | null>(null)"),
    "the ghost state starts empty",
  );
});

test("the silhouette is dead to the pointer and reads as lifted", () => {
  const source = ghost();
  assert.ok(source.includes("pointer-events-none"), "the ghost never intercepts the drop");
  assert.ok(
    source.includes('aria-hidden="true"'),
    "announcements carry the drag state, the ghost is decoration",
  );
  assert.ok(source.includes("shadow-lg"), "the card lifts off the board");
  assert.ok(source.includes("border-primary"), "the accent border marks the card as airborne");
  assert.ok(source.includes("scale-105") && source.includes("opacity-90"), "a slight lift, not a second card");
  assert.ok(
    !source.includes("<a ") && !source.includes("Menu"),
    "no links, no menus, no live controls inside the ghost",
  );
});

test("the board feeds the ghost from data it already holds and keyboard parity survives", () => {
  const board = laneBoard();
  assert.ok(
    board.includes("resolveGhostCard={resolveGhostCard}"),
    "the provider is fed, the board keeps ownership of its data",
  );
  assert.ok(
    board.includes("boardItems.get(slug)"),
    "the lookup reads the board memo, no new data layer",
  );
  const context = dndContext();
  assert.equal(context.split("useSensor(").length - 1, 2, "the pointer and keyboard sensor pair is unchanged");
  assert.ok(context.includes("sortableKeyboardCoordinates"), "keyboard drags keep their coordinate getter");
  assert.ok(context.includes("announcements"), "the screen reader flow is untouched");
});
