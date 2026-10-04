import { strict as assert } from "node:assert";
import { test } from "node:test";
import { titleFor } from "../src/widgets/app-shell/lib/tab-title.ts";

test("open project names the tab", () => {
  assert.equal(titleFor("Mindlash Studio", 0), "Mindlash Studio — Overmind");
});

test("no project or unresolvable id keeps the plain title", () => {
  assert.equal(titleFor(null, 0), "Overmind");
});

test("awaiting count prefixes the project base", () => {
  assert.equal(titleFor("Mindlash Studio", 2), "(2) Mindlash Studio — Overmind");
  assert.equal(titleFor("Mindlash Studio", 12), "(12) Mindlash Studio — Overmind");
});

test("awaiting count prefixes the plain base without a project", () => {
  assert.equal(titleFor(null, 3), "(3) Overmind");
});

test("empty awaiting set keeps the bare project base", () => {
  assert.equal(titleFor("Mindlash Studio", 0), "Mindlash Studio — Overmind");
  assert.equal(titleFor(null, 0), "Overmind");
});
