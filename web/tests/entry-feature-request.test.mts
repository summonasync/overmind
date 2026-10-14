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

test("featureRequest joins the type list after tombstone, mirroring the backend enum", () => {
  assert.deepEqual([...ENTRY_TYPES], [
    "note",
    "decision",
    "gate",
    "guard",
    "skill",
    "tombstone",
    "featureRequest",
  ]);
});

test("the editor type select and the list filter both render from the shared list", () => {
  const editor = source("../src/features/entries/components/entry-editor.tsx");
  const filters = source("../src/features/entries/components/entry-filters.tsx");
  assert.ok(editor.includes(`{ENTRY_TYPES.map((entryType) => (`));
  assert.ok(filters.includes(`{ENTRY_TYPES.map((type) => (`));
});

test("featureRequest maps to the idea tone with the lightbulb icon", () => {
  const meta = source("../src/shared/lib/entry-type.ts");
  assert.ok(
    meta.includes(`featureRequest: { tone: "idea", icon: Lightbulb, label: "Feature request" },`),
  );
  const badge = source("../src/shared/ui/badge.tsx");
  assert.ok(badge.includes(`idea: "bg-type-featureRequest/10 text-type-featureRequest",`));
});

test("featureRequest rows carry their own color token", () => {
  const css = source("../src/app/globals.css");
  assert.ok(css.includes("--color-type-featureRequest:"));
});

test("featureRequest renders as a live entry, not a tombstone", () => {
  assert.equal(entryTitleClasses("featureRequest"), "text-foreground");
  const live = entryRowClasses("note");
  assert.equal(entryRowClasses("featureRequest"), live);
});
