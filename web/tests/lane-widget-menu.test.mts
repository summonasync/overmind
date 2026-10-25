import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const menu = () =>
  readFileSync(new URL("../src/features/lanes/components/lane-widget-menu.tsx", import.meta.url), "utf8");

test("the chevron announces itself and the menu as an actions menu", () => {
  const source = menu();
  assert.ok(source.includes('aria-haspopup="menu"'), "the trigger names what it opens");
  assert.ok(source.includes("aria-expanded={open}"), "the expanded state is tracked for assistive tech");
  assert.ok(source.includes('role="menu"'), "the panel is a menu, not a mystery popover");
  assert.ok(
    source.includes('role="menuitem"'),
    "entries are announced and reachable by keyboard focus",
  );
});

test("open feature is a real link to the handed-in route", () => {
  const source = menu();
  assert.ok(
    source.includes('role="menuitem"\n            href={href}'),
    "open feature navigates, it does not fake it with a button",
  );
});

test("copy link writes the absolute url and reports the copy in place", () => {
  const source = menu();
  assert.ok(source.includes("navigator.clipboard.writeText"), "the link lands on the clipboard");
  assert.ok(
    source.includes("new URL(href, window.location.origin)"),
    "the copied url resolves against the app origin, not the bare route",
  );
  assert.ok(source.includes('"Copied"'), "the click gets feedback without leaving the menu");
});

test("the menu closes on Escape and on a press outside, a press inside keeps it open", () => {
  const source = menu();
  assert.ok(source.includes('"Escape"'), "Escape is a close path");
  assert.ok(
    source.includes('window.addEventListener("pointerdown"'),
    "outside presses are tracked while the menu is open",
  );
  assert.ok(
    source.includes(".contains("),
    "the zone check keeps a press inside the menu from closing it",
  );
});

test("a press inside the menu never starts the card drag", () => {
  const source = menu();
  assert.ok(
    source.includes("onPointerDown={keepPressLocal}"),
    "the trigger and the open panel are excluded from the drag sensors",
  );
  const zoneLine = source.split("\n").find((line) => line.includes("ref={zoneRef}"));
  assert.ok(
    zoneLine !== undefined && zoneLine.includes("onPointerDown={keepPressLocal}"),
    "the exclusion wraps the whole affordance zone, trigger and panel together",
  );
});

test("the claim readout shows the holder alias with its claim state", () => {
  const source = menu();
  assert.ok(source.includes('"unclaimed"'), "an unclaimed feature says so");
  assert.ok(
    source.includes("held by ${claimAlias}"),
    "the holder alias is spelled out, the state rides along when it exists",
  );
});
