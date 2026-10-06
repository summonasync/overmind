import { strict as assert } from "node:assert";
import { test } from "node:test";
import { titleFor } from "../src/widgets/app-shell/lib/tab-title.ts";
import { attentionFaviconSvg } from "../src/widgets/app-shell/lib/attention-favicon.ts";

test("attention keeps the pending badge at its own count while unseen", () => {
  assert.equal(titleFor("Mindlash Studio", 1, 3), "(3) Mindlash Studio — Overmind");
});

test("attention never lowers the badge below the awaiting count", () => {
  assert.equal(titleFor("Mindlash Studio", 5, 2), "(5) Mindlash Studio — Overmind");
});

test("no attention falls back to the plain awaiting badge", () => {
  assert.equal(titleFor("Mindlash Studio", 5, 0), "(5) Mindlash Studio — Overmind");
  assert.equal(titleFor(null, 0, 0), "Overmind");
});

test("attention prefixes the plain base without a project", () => {
  assert.equal(titleFor(null, 0, 2), "(2) Overmind");
});

test("clearing attention hands the badge back to the awaiting count", () => {
  assert.equal(titleFor("Mindlash Studio", 2, 2), "(2) Mindlash Studio — Overmind");
});

test("attention favicon overlays a badge on the base icon", () => {
  const svg = attentionFaviconSvg("/icon.png?v=1");
  assert.ok(svg.startsWith("<svg"));
  assert.ok(svg.includes('<image href="/icon.png?v=1" width="64" height="64"/>'));
  assert.ok(svg.includes('fill="#f59e0b"'));
});

test("attention favicon falls back to the bare badge without a base", () => {
  const svg = attentionFaviconSvg(null);
  assert.ok(!svg.includes("<image"));
  assert.ok(svg.includes("<circle"));
  assert.ok(svg.includes('fill="#f59e0b"'));
});
