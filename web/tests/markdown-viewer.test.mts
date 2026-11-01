import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const viewer = () =>
  readFileSync(new URL("../src/shared/ui/markdown-viewer.tsx", import.meta.url), "utf8");

const manifest = () =>
  readFileSync(new URL("../package.json", import.meta.url), "utf8");

test("the viewer rides react-markdown with the gfm plugin and no raw html escape hatch", () => {
  const source = viewer();
  assert.ok(source.includes('from "react-markdown"'), "react-markdown is the only rendering engine");
  assert.ok(source.includes("remarkGfm"), "gfm comes from the plugin, not hand parsing");
  assert.ok(!source.includes("rehype-raw"), "raw html never reaches the dom");
  assert.ok(!source.includes("dangerouslySetInnerHTML"), "no hand-built html sinks");
});

test("prose blocks keep the shared markdown visual language", () => {
  const source = viewer();
  for (const tone of [
    "text-xl font-bold",
    "text-lg font-semibold",
    "text-base font-semibold",
    "text-sm font-semibold",
  ]) {
    assert.ok(source.includes(tone), `heading tone missing: ${tone}`);
  }
  assert.ok(source.includes("list-disc pl-5"), "unordered lists keep their markers");
  assert.ok(source.includes("list-decimal pl-5"), "ordered lists get numbers");
  assert.ok(
    source.includes("rounded-md border border-border bg-background p-3 font-mono"),
    "fences keep the boxed code chrome",
  );
  assert.ok(
    source.includes("border-l-2 border-border pl-3 text-muted"),
    "blockquotes read as quoted material",
  );
});

test("fences strip the inline-code chip from the code they carry", () => {
  const source = viewer();
  const pre = source.slice(source.indexOf("pre:"), source.indexOf("code:"));
  for (const reset of ["[&_code]:rounded-none", "[&_code]:bg-transparent", "[&_code]:px-0", "[&_code]:py-0"]) {
    assert.ok(pre.includes(reset), `the fence resets its inner code: ${reset}`);
  }
});

test("tables scroll inside the pane instead of blowing it out", () => {
  const source = viewer();
  const table = source.slice(source.indexOf("table:"), source.indexOf("th:"));
  assert.ok(table.includes("overflow-x-auto"), "the wrapper owns the horizontal scroll");
  assert.ok(table.includes("border-collapse"), "cells collapse into one grid");
  assert.ok(source.includes("border border-border px-2 py-1"), "cells share the grid border");
});

test("external links open a new tab that learns nothing about the opener", () => {
  const source = viewer();
  assert.ok(source.includes("/^https?:\\/\\//i"), "only absolute http(s) links count as external");
  assert.ok(source.includes('target="_blank"'), "external links open a new tab");
  assert.ok(source.includes('rel="noreferrer"'), "the referer stays home");
  assert.ok(
    source.includes("text-primary underline underline-offset-2"),
    "links keep the primary underline look",
  );
});

test("ceremony sections split on their tags and render through the shared toned block", () => {
  const source = viewer();
  assert.ok(
    source.includes('from "@/shared/lib/markdown-sections"'),
    "the shared section logic is the only ceremony source",
  );
  assert.ok(source.includes("isCeremonySection"), "unknown tags never hijack the split");
  assert.ok(source.includes("CeremonySectionBlock"), "sections render through the shared block");
  assert.ok(!/<section/.test(source), "no second section markup hides in the viewer");
});

test("the markdown stack is the two approved npm additions", () => {
  const dependencies = JSON.parse(manifest()).dependencies;
  assert.ok(dependencies["react-markdown"], "react-markdown rides in dependencies");
  assert.ok(dependencies["remark-gfm"], "remark-gfm rides in dependencies");
  assert.deepEqual(
    Object.keys(dependencies).filter((name) => name.includes("markdown") || name.includes("remark")),
    ["react-markdown", "remark-gfm"],
    "no third markdown package snuck in",
  );
});
