import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const filterBar = () =>
  readFileSync(new URL("../src/features/planning/components/board-filter-bar.tsx", import.meta.url), "utf8");

const fastChips = () =>
  readFileSync(new URL("../src/features/planning/components/board-fast-chips.tsx", import.meta.url), "utf8");

const searchInput = () =>
  readFileSync(new URL("../src/features/planning/components/board-search-input.tsx", import.meta.url), "utf8");

const filterChip = () =>
  readFileSync(new URL("../src/features/planning/components/board-filter-chip.tsx", import.meta.url), "utf8");

test("the toolbar is two stacked rows", () => {
  const source = filterBar();
  assert.ok(source.includes('className="flex flex-col gap-2.5 rounded-lg border border-border bg-surface p-3"'));
  assert.ok(source.includes('className="flex flex-wrap items-center gap-2"'));
});

test("row 1 owns the filters: statuses, priorities and artifact selects", () => {
  const source = filterBar();
  assert.ok(source.includes('aria-label="Filter by feature status"'));
  assert.ok(source.includes('aria-label="Filter by priority"'));
  assert.ok(source.includes('aria-label="Spec filter"'));
  assert.ok(source.includes('aria-label="Plan filter"'));
});

test("row 2 pairs the search with the fast chips", () => {
  const source = filterBar();
  const fastRow = source.slice(source.lastIndexOf('className="flex flex-wrap items-center gap-2"'));
  assert.ok(fastRow.includes("<BoardSearchInput"));
  assert.ok(fastRow.includes("<BoardFastChips"));
});

test("fast chips are the three toggles with pressed state", () => {
  const source = fastChips();
  assert.deepEqual(
    [...source.matchAll(/id: "(current|hideDone|blocked)"/g)].map((match) => match[1]),
    ["current", "hideDone", "blocked"],
  );
  assert.ok(source.includes('aria-label="Fast board filters"'));
  const chip = filterChip();
  assert.ok(chip.includes("aria-pressed={active}"));
});

test("064 pin: Enter still applies the search", () => {
  const source = searchInput();
  assert.ok(source.includes('<form\n      role="search"'));
  assert.ok(source.includes('type="submit"'));
  assert.ok(source.includes("onApply(value)"));
});

test("064 pin: Escape clears the draft, Shift+Escape clears search and filters", () => {
  const source = searchInput();
  assert.ok(source.includes('event.key !== "Escape"'));
  assert.ok(source.includes("if (event.shiftKey)"));
  assert.match(source, /if \(event\.shiftKey\) \{\n\s*onClearAll\(\);\n\s*\} else \{\n\s*onClear\(\);/);
});
