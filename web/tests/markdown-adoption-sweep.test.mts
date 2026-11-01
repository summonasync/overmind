import { strict as assert } from "node:assert";
import { readFileSync, readdirSync } from "node:fs";
import { test } from "node:test";

const sourceOf = (relative: string) =>
  readFileSync(new URL(relative, import.meta.url), "utf8");

const EDITOR_SURFACES = [
  ["artifact-editor", "../src/features/planning/components/artifact-editor.tsx"],
  ["task-editor", "../src/features/planning/components/task-editor.tsx"],
  ["entry-editor", "../src/features/entries/components/entry-editor.tsx"],
  ["feature-form", "../src/features/planning/components/feature-form.tsx"],
  ["project-manifest-section", "../src/features/projects/components/project-manifest-section.tsx"],
  ["skill-files-editor", "../src/features/entries/components/skill-files-editor.tsx"],
  ["entry-read-modal", "../src/features/entries/components/entry-read-modal.tsx"],
  ["entry-list", "../src/features/entries/components/entry-list.tsx"],
  ["feature-overview-panel", "../src/pages/planning-page/ui/feature-overview-panel.tsx"],
  ["feature-workspace", "../src/features/planning/components/feature-workspace.tsx"],
] as const;

const EDITING_SURFACES = new Set([
  "artifact-editor",
  "task-editor",
  "entry-editor",
  "feature-form",
  "project-manifest-section",
  "skill-files-editor",
]);

const EDITOR_WIRING: Record<string, [string, string]> = {
  "artifact-editor": ["value={body}", "onValueChange={setBody}"],
  "task-editor": ["value={body}", "onValueChange={setBody}"],
  "entry-editor": ["value={content}", "onValueChange={setContent}"],
  "feature-form": ["value={body}", "onValueChange={setBody}"],
  "project-manifest-section": ["value={draft}", "onValueChange={setDraft}"],
  "skill-files-editor": [
    "value={file.content}",
    "onValueChange={(content) => updateFile(file.relativePath, content)}",
  ],
};

const OLD_RENDERER = 'from "@/shared/lib/markdown"';

test("all ten scoped surfaces left the retired inline renderer behind", () => {
  assert.equal(EDITOR_SURFACES.length, 10, "the spec pins exactly ten surfaces");
  for (const [name, path] of EDITOR_SURFACES) {
    const source = sourceOf(path);
    assert.ok(!source.includes(OLD_RENDERER), `${name} still renders through the retired inline renderer`);
  }
});

test("editing surfaces mount the shared MarkdownEditor straight onto their state", () => {
  for (const [name, path] of EDITOR_SURFACES) {
    if (!EDITING_SURFACES.has(name)) {
      continue;
    }
    const source = sourceOf(path);
    assert.ok(!source.includes('from "@/shared/ui/textarea"'), `${name} still edits markdown in a bare textarea`);
    assert.ok(
      !source.includes('from "@/shared/ui/write-preview-toggle"'),
      `${name} still hand-wires its own write/preview pair`,
    );
    assert.ok(source.includes('from "@/shared/ui/markdown-editor"'), `${name} misses the shared editor import`);
    assert.ok(source.includes("<MarkdownEditor"), `${name} never mounts the editor`);
    const [valueProp, changeProp] = EDITOR_WIRING[name];
    assert.ok(source.includes(valueProp), `${name} does not feed its persisted state into the editor`);
    assert.ok(source.includes(changeProp), `${name} does not pipe edits back into its own state`);
  }
});

test("reading surfaces mount the shared MarkdownViewer", () => {
  for (const [name, path] of EDITOR_SURFACES) {
    if (EDITING_SURFACES.has(name)) {
      continue;
    }
    const source = sourceOf(path);
    assert.ok(!source.includes('from "@/shared/ui/textarea"'), `${name} renders markdown through a textarea`);
    assert.ok(source.includes('from "@/shared/ui/markdown-viewer"'), `${name} misses the shared viewer import`);
    assert.ok(source.includes("<MarkdownViewer"), `${name} never mounts the viewer`);
  }
});

test("the whole src tree is clean of the retired renderer", () => {
  const files = readdirSync(new URL("../src/", import.meta.url), { recursive: true }).filter(
    (file) => /\.(ts|tsx)$/.test(file),
  );
  assert.ok(files.length > 0, "the src tree resolved");
  const importers = files.filter((file) => sourceOf(`../src/${file}`).includes(OLD_RENDERER));
  assert.deepEqual(importers, [], "the inline renderer has no callers left");
  const toggleImporters = files.filter((file) =>
    sourceOf(`../src/${file}`).includes('from "@/shared/ui/write-preview-toggle"'),
  );
  assert.deepEqual(
    toggleImporters,
    ["shared/ui/markdown-editor.tsx"],
    "the toggle lives only inside the shared editor",
  );
});

test("no parallel markdown parser remains in src", () => {
  const files = readdirSync(new URL("../src/", import.meta.url), { recursive: true }).filter(
    (file) => /\.(ts|tsx)$/.test(file),
  );
  assert.ok(!files.includes("shared/lib/markdown.tsx"), "the retired renderer file itself is gone");
  for (const engine of ['from "react-markdown"', 'from "remark-gfm"']) {
    const importers = files.filter((file) => sourceOf(`../src/${file}`).includes(engine));
    assert.deepEqual(
      importers,
      ["shared/ui/markdown-viewer.tsx"],
      `a parallel markdown parser snuck in via ${engine}`,
    );
  }
});

test("no test suite keeps a private react-markdown pipeline", () => {
  const roundtrip = sourceOf("./markdown-roundtrip.test.mts");
  assert.ok(!roundtrip.includes('from "react-markdown"'), "the roundtrip render owns no engine");
  assert.ok(roundtrip.includes("MarkdownViewer"), "the roundtrip render goes through the shared viewer");
});
