import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  agentOutcomeTone,
  detectionView,
  grimoireUiUrl,
  installArmHint,
  installPendingLabel,
  showGrimoireBanner,
  validateBookName,
  wiredView,
  wizardOffers,
} from "../src/features/grimoire/lib/grimoire-view.ts";
import type { GrimoireStatusResponse } from "../src/features/grimoire/types.ts";

function statusResponse(overrides: Partial<GrimoireStatusResponse> = {}): GrimoireStatusResponse {
  return {
    status: "notInstalled",
    binaryPath: null,
    version: null,
    port: null,
    bookName: "",
    overridePath: "",
    wired: false,
    installChannel: "",
    ...overrides,
  };
}

test("notInstalled renders the neutral state and offers manual plus assisted", () => {
  const status = statusResponse();
  const view = detectionView(status);
  assert.equal(view.label, "Not installed");
  assert.equal(view.tone, "neutral");
  assert.deepEqual(wizardOffers(status), ["manual", "assisted"]);
});

test("installed renders the info state and offers wiring only", () => {
  const status = statusResponse({ status: "installed", binaryPath: "/usr/bin/grimoire", version: "0.4.0" });
  const view = detectionView(status);
  assert.equal(view.label, "Installed");
  assert.equal(view.tone, "info");
  assert.equal(view.detail, "version 0.4.0");
  assert.deepEqual(wizardOffers(status), ["wire"]);
});

test("running renders the success state with version and UI port", () => {
  const status = statusResponse({ status: "running", version: "0.4.0", port: 5277 });
  const view = detectionView(status);
  assert.equal(view.label, "Running");
  assert.equal(view.tone, "success");
  assert.equal(view.detail, "version 0.4.0 · UI on port 5277");
  assert.deepEqual(wizardOffers(status), ["wire"]);
});

test("wired state collapses the wizard to reconfigure regardless of detection", () => {
  assert.deepEqual(wizardOffers(statusResponse({ wired: true })), ["reconfigure"]);
  assert.deepEqual(
    wizardOffers(statusResponse({ status: "running", wired: true })),
    ["reconfigure"],
  );
});

test("wired view separates the connected and unconnected tile states", () => {
  assert.deepEqual(wiredView(true), { label: "Wired", tone: "success" });
  assert.deepEqual(wiredView(false), { label: "Not wired", tone: "warning" });
});

test("banner shows only while grimoire exists in the response and is not wired", () => {
  assert.equal(showGrimoireBanner(null), false);
  assert.equal(showGrimoireBanner(statusResponse()), true);
  assert.equal(showGrimoireBanner(statusResponse({ status: "installed" })), true);
  assert.equal(showGrimoireBanner(statusResponse({ wired: true })), false);
});

test("UI link appears only in the running state with a known port", () => {
  assert.equal(grimoireUiUrl(statusResponse()), null);
  assert.equal(
    grimoireUiUrl(statusResponse({ status: "installed", port: 5277 })),
    null,
  );
  assert.equal(grimoireUiUrl(statusResponse({ status: "running", port: 5277 })), "http://127.0.0.1:5277");
});

test("book name mirrors the host validation", () => {
  assert.match(validateBookName("") as string, /required/);
  assert.match(validateBookName("   ") as string, /required/);
  assert.match(validateBookName("x".repeat(101)) as string, /at most 100/);
  assert.equal(validateBookName("johan-crew"), null);
});

test("agent outcomes get an honest tone per row", () => {
  assert.equal(agentOutcomeTone({ agent: "claude", outcome: "wired", configPath: null, error: null }), "success");
  assert.equal(
    agentOutcomeTone({ agent: "codex", outcome: "skipped", configPath: null, error: null }),
    "neutral",
  );
  assert.equal(
    agentOutcomeTone({ agent: "qwen", outcome: "failed", configPath: null, error: "config not found" }),
    "danger",
  );
});

test("manual arm hint shows hand steps unconditionally and promises no channel run", () => {
  const hint = installArmHint("manual");
  assert.ok(hint.startsWith("Always shows"));
  assert.match(hint, /install channel never runs/);
  assert.match(hint, /no agent config is touched/);
});

test("the 257-era channel-first workaround sentence stays removed", () => {
  assert.ok(!installArmHint("manual").includes("unless the configured channel"));
});

test("assisted arm hint keeps the channel-first promise", () => {
  assert.equal(
    installArmHint("assisted"),
    "Runs Grimoire's own install channel when one is configured.",
  );
});

test("pending label per arm: manual never says it is checking a channel", () => {
  assert.equal(installPendingLabel("manual"), "Preparing the hand-install steps…");
  assert.equal(installPendingLabel("assisted"), "Checking for an install channel…");
});
