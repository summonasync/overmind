import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { healthPopupModel } from "../src/widgets/app-shell/lib/health-popup-rows.ts";
import type { HealthResponse } from "../src/features/health/types.ts";

const topbar = () =>
  readFileSync(new URL("../src/widgets/app-shell/ui/app-topbar.tsx", import.meta.url), "utf8");

const chip = () =>
  readFileSync(new URL("../src/widgets/app-shell/ui/status-chip.tsx", import.meta.url), "utf8");

const popup = () =>
  readFileSync(new URL("../src/widgets/app-shell/ui/health-popup.tsx", import.meta.url), "utf8");

const healthPayload = (): HealthResponse => ({
  status: "ok",
  version: "0.9.8",
  vectorSearch: {
    up: true,
    qdrant: { up: true, endpoint: "http://localhost:6333", error: null },
    embedding: { up: true, provider: "ollama", model: "nomic-embed-text", dimensions: 768, error: null },
  },
});

test("the popup maps qdrant, embedding and version from the health payload", () => {
  const payload = healthPayload();
  const model = healthPopupModel({
    data: payload,
    vectorSearch: payload.vectorSearch,
    vectorSearchDown: false,
  });

  assert.deepEqual(model.rows, [
    { term: "Qdrant", value: "Up · http://localhost:6333" },
    { term: "Embedding", value: "ollama · nomic-embed-text · 768d" },
    { term: "Version", value: "0.9.8" },
  ]);
  assert.equal(model.degraded, false);
});

test("a failing leg reports its error and flags the degraded state", () => {
  const payload = healthPayload();
  payload.vectorSearch.up = false;
  payload.vectorSearch.qdrant = { up: false, endpoint: "http://localhost:6333", error: "connection refused" };
  payload.vectorSearch.embedding = { up: false, provider: "ollama", model: "nomic-embed-text", dimensions: null, error: "model load failed" };

  const model = healthPopupModel({
    data: payload,
    vectorSearch: payload.vectorSearch,
    vectorSearchDown: true,
  });

  assert.equal(model.rows[0].value, "Down · connection refused");
  assert.equal(model.rows[1].value, "Down · model load failed");
  assert.equal(model.degraded, true);
});

test("a missing report degrades to unknown values without inventing legs", () => {
  const model = healthPopupModel({ data: null, vectorSearch: null, vectorSearchDown: false });

  assert.deepEqual(model.rows, [
    { term: "Vector search", value: "Unknown" },
    { term: "Version", value: "unknown" },
  ]);
});

test("null dimensions drop the size segment instead of printing null", () => {
  const payload = healthPayload();
  payload.vectorSearch.embedding = { up: true, provider: "ollama", model: "nomic-embed-text", dimensions: null, error: null };

  const model = healthPopupModel({
    data: payload,
    vectorSearch: payload.vectorSearch,
    vectorSearchDown: false,
  });

  assert.equal(model.rows[1].value, "ollama · nomic-embed-text");
});

test("the placeholder avatar chip is gone and StatusChip sits in the far right corner", () => {
  const source = topbar();
  assert.ok(!source.includes("Humanity Evolves"), "the placeholder headline is removed");
  assert.ok(!source.includes("kerriganAvatar"), "the avatar import leaves with its chip");
  const bell = source.lastIndexOf("<NotificationBell />");
  const status = source.lastIndexOf("<StatusChip />");
  assert.ok(bell !== -1 && status > bell, "StatusChip renders last, pinned to the corner");
});

test("the chip opens its popup from the keyboard via the hint primitive", () => {
  const source = chip();
  assert.ok(source.includes("Hint"), "the popup rides the shared hint primitive");
  assert.ok(source.includes('align="right"'), "the bubble clears the right screen edge");
  assert.ok(source.includes('width="wide"'), "the health popup uses the wide bubble");
  assert.ok(source.includes("trigger="), "the chip visual is the hint trigger");
  assert.ok(source.includes('role="status"'), "live region semantics survive the rewrap");
  assert.ok(!source.includes("onMouse"), "no js hover wiring — the css group rules own the reveal");
});

test("the degraded popup links to settings", () => {
  const source = popup();
  assert.ok(source.includes("healthPopupModel"), "content maps through the shared row model");
  assert.ok(source.includes("model.degraded"), "the link is gated on the degraded flag");
  assert.ok(source.includes('href="/settings"'), "degraded state offers the settings escape hatch");
});
