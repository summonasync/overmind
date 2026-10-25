import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { KeyboardSensor } from "@dnd-kit/core";

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
const widget = () =>
  readFileSync(
    new URL("../src/features/lanes/components/lane-feature-widget.tsx", import.meta.url),
    "utf8",
  );
const menu = () =>
  readFileSync(
    new URL("../src/features/lanes/components/lane-widget-menu.tsx", import.meta.url),
    "utf8",
  );

const pressOn = (target: object, code: string, prevented: string[]) => ({
  nativeEvent: { code },
  target,
  preventDefault: () => prevented.push(code),
});

const activateWith = (event: object, activator: object | null) =>
  KeyboardSensor.activators[0].handler(
    event as never,
    {},
    { active: { activatorNode: { current: activator } } } as never,
  );

test("a keyboard press on an affordance is refused when the card root is the activator", () => {
  const cardRoot = { id: "card-root" };
  const prevented: string[] = [];

  assert.equal(
    activateWith(pressOn({ id: "nnn-anchor" }, "Enter", prevented), cardRoot),
    false,
    "Enter on the number link never activates the sensor, the anchor navigates",
  );
  assert.deepEqual(prevented, [], "the key press is left to the link, nothing is cancelled");

  assert.equal(
    activateWith(pressOn({ id: "chevron" }, "Space", prevented), cardRoot),
    false,
    "Space on the menu trigger opens the menu, it does not pick the card up",
  );
});

test("a keyboard press on the focused card body still starts the drag", () => {
  const cardRoot = { id: "card-root" };
  const prevented: string[] = [];

  assert.equal(
    activateWith(pressOn(cardRoot, "Space", prevented), cardRoot),
    true,
    "the announced focus + Space contract holds",
  );
  assert.deepEqual(prevented, ["Space"], "the sensor owns the key while it drags");
});

test("an unwired activator would swallow the affordances again", () => {
  const prevented: string[] = [];

  assert.equal(
    activateWith(pressOn({ id: "nnn-anchor" }, "Enter", prevented), null),
    true,
    "the B1 bug: with no activator node the guard passes every key and cancels navigation",
  );
  assert.deepEqual(prevented, ["Enter"]);
});

test("both card wrappers hand dnd-kit the activator node on the card root", () => {
  for (const [name, source] of [
    ["pool card", draggableCard()],
    ["lane card", sortableCard()],
  ] as const) {
    assert.ok(
      source.includes("setNodeRef(element);") && source.includes("setActivatorNodeRef(element);"),
      `${name} wires the sensor guard to the same node that carries the listeners`,
    );
  }
});

test("the affordances stay native under the keyboard: no per-element key interception", () => {
  assert.ok(!widget().includes("onKeyDown"), "the widget adds no keyboard handler of its own");
  const menuSource = menu();
  assert.ok(
    !menuSource.includes("onKeyDown={") && !menuSource.includes("preventDefault"),
    "the chevron and the items activate natively, Escape rides the window listener",
  );
});
