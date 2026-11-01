import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const editor = () =>
  readFileSync(new URL("../src/shared/ui/markdown-editor.tsx", import.meta.url), "utf8");

test("the editor composes the shared viewer instead of a second markdown pipeline", () => {
  const source = editor();
  assert.ok(
    source.includes('from "@/shared/ui/markdown-viewer"'),
    "preview renders through MarkdownViewer from T0",
  );
  assert.ok(source.includes("<MarkdownViewer"), "the viewer is actually mounted");
  assert.ok(!source.includes("react-markdown"), "no second react-markdown import");
  assert.ok(!source.includes("remark"), "no second remark pipeline");
  assert.ok(!source.includes("dangerouslySetInnerHTML"), "no hand-built html sinks");
  assert.ok(
    source.includes('from "@/shared/ui/textarea"'),
    "the write pane is the shared Textarea",
  );
  assert.ok(
    source.includes('from "@/shared/ui/write-preview-toggle"'),
    "the toggle is the shared WritePreviewToggle",
  );
  assert.ok(!source.includes(">Write<"), "the editor never hand-rolls its own Write button");
});

test("preview trails the typing on a 200ms debounce", () => {
  const source = editor();
  assert.ok(source.includes("PREVIEW_DEBOUNCE_MS = 200"), "the debounce is 200ms, by name");
  const hook = source.slice(source.indexOf("useEffect"), source.indexOf("return ("));
  assert.ok(hook.includes("PREVIEW_DEBOUNCE_MS"), "the timer reads the named constant");
  assert.ok(hook.includes("setTimeout"), "the preview update is deferred");
  assert.ok(source.includes("clearTimeout"), "every keystroke cancels the pending update");
  assert.ok(source.includes("}, [value]"), "the timer re-arms on value, not on preview");
});

test("xl+ renders the panes side by side", () => {
  const source = editor();
  const rowStart = source.indexOf("flex min-h-0 flex-1 flex-col");
  const row = source.slice(rowStart, source.indexOf("</div>", rowStart));
  assert.ok(row.includes("xl:flex-row"), "the pane row turns horizontal at xl");
  assert.ok((source.match(/flex-1/g) ?? []).length >= 2, "both panes split the space evenly");
  assert.ok(source.includes("min-w-0"), "the preview pane can shrink below its content");
});

test("below xl the panes trade places through the Edit/Preview toggle", () => {
  const source = editor();
  const toggle = source.slice(source.indexOf("xl:hidden"), source.indexOf("flex min-h-0 flex-1 flex-col"));
  assert.ok(toggle.includes("<WritePreviewToggle"), "the toggle below xl is the shared one");

  const textareaStart = source.indexOf('"min-h-0 flex-1 resize-none"');
  const textareaBlock = source.slice(textareaStart, source.indexOf("<div", textareaStart));
  assert.ok(textareaBlock.includes("hidden xl:block"), "while previewing, the textarea hides until xl");

  const paneStart = source.indexOf("min-w-0 flex-1");
  const paneBlock = source.slice(paneStart, source.indexOf("</div>", paneStart));
  assert.ok(paneBlock.includes('showPreview ? "block"'), "toggling preview reveals the pane");
  assert.ok(paneBlock.includes("hidden xl:block"), "while writing, the preview still shows at xl");
});
