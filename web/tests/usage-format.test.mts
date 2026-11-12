import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  formatCompactTokens,
  formatCost,
  formatCostShare,
  totalUsageTokens,
} from "../src/features/usage/lib/format.ts";
import { featureUsageRow } from "../src/features/usage/lib/usage-rows.ts";
import type { UsageSummaryResponse } from "../src/features/usage/types.ts";

const row = (overrides: Partial<Parameters<typeof totalUsageTokens>[0]> = {}) => ({
  inputTokens: 0,
  outputTokens: 0,
  cacheReadTokens: 0,
  cacheWriteTokens: 0,
  ...overrides,
});

test("total tokens sum all four token fields", () => {
  assert.equal(
    totalUsageTokens(row({ inputTokens: 100, outputTokens: 20, cacheReadTokens: 30, cacheWriteTokens: 5 })),
    155,
  );
});

test("compact tokens stay exact under a thousand", () => {
  assert.equal(formatCompactTokens(0), "0");
  assert.equal(formatCompactTokens(999), "999");
});

test("compact tokens scale to k and M with one trimmed decimal", () => {
  assert.equal(formatCompactTokens(1_000), "1k");
  assert.equal(formatCompactTokens(12_400), "12.4k");
  assert.equal(formatCompactTokens(3_100_000), "3.1M");
  assert.equal(formatCompactTokens(1_000_000), "1M");
});

test("cost shows three decimals under a dollar and two above", () => {
  assert.equal(formatCost(0.001), "$0.001");
  assert.equal(formatCost(0.25), "$0.250");
  assert.equal(formatCost(1.234), "$1.23");
  assert.equal(formatCost(12.5), "$12.50");
});

test("cost share rounds to whole percents and stays zero without a base", () => {
  assert.equal(formatCostShare(0.25, 1), "25%");
  assert.equal(formatCostShare(0.333, 0.999), "33%");
  assert.equal(formatCostShare(5, 0), "0%");
});

test("feature row lookup matches by slug and misses cleanly", () => {
  const summary: UsageSummaryResponse = {
    projectId: "p1",
    since: null,
    until: null,
    features: [
      {
        featureId: "f1",
        slug: "042-demo",
        records: 3,
        inputTokens: 10,
        outputTokens: 5,
        cacheReadTokens: 0,
        cacheWriteTokens: 0,
        cost: 0.5,
      },
    ],
    agents: [],
  };
  assert.equal(featureUsageRow(summary, "042-demo")?.cost, 0.5);
  assert.equal(featureUsageRow(summary, "999-other"), null);
});
