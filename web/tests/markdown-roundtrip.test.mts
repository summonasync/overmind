import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { createJiti } from "jiti";

// the viewer carries no React import, so the transform needs the automatic jsx runtime
const jiti = createJiti(import.meta.url, {
  jsx: { runtime: "automatic", importSource: "react" },
  alias: { "@": new URL("../src/", import.meta.url).href },
});

const { MarkdownViewer } = await jiti.import("../src/shared/ui/markdown-viewer.tsx");

const fixture = () =>
  readFileSync(new URL("./fixtures/markdown-roundtrip.md", import.meta.url), "utf8");

const viewer = () =>
  readFileSync(new URL("../src/shared/ui/markdown-viewer.tsx", import.meta.url), "utf8");

const editor = () =>
  readFileSync(new URL("../src/shared/ui/markdown-editor.tsx", import.meta.url), "utf8");

const render = (content: string) =>
  renderToStaticMarkup(React.createElement(MarkdownViewer, { content }));

test("a persisted body renders through the shared viewer with every block present", () => {
  const html = render(fixture());
  const blocks = [
    '<h2 class="mt-3 mb-1 text-lg font-semibold">Round-trip fixture</h2>',
    '<strong class="font-semibold">bold</strong>',
    "<em>italic</em>",
    '<code class="rounded bg-surface-raised px-1 py-0.5 font-mono text-[0.85em] text-foreground">inline code</code>',
    'href="https://example.com/docs"',
    "<li>alpha item</li>",
    "<li>first step</li>",
    "const roundtrip =",
    "byte-identical",
    '<blockquote class="my-2 border-l-2 border-border pl-3 text-muted">',
    "Quoted note that must survive the swap.",
    '<td class="border border-border px-2 py-1 align-top">gfm</td>',
    '<td class="border border-border px-2 py-1 align-top">none</td>',
  ];
  for (const block of blocks) {
    assert.ok(html.includes(block), `block missing from the render: ${block}`);
  }
});

test("ceremony and tombstone lines ride through as escaped literals, never as html", () => {
  const html = render(fixture());
  for (const literal of ["&lt;goal&gt;", "&lt;/goal&gt;", "&lt;removed 2026-11-01 — tombstoned for a reason&gt;"]) {
    assert.ok(html.includes(literal), `literal missing from the render: ${literal}`);
  }
  assert.ok(!/<goal>/.test(html), "no raw goal tag reaches the dom");
});

test("once the viewer splits the ceremony wrapper, its content renders as markdown inside the toned pane", () => {
  const html = render(fixture());
  const section = html.indexOf('aria-label="goal section"');
  assert.ok(section > -1, "the goal section renders through the shared toned block");
  const pane = html.slice(section, html.indexOf("</section>", section));
  assert.ok(pane.includes("<li>keep the heading</li>"), "the section list keeps its items");
  assert.ok(pane.includes("<li>keep the list</li>"), "the section list keeps every line");
});

test("the editor hands the persisted value over untouched", () => {
  const source = editor();
  assert.ok(source.includes("value={value}"), "the textarea echoes the incoming prop verbatim");
  assert.ok(
    source.includes("onValueChange(event.target.value)"),
    "keystrokes report the raw textarea value back",
  );
  assert.ok(source.includes("setPreview(value)"), "the preview renders the raw value");
  assert.ok(
    !/\.(replace|replaceAll|normalize|trimEnd|trimStart|escape)\(/.test(source),
    "nothing on the value path rewrites content",
  );
});

test("the viewer splits ceremony sections without editing what sits between them", () => {
  const source = viewer();
  assert.ok(source.includes("{chunk.content}"), "chunks reach the markdown engine untouched");
  assert.ok(source.includes("tag={chunk.section}"), "ceremony tags re-render through the shared block");
  assert.ok(
    !/\.(replace|replaceAll|normalize|escape)\(/.test(source),
    "the viewer never rewrites content",
  );
  assert.ok(source.includes("content: singleLine[2]"), "single-line sections keep their full remainder");
  assert.ok(source.includes("sectionLines.join"), "open sections keep every line before the close tag");
});
