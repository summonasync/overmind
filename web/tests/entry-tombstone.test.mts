import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { ENTRY_TYPES } from "../src/shared/api/types.ts";
import { apiDelete } from "../src/shared/api/client.ts";
import {
  entryRowClasses,
  entryTitleClasses,
} from "../src/features/entries/lib/entry-read.ts";

const source = (relativePath) =>
  readFileSync(new URL(relativePath, import.meta.url), "utf8");

const tombstoneResponse = () => ({
  id: "9f1c6a50-0b17-4ad2-8ff3-2f6b0cf1a111",
  projectId: "p1",
  type: "tombstone",
  title: "Do it via cron (deleted)",
  content:
    "Deleted 2026-10-05 10:00:00 UTC via agent door by Van.\nReason: replaced by the queue\nOriginal type: decision\n\nFinal content:\n\n```\ncron: every night\n```",
  currentVersion: 1,
  createdAt: "2026-10-05T10:00:00Z",
  updatedAt: "2026-10-05T10:00:00Z",
  tags: [],
  skillFiles: [],
});

test("tombstone joins the entry type list as the fifth-plus-one citizen", () => {
  assert.deepEqual([...ENTRY_TYPES], [
    "note",
    "decision",
    "gate",
    "guard",
    "skill",
    "tombstone",
  ]);
});

test("the type filter renders its options from the shared type list", () => {
  const filters = source("../src/features/entries/components/entry-filters.tsx");
  assert.ok(filters.includes(`import { ENTRY_TYPES } from "@/shared/api/types";`));
  assert.ok(filters.includes(`{ENTRY_TYPES.map((type) => (`));
  assert.ok(filters.includes(`<option value="">All types</option>`));
});

test("the create form admits every shared type, tombstone included", () => {
  const editor = source("../src/features/entries/components/entry-editor.tsx");
  assert.ok(editor.includes(`{ENTRY_TYPES.map((entryType) => (`));
  const api = source("../src/features/entries/api.ts");
  assert.ok(api.includes("skillFiles: values.type === \"skill\" ? values.skillFiles : null,"));
});

test("tombstone maps to the muted badge tone with a distinct icon", () => {
  const meta = source("../src/shared/lib/entry-type.ts");
  assert.ok(meta.includes(`tombstone: { tone: "muted", icon: Skull, label: "Tombstone" },`));
  const badge = source("../src/shared/ui/badge.tsx");
  assert.ok(badge.includes(`muted: "border border-border/50 text-muted/70",`));
});

test("tombstone rows carry their own color token", () => {
  const css = source("../src/app/globals.css");
  assert.ok(css.includes("--color-type-tombstone:"));
});

test("tombstone rows render muted while live rows keep the raised hover", () => {
  assert.equal(entryTitleClasses("tombstone"), "text-muted");
  assert.equal(entryTitleClasses("note"), "text-foreground");

  const live = entryRowClasses("note");
  const buried = entryRowClasses("tombstone");
  assert.ok(live.includes("hover:bg-surface-raised"));
  assert.ok(buried.includes("bg-surface-raised/40"));
  assert.notEqual(live, buried);
});

test("delete parses the tombstone the backend returns on 200", async () => {
  const tombstone = tombstoneResponse();
  const calls = [];
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async (input, init) => {
    calls.push({ path: String(input), method: init.method });
    return {
      ok: true,
      status: 200,
      text: async () => JSON.stringify(tombstone),
    };
  };

  try {
    const result = await apiDelete("/entries/9f1c6a50-0b17-4ad2-8ff3-2f6b0cf1a111");
    assert.deepEqual(result, tombstone);
    assert.equal(result.type, "tombstone");
    assert.equal(calls.length, 1);
    assert.equal(calls[0].method, "DELETE");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("delete still tolerates the 204 of a tombstone plain delete", async () => {
  const originalFetch = globalThis.fetch;
  globalThis.fetch = async () => ({
    ok: true,
    status: 204,
    text: async () => "",
  });

  try {
    const result = await apiDelete("/entries/9f1c6a50-0b17-4ad2-8ff3-2f6b0cf1a111");
    assert.equal(result, undefined);
  } finally {
    globalThis.fetch = originalFetch;
  }
});

test("the entries api exposes the delete response as an optional tombstone", () => {
  const api = source("../src/features/entries/api.ts");
  assert.ok(api.includes("export function deleteEntry(id: string, reason?: string)"));
  assert.ok(api.includes("apiDelete<EntryResponse | undefined>(`/entries/${id}${query}`)"));
});

test("the delete dialog offers the optional reason for live entries only", () => {
  const page = source("../src/pages/entries-page/ui/entries-page.tsx");
  const api = source("../src/features/entries/api.ts");
  assert.ok(page.includes(`placeholder="Reason (optional) — recorded on the tombstone"`));
  assert.ok(page.includes(`deleting.type !== "tombstone"`));
  assert.ok(api.includes("encodeURIComponent(trimmed)"));
});

test("the delete dialog names the burial and the grave-removal paths", () => {
  const page = source("../src/pages/entries-page/ui/entries-page.tsx");
  assert.ok(page.includes(`deleting.type === "tombstone"`));
  assert.ok(page.includes("A tombstone entry will keep the memory of it"));
  assert.ok(page.includes("second tombstone is created"));
});

test("tombstone bodies keep the provenance header lines renderable", () => {
  const entry = tombstoneResponse();
  assert.equal(typeof entry.content, "string");
  assert.ok(entry.content.startsWith("Deleted "));
  assert.ok(entry.content.includes("Reason: "));
  assert.ok(entry.content.includes("Original type:"));
  assert.ok(entry.content.includes("Final content:"));
});
