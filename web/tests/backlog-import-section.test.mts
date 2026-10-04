import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const section = () =>
  readFileSync(
    new URL("../src/features/planning/components/backlog-import-section.tsx", import.meta.url),
    "utf8",
  );

const row = () =>
  readFileSync(
    new URL("../src/features/planning/components/import-folder-row.tsx", import.meta.url),
    "utf8",
  );

const list = () =>
  readFileSync(
    new URL("../src/features/planning/components/import-folder-list.tsx", import.meta.url),
    "utf8",
  );

const page = () =>
  readFileSync(
    new URL("../src/pages/projects-page/ui/projects-page.tsx", import.meta.url),
    "utf8",
  );

test("section drives the list through the shared scan and import mutations", () => {
  const source = section();
  assert.ok(source.includes(`usePlanningImportScan(projectId)`));
  assert.ok(source.includes(`usePlanningFolderImport(projectId)`));
  assert.ok(source.includes(`<ImportFolderList`));
});

test("re-import goes through an inline confirm, fresh folders import directly", () => {
  const source = section();
  assert.ok(source.includes(`importConfirmText(entry) !== null`));
  assert.ok(source.includes(`setConfirmEntry(entry)`));
  assert.ok(source.includes(`{importConfirmText(confirmEntry)}`));
  assert.ok(source.includes("Import again"));
  assert.ok(source.includes(`runImport(entry)`));
  assert.ok(!source.includes("<Modal"));
});

test("a finished import refreshes the scan so row states flip", () => {
  const source = section();
  assert.ok(source.includes(`setReports((previous) => [...previous, imported])`));
  assert.ok(source.includes(`scan.scan()`));
});

test("import report lines are rendered with a status role", () => {
  const source = section();
  assert.ok(source.includes(`importReportLine(item)`));
  assert.ok(source.includes(`role="status"`));
});

test("folder rows render the state badge and the error note", () => {
  const source = row();
  assert.ok(source.includes(`<Badge tone={importFolderStateTone(state)}>`));
  assert.ok(source.includes(`{importFolderStateLabel(state)}`));
  assert.ok(source.includes(`{entry.error}`));
  assert.ok(source.includes(`role="note"`));
});

test("import is disabled for error folders and while the folder is importing", () => {
  const source = row();
  assert.ok(source.includes(`disabled={state === "error" || importing}`));
});

test("folder rows expose an expandable preview with aria state", () => {
  const source = row();
  assert.ok(source.includes(`aria-expanded={open}`));
  assert.ok(source.includes(`{entry.specs} specs · {entry.plans} plans · {entry.tasks} tasks · {entry.results} results`));
});

test("the list marks exactly the folder being imported as pending", () => {
  const source = list();
  assert.ok(source.includes(`entry.folder === importingFolder`));
});

test("the project settings surface renders the backlog import section", () => {
  const source = page();
  assert.ok(source.includes(`<BacklogImportSection projectId={detail.id} />`));
  assert.ok(source.includes("Backlog import"));
});

test("no bulk import-all exists in the import flow", () => {
  for (const source of [section(), list(), row()]) {
    assert.ok(!source.toLowerCase().includes("import all"));
  }
});
