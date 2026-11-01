import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const sourceOf = (relative: string) =>
  readFileSync(new URL(relative, import.meta.url), "utf8");

const ACTION_SITES = [
  ["planning-page", "../src/pages/planning-page/ui/planning-page.tsx", "New feature"],
  ["entries-page", "../src/pages/entries-page/ui/entries-page.tsx", "New entry"],
  ["projects-page", "../src/pages/projects-page/ui/projects-page.tsx", "Manage projects"],
  ["projects-settings-page", "../src/pages/projects-settings-page/ui/projects-settings-page.tsx", "New project"],
  ["kits-page", "../src/pages/kits-page/ui/kits-page.tsx", "New kit"],
] as const;

const HEADER_PRIMITIVES = ["tab-header", "page-header"] as const;

test("header primitives flow actions left instead of anchoring them right", () => {
  for (const name of HEADER_PRIMITIVES) {
    const source = sourceOf(`../src/shared/ui/${name}.tsx`);
    assert.ok(!source.includes("justify-between"), `${name} still anchors actions to the right edge`);
    assert.ok(!source.includes("justify-end"), `${name} still anchors actions to the right edge`);
    const textAt = source.indexOf("text-muted");
    const actionsAt = source.indexOf("{actions ?");
    assert.notEqual(textAt, -1, `${name} lost its text block`);
    assert.notEqual(actionsAt, -1, `${name} lost its actions slot`);
    assert.ok(textAt < actionsAt, `${name} renders actions before the text on the shared axis`);
  }
});

test("every known page-level action still mounts through its header", () => {
  for (const [name, path, label] of ACTION_SITES) {
    const source = sourceOf(path);
    assert.ok(source.includes("actions={"), `${name} dropped its page action slot`);
    assert.ok(source.includes(label), `${name} lost the ${label} action`);
  }
});

test("issues page stays action-free at the page level", () => {
  const source = sourceOf("../src/pages/issues-page/ui/issues-page.tsx");
  assert.ok(!source.includes("actions={"), "issues page grew a page-level action");
});

test("foreign tree folders mute by a contains-projects predicate", () => {
  const source = sourceOf("../src/features/projects/components/project-tree.tsx");
  assert.ok(source.includes("root.containsProjects"), "the predicate ignores the server root flag");
  assert.ok(source.includes("projectAncestorPaths("), "the predicate ignores project ancestor chains");
  assert.ok(
    source.includes('!containsProjects && "opacity-70"'),
    "folder rows mute unconditionally or not at all",
  );
  assert.ok(source.includes("aria-disabled="), "muted rows lost the aria-disabled semantics");
});

test("the muted convention never disables a tree row", () => {
  const source = sourceOf("../src/features/projects/components/project-tree.tsx");
  assert.ok(
    !/(?<!aria-)disabled=\{[^}]*containsProjects/.test(source),
    "a disabled attribute got tied to the muted predicate",
  );
  assert.ok(
    source.includes("disabled={!entry.accessible}"),
    "the accessibility-only chevron guard vanished",
  );
  assert.ok(source.includes("aria-expanded="), "folder rows lost the expand/collapse state");
});
