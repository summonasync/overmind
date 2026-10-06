import { strict as assert } from "node:assert";
import { test } from "node:test";
import { desktopToggleState } from "../src/widgets/app-shell/lib/desktop-toggle-state.ts";

test("granted and enabled checks the toggle", () => {
  const state = desktopToggleState("granted", true);
  assert.deepEqual(
    { checked: state.checked, disabled: state.disabled, warning: state.warning },
    { checked: true, disabled: false, warning: false },
  );
});

test("granted but preference off keeps the toggle unchecked and usable", () => {
  const state = desktopToggleState("granted", false);
  assert.equal(state.checked, false);
  assert.equal(state.disabled, false);
});

test("default permission explains the coming prompt", () => {
  const state = desktopToggleState("default", false);
  assert.equal(state.checked, false);
  assert.equal(state.disabled, false);
  assert.ok(state.hint.includes("asks the browser"));
});

test("a stale enabled preference never renders checked without a grant", () => {
  assert.equal(desktopToggleState("default", true).checked, false);
});

test("denied renders the blocked state honestly", () => {
  const state = desktopToggleState("denied", true);
  assert.equal(state.checked, false);
  assert.equal(state.disabled, true);
  assert.equal(state.warning, true);
  assert.ok(state.hint.includes("Blocked by the browser"));
});

test("unsupported browsers disable the toggle outright", () => {
  const state = desktopToggleState("unsupported", true);
  assert.equal(state.checked, false);
  assert.equal(state.disabled, true);
  assert.equal(state.warning, false);
  assert.ok(state.hint.includes("no desktop notification support"));
});
