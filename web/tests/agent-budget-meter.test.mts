import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import {
  formatTokenCount,
  resolveBudgetMeterState,
} from "../src/features/agent-chat/lib/budget-meter-state.ts";

const meterSource = () =>
  readFileSync(
    new URL("../src/features/agent-chat/components/agent-budget-meter.tsx", import.meta.url),
    "utf8",
  );

const usage = (dailyBudget: number, spentToday: number) => ({
  dailyBudget,
  spentToday,
  remainingToday: dailyBudget > 0 ? Math.max(0, dailyBudget - spentToday) : null,
  perTurnMaxTokens: 8192,
});

test("off state keeps zero limit semantics: never warn or exhausted", () => {
  assert.equal(resolveBudgetMeterState(usage(0, 1200)), "off");
  assert.equal(resolveBudgetMeterState(usage(-5, 9000)), "off");
});

test("budgeted states classify normal, warn and exhausted", () => {
  assert.equal(resolveBudgetMeterState(usage(1000, 400)), "normal");
  assert.equal(resolveBudgetMeterState(usage(1000, 850)), "warn");
  assert.equal(resolveBudgetMeterState(usage(1000, 1000)), "exhausted");
});

test("token counts format compactly", () => {
  assert.equal(formatTokenCount(980), "980");
  assert.equal(formatTokenCount(1200), "1.2k");
  assert.equal(formatTokenCount(43_000), "43k");
  assert.equal(formatTokenCount(256_000), "256k");
});

test("meter copy states the output basis and the UTC reset", () => {
  const source = meterSource();
  assert.ok(
    source.includes("Output tokens written by the agent today, resets at 00:00 UTC"),
    "the meter title lost the output basis or the UTC reset",
  );
  assert.ok(
    source.includes("output tokens used today, resets at 00:00 UTC"),
    "the budgeted tooltip lost the output basis or the UTC reset",
  );
});

test("off state renders the informational spent counter without budget chrome", () => {
  const source = meterSource();
  assert.ok(!source.includes("Budget off"), "the muted Budget off label is gone");
  assert.ok(source.includes("output tokens today"), "the spent counter lost its basis");
  const offBranch = source.slice(
    source.indexOf('state === "off"'),
    source.indexOf("const exhausted"),
  );
  assert.ok(offBranch.length > 0, "the off branch vanished from the meter");
  assert.ok(!offBranch.includes("width:"), "the off state renders no progress bar fill");
  assert.ok(
    !offBranch.includes("bg-warning") && !offBranch.includes("bg-danger"),
    "the off state carries no warn or exhausted colors",
  );
});

test("budgeted meter keeps warn and exhausted chrome", () => {
  const source = meterSource();
  assert.ok(source.includes("Budget reached"));
  assert.ok(source.includes("bg-warning"));
  assert.ok(source.includes("bg-danger"));
});
