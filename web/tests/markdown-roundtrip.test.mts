import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";

const fixture = () =>
  readFileSync(new URL("./fixtures/markdown-roundtrip.md", import.meta.url), "utf8");

const viewer = () =>
  readFileSync(new URL("../src/shared/ui/markdown-viewer.tsx", import.meta.url), "utf8");

const editor = () =>
  readFileSync(new URL("../src/shared/ui/markdown-editor.tsx", import.meta.url), "utf8");

const render = (content: string) =>
  renderToStaticMarkup(React.createElement(ReactMarkdown, { remarkPlugins: [remarkGfm] }, content));

test("a persisted body renders with every block present", () => {
  const html = render(fixture());
  const blocks = [
    "<h2>Round-trip fixture</h2>",
    "<strong>bold</strong>",
    "<em>italic</em>",
    "<code>inline code</code>",
    'href="https://example.com/docs"',
    "<li>alpha item</li>",
    "<li>first step</li>",
    'class="language-ts"',
    "const roundtrip =",
    "byte-identical",
    "<blockquote>",
    "Quoted note that must survive the swap.",
    "<table>",
    "<td>gfm</td>",
    "<td>none</td>",
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

test("once the ceremony wrapper is split off, its content renders as markdown again", () => {
  const inner = fixture().match(/<goal>\n([\s\S]*?)<\/goal>/)?.[1];
  assert.ok(inner, "the fixture carries a goal section");
  const html = render(inner);
  assert.ok(html.includes("<li>keep the heading</li>"), "the section list keeps its items");
  assert.ok(html.includes("<li>keep the list</li>"), "the section list keeps every line");
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
