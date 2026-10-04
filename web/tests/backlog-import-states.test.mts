import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  importConfirmText,
  importFolderState,
  importFolderStateLabel,
  importFolderStateTone,
  importReportLine,
} from "../src/features/planning/lib/import-folder-state.ts";
import type { PlanningImportScanEntry } from "../src/features/planning/types.ts";

const entry = (overrides: Partial<PlanningImportScanEntry>): PlanningImportScanEntry => ({
  folder: "042-demo",
  slug: "042-demo",
  title: "Demo feature",
  status: "draft",
  specs: 1,
  plans: 1,
  results: 0,
  tasks: 3,
  alreadyImported: false,
  error: null,
  ...overrides,
});

test("folder without import and without error is new", () => {
  assert.equal(importFolderState(entry({})), "new");
});

test("folder whose slug is already on the board is already imported", () => {
  assert.equal(importFolderState(entry({ alreadyImported: true })), "imported");
});

test("folder with a parse error is the error state and drops parse details", () => {
  const broken = entry({ slug: null, title: null, status: null, alreadyImported: false, error: "feature.md is missing" });
  assert.equal(importFolderState(broken), "error");
});

test("error beats the already-imported marker", () => {
  assert.equal(importFolderState(entry({ alreadyImported: true, error: "bad frontmatter" })), "error");
});

test("state labels name all three states", () => {
  assert.equal(importFolderStateLabel("new"), "new");
  assert.equal(importFolderStateLabel("imported"), "already imported");
  assert.equal(importFolderStateLabel("error"), "error");
});

test("state tones map to badge danger, info and success", () => {
  assert.equal(importFolderStateTone("new"), "success");
  assert.equal(importFolderStateTone("imported"), "info");
  assert.equal(importFolderStateTone("error"), "danger");
});

test("re-import confirm names the already-imported state and both folder and feature", () => {
  const text = importConfirmText(entry({ alreadyImported: true }));
  assert.ok(text !== null);
  assert.ok(text.includes("already imported"));
  assert.ok(text.includes("042-demo"));
});

test("new folder imports without a confirm", () => {
  assert.equal(importConfirmText(entry({})), null);
});

test("a folder imported this session keeps requiring the confirm even before the rescan flips its badge", () => {
  const stale = entry({});
  assert.equal(importConfirmText(stale), null);
  const text = importConfirmText(stale, new Set(["042-demo"]));
  assert.ok(text !== null);
  assert.ok(text.includes("already imported"));
});

test("session-local confirm only applies to folders actually imported this session", () => {
  assert.equal(importConfirmText(entry({}), new Set(["099-other"])), null);
});

test("error folder imports without a confirm (button is disabled instead)", () => {
  assert.equal(importConfirmText(entry({ error: "feature.md is missing" })), null);
});

test("first import reports creation with artifact counts", () => {
  const line = importReportLine({
    slug: "042-demo",
    title: "Demo feature",
    status: "draft",
    created: true,
    artifactsCreated: 5,
    artifactsUpdated: 0,
    notes: [],
    trackedFiles: 9,
    materialization: "on",
    nextStep: null,
  });
  assert.ok(line.startsWith("Imported 042-demo"));
  assert.ok(line.includes("5 artifacts created"));
  assert.ok(line.includes("0 updated"));
  assert.ok(!line.startsWith("Re-imported"));
});

test("re-import reports an update, not a creation", () => {
  const line = importReportLine({
    slug: "042-demo",
    title: "Demo feature",
    status: "ready",
    created: false,
    artifactsCreated: 0,
    artifactsUpdated: 4,
    notes: [],
    trackedFiles: 9,
    materialization: "on",
    nextStep: null,
  });
  assert.ok(line.startsWith("Re-imported 042-demo"));
  assert.ok(line.includes("4 updated"));
});
