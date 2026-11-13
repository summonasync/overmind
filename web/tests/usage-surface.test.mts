import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const board = () =>
  readFileSync(
    new URL("../src/features/planning/components/feature-board.tsx", import.meta.url),
    "utf8",
  );

const workspace = () =>
  readFileSync(
    new URL("../src/features/planning/components/feature-workspace.tsx", import.meta.url),
    "utf8",
  );

const queries = () =>
  readFileSync(new URL("../src/features/usage/queries.ts", import.meta.url), "utf8");

const api = () => readFileSync(new URL("../src/features/usage/api.ts", import.meta.url), "utf8");

const chip = () =>
  readFileSync(new URL("../src/features/usage/components/usage-chip.tsx", import.meta.url), "utf8");

const section = () =>
  readFileSync(
    new URL("../src/features/usage/components/feature-usage-section.tsx", import.meta.url),
    "utf8",
  );

test("board rows carry the usage chip next to the claim chip", () => {
  const source = board();
  assert.ok(source.includes(`<UsageChip projectId={projectId} slug={item.slug} />`));
  assert.ok(source.includes(`import { UsageChip } from "@/features/usage";`));
});

test("result tab hosts the usage section, other tabs do not", () => {
  const source = workspace();
  assert.ok(source.includes(`{tab === "result" ? <FeatureUsageSection projectId={projectId} slug={slug} /> : null}`));
  assert.ok(source.includes(`import { FeatureUsageSection } from "@/features/usage";`));
});

test("usage query keys follow the domain factory pattern", () => {
  const source = queries();
  assert.ok(source.includes(`all: ["usage"] as const,`));
  assert.ok(source.includes(`summary: (projectId: string, since?: string, until?: string)`));
  assert.ok(source.includes(`enabled: projectId.length > 0,`));
});

test("usage fetches go through the shared BFF client", () => {
  const source = api();
  assert.ok(source.includes(`apiGet`));
  assert.ok(!source.includes("http://") && !source.includes("https://"));
});

test("chip renders nothing without a usage row or when all token counts are zero", () => {
  const source = chip();
  assert.ok(source.includes(`if (row === null || tokens === 0) {`));
  assert.ok(source.includes(`return null;`));
});

test("usage section lists agents with a project cost share", () => {
  const source = section();
  assert.ok(source.includes(`formatCostShare(agent.cost, projectCost)`));
  assert.ok(source.includes(`Agents · project share`));
});
