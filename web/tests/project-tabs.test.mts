import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const tabs = () =>
  readFileSync(new URL("../src/widgets/app-shell/ui/project-tabs.tsx", import.meta.url), "utf8");

test("issues is the third project tab after dashboard and entries", () => {
  const source = tabs();
  const dashboard = source.indexOf('{ key: "dashboard"');
  const entries = source.indexOf('{ key: "entries"');
  const issues = source.indexOf('{ key: "issues"');
  assert.ok(dashboard > -1);
  assert.ok(entries > dashboard);
  assert.ok(issues > entries);
});

test("the issues tab rides the same project deep link as the other tabs", () => {
  const source = tabs();
  assert.ok(source.includes('{ key: "issues", label: "Issues & requests", pathname: "/issues" }'));
  assert.ok(source.includes("href: projectId !== null ? `${route.pathname}?project="));
});
