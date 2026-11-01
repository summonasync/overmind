import { strict as assert } from "node:assert";
import { readFileSync, readdirSync } from "node:fs";
import { test } from "node:test";

const sourceOf = (relative: string) =>
  readFileSync(new URL(relative, import.meta.url), "utf8");

const BACK_SURFACES = [
  ["planning-page", "../src/pages/planning-page/ui/planning-page.tsx", "<TabHeader"],
  ["entries-page", "../src/pages/entries-page/ui/entries-page.tsx", "<TabHeader"],
  ["issues-page", "../src/pages/issues-page/ui/issues-page.tsx", "<TabHeader"],
  ["feature-workspace", "../src/features/planning/components/feature-workspace.tsx", "<PageHeader"],
] as const;

const LEGACY_BACK_CLASSES = "text-xs text-muted underline-offset-2 hover:text-foreground hover:underline";

test("every known back surface routes through the shared primitive", () => {
  for (const [name, path] of BACK_SURFACES) {
    const source = sourceOf(path);
    assert.ok(source.includes('from "@/shared/ui/page-back-link"'), `${name} bypasses the shared back link`);
    assert.ok(source.includes("<PageBackLink"), `${name} imports the primitive but never mounts it`);
  }
});

test("the back link sits above the header, never inside the actions slot", () => {
  for (const [name, path, header] of BACK_SURFACES) {
    const source = sourceOf(path);
    const backAt = source.indexOf("<PageBackLink");
    const headerAt = source.indexOf(header);
    assert.notEqual(headerAt, -1, `${name} lost its header along the way`);
    assert.ok(backAt !== -1 && backAt < headerAt, `${name} renders the back link below the header`);
    assert.ok(!source.includes("←"), `${name} hand-rolls the arrow instead of the primitive`);
  }
});

test("the left back-link contract lives only in the primitive", () => {
  const files = readdirSync(new URL("../src/", import.meta.url), { recursive: true }).filter(
    (file) => /\.(ts|tsx)$/.test(file),
  );
  const classHolders = files.filter((file) => sourceOf(`../src/${file}`).includes(LEGACY_BACK_CLASSES));
  assert.deepEqual(
    classHolders,
    ["shared/ui/page-back-link.tsx"],
    "a page re-invented the back link styling by hand",
  );
  const arrowHolders = files.filter((file) => sourceOf(`../src/${file}`).includes("←"));
  assert.deepEqual(
    arrowHolders,
    ["shared/ui/page-back-link.tsx"],
    "a page drew its own ← arrow instead of the primitive",
  );
});

test("wave chat paging is message history, not a nav back link", () => {
  const panel = sourceOf("../src/features/wave-chat/components/wave-chat-panel.tsx");
  const list = sourceOf("../src/features/wave-chat/components/wave-message-list.tsx");
  assert.ok(panel.includes("canPageBack"), "the panel lost its history pager");
  assert.ok(list.includes("canPageBack"), "the message list lost its history pager");
  assert.ok(!panel.includes("PageBackLink"), "the pager got mistaken for a nav back link");
  assert.ok(!list.includes("PageBackLink"), "the pager got mistaken for a nav back link");
});
