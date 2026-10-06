import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const source = (relativePath) =>
  readFileSync(new URL(relativePath, import.meta.url), "utf8");

test("the base layer styles the app-wide scrollbar thin on both engines", () => {
  const css = source("../src/app/globals.css");
  assert.ok(css.includes("scrollbar-width: thin;"));
  assert.ok(css.includes("scrollbar-color: var(--color-muted) transparent;"));
  assert.ok(css.includes("::-webkit-scrollbar {"));
  assert.ok(css.includes("::-webkit-scrollbar-track {"));
  assert.ok(css.includes("::-webkit-scrollbar-corner {"));
  assert.ok(css.includes("::-webkit-scrollbar-thumb {"));
});

test("the thumb rounds out and brightens on hover via color-mix", () => {
  const css = source("../src/app/globals.css");
  assert.ok(css.includes("border-radius: 9999px;"));
  assert.ok(
    css.includes(
      "color-mix(in oklab, var(--color-muted) 60%, var(--color-foreground))",
    ),
  );
});

test("the scrollbar block stays on theme tokens with no hardcoded hex", () => {
  const css = source("../src/app/globals.css");
  const start = css.indexOf("scrollbar-width: thin;");
  const end = css.indexOf("}", css.indexOf("::-webkit-scrollbar-thumb:hover"));
  assert.notEqual(start, -1);
  assert.notEqual(end, -1);
  assert.ok(!css.slice(start, end).includes("#"));
});
