import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const parser = () =>
  readFileSync(new URL("../src/features/projects/lib/mirror-drift.ts", import.meta.url), "utf8");

const dialog = () =>
  readFileSync(
    new URL("../src/features/projects/components/project-moved-dialog.tsx", import.meta.url),
    "utf8",
  );

const hook = () =>
  readFileSync(new URL("../src/features/projects/lib/use-project-moved.ts", import.meta.url), "utf8");

const detail = () =>
  readFileSync(new URL("../src/features/projects/components/project-detail.tsx", import.meta.url), "utf8");

const picker = () =>
  readFileSync(
    new URL("../src/features/projects/components/folder-picker-modal.tsx", import.meta.url),
    "utf8",
  );

test("the drift parser keys on the host 409 detail prefix with counts and bounded paths", () => {
  const source = parser();
  assert.ok(
    source.includes(
      "/^Mirror verification failed: (\\d+) of (\\d+) files drifted or missing at the candidate path\\. Files: /",
    ),
  );
  assert.ok(source.includes('split(", ")'));
  assert.ok(source.includes("/ and (\\d+) more$/"));
});

test("a detail without the mirror prefix is not drift, so kits and validation stay raw", () => {
  const source = parser();
  assert.ok(source.includes("return null;"));
  const source2 = dialog();
  assert.ok(source2.includes("parseMirrorDrift(move.error.message)"));
  assert.ok(source2.includes("<ErrorText error={move.error} />"));
});

test("the dialog renders the parsed drift as a bounded monospace list with the hidden tail", () => {
  const source = dialog();
  assert.ok(source.includes('aria-label="Drifted or missing files"'));
  assert.ok(source.includes("{drift.files.map((file) => ("));
  assert.ok(source.includes("and {drift.hiddenCount} more."));
});

test("the browser is the shared folder browser over the browse doors", () => {
  const source = dialog();
  assert.ok(
    source.includes('import { FolderBrowser } from "@/features/projects/components/folder-picker-modal"'),
  );
  assert.ok(source.includes("<FolderBrowser path={browsePath} onNavigate={setBrowsePath} />"));
  assert.ok(source.includes("Use selected folder"));
  const source2 = picker();
  assert.ok(source2.includes("export function FolderBrowser"));
  assert.ok(source2.includes("useBrowseRoots()"));
  assert.ok(source2.includes("useDirectory(path)"));
});

test("the free-text path field is the confirm source with the current location hint", () => {
  const source = dialog();
  assert.ok(source.includes('id="project-moved-path"'));
  assert.ok(source.includes("useState(project.diskPath)"));
  assert.ok(source.includes("Current location:"));
  assert.ok(source.includes("move.mutate(trimmed)"));
  assert.ok(source.includes("disabled={move.isPending || trimmed.length === 0 || unchanged}"));
});

test("confirm reuses the project update mutation and invalidates project caches", () => {
  const source = hook();
  assert.ok(source.includes("updateProject(project.id, {"));
  assert.ok(source.includes("diskPath,"));
  assert.ok(source.includes("invalidateQueries({ queryKey: projectKeys.lists() })"));
  assert.ok(source.includes('invalidateQueries({ queryKey: projectKeys.detail(project.id) })'));
  assert.ok(source.includes('invalidateQueries({ queryKey: [...projectKeys.all, "tree"] })'));
});

test("success states that the mirror was verified at the new location", () => {
  const source = dialog();
  assert.ok(source.includes('role="status"'));
  assert.ok(source.includes("planning mirror was verified at the new location"));
});

test("the project detail hosts the moved-to trigger next to the disk path badge", () => {
  const source = detail();
  assert.ok(source.includes("Project moved to…"));
  assert.ok(source.includes("<Badge"));
  assert.ok(source.includes("<ProjectMovedDialog key={project.id}"));
});
