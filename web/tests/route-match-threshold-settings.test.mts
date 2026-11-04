import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const types = () => readFileSync(new URL("../src/features/settings/types.ts", import.meta.url), "utf8");

const api = () => readFileSync(new URL("../src/features/settings/api.ts", import.meta.url), "utf8");

const form = () =>
  readFileSync(new URL("../src/features/settings/components/settings-form.tsx", import.meta.url), "utf8");

test("the settings types carry the route match threshold", () => {
  const source = types();
  const matches = source.match(/agentRouteMatchThreshold: number;/g) ?? [];
  assert.ok(matches.length >= 2, "response and form types both carry the threshold");
});

test("the settings api maps the threshold into the update payload", () => {
  const source = api();
  assert.ok(source.includes("agentRouteMatchThreshold: values.agentRouteMatchThreshold"));
});

test("the settings form gates the threshold to the open interval and sends it", () => {
  const source = form();
  assert.ok(source.includes("agentRouteMatchThreshold"));
  assert.ok(source.includes("Route match threshold"));
  assert.ok(source.includes("routeMatchThreshold <= 0 || routeMatchThreshold >= 1"));
  assert.ok(source.includes("agentRouteMatchThreshold: routeMatchThreshold"));
});
