import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import {
  manifestImportedLine,
  manifestIsAbsent,
} from "../src/features/projects/lib/manifest-text.ts";

const section = () =>
  readFileSync(
    new URL("../src/features/projects/components/project-manifest-section.tsx", import.meta.url),
    "utf8",
  );

const api = () =>
  readFileSync(new URL("../src/features/projects/api.ts", import.meta.url), "utf8");

const hooks = () =>
  readFileSync(new URL("../src/features/projects/lib/use-projects.ts", import.meta.url), "utf8");

const page = () =>
  readFileSync(new URL("../src/pages/projects-page/ui/projects-page.tsx", import.meta.url), "utf8");

test("an unset or whitespace-only manifest counts as absent", () => {
  assert.equal(manifestIsAbsent(null), true);
  assert.equal(manifestIsAbsent(""), true);
  assert.equal(manifestIsAbsent("   \n  "), true);
});

test("a stored manifest is present", () => {
  assert.equal(manifestIsAbsent("# Demo"), false);
});

test("the imported line names the found file", () => {
  assert.equal(manifestImportedLine("AGENTS.md"), "Imported manifest from AGENTS.md.");
});

test("saving goes through the project update mutation with setManifest wiring", () => {
  const source = section();
  assert.ok(source.includes(`update.mutate({`));
  assert.ok(source.includes(`manifest: draft`));
  assert.ok(source.includes(`setManifest: true`));
});

test("the editor starts from the stored manifest and follows server changes", () => {
  const source = section();
  assert.ok(source.includes(`useState(project.manifest ?? "")`));
  assert.ok(source.includes(`project.manifest !== syncedManifest`));
});

test("a finished import lands in the editor", () => {
  const source = section();
  assert.ok(
    source.includes(
      `manifestImport.importManifest(project.id, (imported) => setDraft(imported.manifest))`,
    ),
  );
});

test("import reports the found file, its pending state and errors", () => {
  const source = section();
  assert.ok(source.includes(`{manifestImportedLine(importedFile)}`));
  assert.ok(source.includes(`role="status"`));
  assert.ok(source.includes(`<ErrorText error={manifestImport.error} />`));
  assert.ok(source.includes(`disabled={manifestImport.pending}`));
  assert.ok(source.includes("Importing…"));
});

test("an unset manifest shows the honest empty state", () => {
  const source = section();
  assert.ok(source.includes(`manifestIsAbsent(project.manifest)`));
  assert.ok(source.includes("No manifest set"));
});

test("the update call only sends manifest fields when the manifest is being set", () => {
  const source = api();
  assert.ok(
    source.includes(
      `...(values.setManifest ? { manifest: values.manifest ?? null, setManifest: true } : {})`,
    ),
  );
});

test("the import endpoint is called without a body", () => {
  const source = api();
  assert.ok(
    source.includes("apiPost<ProjectManifestImportResponse>(`/projects/${id}/manifest/import`)"),
  );
});

test("a finished import invalidates the project list", () => {
  const source = hooks();
  const hook = source.slice(source.indexOf("useProjectManifestImport"));
  assert.ok(hook.includes(`queryClient.invalidateQueries({ queryKey: projectKeys.lists() })`));
});

test("the project settings surface renders the manifest section", () => {
  const source = page();
  assert.ok(source.includes(`<ProjectManifestSection project={detail} />`));
  assert.ok(source.includes("Manifest"));
});
