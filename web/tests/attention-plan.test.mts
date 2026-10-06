import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import {
  attentionCount,
  attentionDecision,
  clearAttention,
  initialAttentionState,
  isAttentionKind,
  markAllSeen,
  markSeen,
  pruneAttention,
  raiseAttention,
  type AttentionEnvironment,
} from "../src/widgets/app-shell/lib/attention-plan.ts";

const env = (overrides: Partial<AttentionEnvironment> = {}): AttentionEnvironment => ({
  hidden: true,
  desktopEnabled: true,
  chirpEnabled: true,
  permission: "granted",
  ...overrides,
});

const decide = (
  kind: "approval" | "question" | "info",
  seen: boolean,
  environment: AttentionEnvironment,
) => attentionDecision({ id: "n1", kind }, seen, environment);

test("hidden approval raises every surface", () => {
  assert.deepEqual(decide("approval", false, env()), { toast: true, chirp: true, raise: true });
});

test("hidden question raises every surface", () => {
  assert.deepEqual(decide("question", false, env()), { toast: true, chirp: true, raise: true });
});

test("in focus nothing fires — the bubble owns visibility", () => {
  for (const kind of ["approval", "question"] as const) {
    assert.deepEqual(decide(kind, false, env({ hidden: false })), {
      toast: false,
      chirp: false,
      raise: false,
    });
  }
});

test("info kind never surfaces, even hidden", () => {
  assert.deepEqual(decide("info", false, env()), { toast: false, chirp: false, raise: false });
  assert.equal(isAttentionKind("info"), false);
});

test("a seen id stays silent — one surface per event", () => {
  assert.deepEqual(decide("approval", true, env()), { toast: false, chirp: false, raise: false });
});

test("a new event while hidden re-raises", () => {
  assert.equal(attentionDecision({ id: "n2", kind: "approval" }, false, env()).toast, true);
});

test("desktop toggle gates the toast only", () => {
  assert.deepEqual(decide("approval", false, env({ desktopEnabled: false })), {
    toast: false,
    chirp: true,
    raise: true,
  });
});

test("ungranted permissions mute the toast but keep the rest", () => {
  for (const permission of ["denied", "default", "unsupported"] as const) {
    assert.deepEqual(decide("approval", false, env({ permission })), {
      toast: false,
      chirp: true,
      raise: true,
    });
  }
});

test("chirp toggle gates the sound only", () => {
  assert.deepEqual(decide("approval", false, env({ chirpEnabled: false })), {
    toast: true,
    chirp: false,
    raise: true,
  });
});

test("seen marks are idempotent and priming never raises", () => {
  let state = initialAttentionState;
  state = markSeen(state, "n1");
  state = markSeen(state, "n1");
  assert.deepEqual(state.firedIds, ["n1"]);
  assert.deepEqual(attentionCount(state), 0);

  const primed = markAllSeen(state, ["n1", "n2", "n3"]);
  assert.deepEqual(primed.firedIds, ["n1", "n2", "n3"]);
  assert.equal(markAllSeen(primed, ["n2"]), primed);
});

test("attention raises count per id, prunes answered ones and clears on focus", () => {
  let state = initialAttentionState;
  state = markSeen(state, "n1");
  state = markSeen(state, "n2");
  state = raiseAttention(state, "n1");
  state = raiseAttention(state, "n1");
  state = raiseAttention(state, "n2");
  assert.equal(attentionCount(state), 2);

  const pruned = pruneAttention(state, ["n2", "n3"]);
  assert.deepEqual(pruned.attentionIds, ["n2"]);
  assert.deepEqual(pruned.firedIds, ["n2"]);
  assert.equal(pruneAttention(pruned, ["n2", "n3"]), pruned);

  const cleared = clearAttention(pruned);
  assert.equal(attentionCount(cleared), 0);
  assert.deepEqual(cleared.firedIds, ["n2"]);
  assert.equal(clearAttention(cleared), cleared);
});

test("attention wiring holds: shell mounts surfaces, bubble feeds the title owner", () => {
  const shell = readFileSync(
    new URL("../src/widgets/app-shell/ui/app-shell.tsx", import.meta.url),
    "utf8",
  );
  assert.ok(shell.includes("<AttentionSurfaces />"));

  const bubble = readFileSync(
    new URL("../src/widgets/app-shell/ui/approval-bubble.tsx", import.meta.url),
    "utf8",
  );
  assert.ok(bubble.includes("useTabTitle(awaitingCount, useAttentionCount())"));

  const owner = readFileSync(
    new URL("../src/widgets/app-shell/lib/use-tab-title.ts", import.meta.url),
    "utf8",
  );
  assert.ok(owner.includes("titleFor(projectName, awaitingCount, attentionCount)"));

  const settings = readFileSync(
    new URL("../src/pages/settings-page/ui/settings-page.tsx", import.meta.url),
    "utf8",
  );
  assert.ok(settings.includes("<AttentionSettingsSection />"));
});
