import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const draggableCard = () =>
  readFileSync(
    new URL("../src/features/lanes/components/lane-draggable-card.tsx", import.meta.url),
    "utf8",
  );
const sortableCard = () =>
  readFileSync(
    new URL("../src/features/lanes/components/lane-sortable-card.tsx", import.meta.url),
    "utf8",
  );
const dndContext = () =>
  readFileSync(
    new URL("../src/features/lanes/components/lane-dnd-context.tsx", import.meta.url),
    "utf8",
  );

test("a pickable card handle advertises the drag with a grab cursor", () => {
  for (const [name, source] of [
    ["pool card", draggableCard()],
    ["lane card", sortableCard()],
  ] as const) {
    assert.ok(
      source.includes("cursor-grab active:cursor-grabbing"),
      `${name} hovers as grab and presses down as grabbing`,
    );
    assert.ok(
      source.includes("disabled ? undefined :"),
      `${name} never advertises a grab it cannot honor`,
    );
  }
});

test("the board root holds the grabbing cursor while any drag runs", () => {
  const context = dndContext();
  assert.ok(
    context.includes("const [dragActive, setDragActive] = useState(false);"),
    "the flag starts idle and lives in the provider that owns the drag lifecycle",
  );
  assert.ok(
    context.includes("setDragActive(true)"),
    "a parsed drag start turns the flag on",
  );
  assert.equal(
    context.split("setDragActive(false)").length - 1,
    2,
    "drag end and drag cancel both release the cursor",
  );
  assert.ok(
    context.includes('dragActive ? "cursor-grabbing" : undefined'),
    "the provider wrapper paints the board root, so every descendant inherits it",
  );
});

test("the in-card affordances keep their press-local sensor exclusions", () => {
  for (const [name, url] of [
    ["number link", "../src/features/lanes/components/lane-feature-widget.tsx"],
    ["widget menu", "../src/features/lanes/components/lane-widget-menu.tsx"],
  ] as const) {
    const source = readFileSync(new URL(url, import.meta.url), "utf8");
    assert.ok(
      source.includes("keepPressLocal"),
      `${name} still stops the press before the pointer sensor sees it`,
    );
  }
  const context = dndContext();
  assert.equal(
    context.split("useSensor(").length - 1,
    2,
    "the pointer and keyboard sensor pair is unchanged",
  );
});
