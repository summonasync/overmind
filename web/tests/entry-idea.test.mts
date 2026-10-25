import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { ENTRY_TYPES } from "../src/shared/api/types.ts";
import {
  entryRowClasses,
  entryTitleClasses,
} from "../src/features/entries/lib/entry-read.ts";

const source = (relativePath) =>
  readFileSync(new URL(relativePath, import.meta.url), "utf8");

test("idea joins the type list after tombstone, mirroring the backend enum", () => {
  assert.deepEqual([...ENTRY_TYPES], [
    "note",
    "decision",
    "gate",
    "guard",
    "skill",
    "tombstone",
    "idea",
    "audit",
  ]);
});

test("the editor type select and the list filter render from the shared list and meta", () => {
  const editor = source("../src/features/entries/components/entry-editor.tsx");
  const filters = source("../src/features/entries/components/entry-filters.tsx");
  assert.ok(editor.includes(`{ENTRY_TYPES.map((entryType) => (`));
  assert.ok(editor.includes("{ENTRY_TYPE_META[entryType].label}"));
  assert.ok(filters.includes(`{ENTRY_TYPES.map((type) => (`));
  assert.ok(filters.includes("{ENTRY_TYPE_META[type].label}"));
});

test("idea maps to the idea tone with the lightbulb icon", () => {
  const meta = source("../src/shared/lib/entry-type.ts");
  assert.ok(meta.includes(`idea: { tone: "idea", icon: Lightbulb, label: "Idea" },`));
  const badge = source("../src/shared/ui/badge.tsx");
  assert.ok(badge.includes(`idea: "bg-type-idea/10 text-type-idea",`));
});

test("idea rows carry their own color token", () => {
  const css = source("../src/app/globals.css");
  assert.ok(css.includes("--color-type-idea:"));
});

test("idea renders as a live entry, not a tombstone", () => {
  assert.equal(entryTitleClasses("idea"), "text-foreground");
  const live = entryRowClasses("note");
  assert.equal(entryRowClasses("idea"), live);
});

test("featureRequest no longer appears in the web vocabulary", () => {
  const paths = [
    "../src/shared/api/types.ts",
    "../src/shared/lib/entry-type.ts",
    "../src/shared/ui/badge.tsx",
    "../src/app/globals.css",
    "../src/features/planning/lib/select-tone-classes.ts",
    "../src/features/planning/components/feature-board.tsx",
    "../src/features/planning/components/ceremony-stepper.tsx",
    "../src/features/entries/components/entry-editor.tsx",
    "../src/features/entries/components/entry-filters.tsx",
  ];
  for (const path of paths) {
    assert.ok(!source(path).includes("featureRequest"), `${path} still mentions featureRequest`);
  }
});
