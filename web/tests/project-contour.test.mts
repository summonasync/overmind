import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const rail = () =>
  readFileSync(new URL("../src/widgets/app-shell/ui/projects-rail.tsx", import.meta.url), "utf8");

const tabs = () =>
  readFileSync(new URL("../src/widgets/app-shell/ui/project-tabs.tsx", import.meta.url), "utf8");

const tabPathnames = (source: string) =>
  [...source.matchAll(/pathname: "([^"]+)"/g)].map((match) => match[1]);

test("every project tab route keeps the left projects rail", () => {
  const source = rail();
  const contour = source.match(/const CONTOUR_PATHS = \[([^\]]*)\]/)?.[1] ?? "";
  assert.ok(contour.includes('"/"'));
  const pathnames = tabPathnames(tabs());
  assert.ok(pathnames.length > 0);
  for (const pathname of pathnames) {
    const unconditional = contour.includes(`"${pathname}"`);
    const paramGated = source.includes(`pathname === "${pathname}"`);
    assert.ok(unconditional || paramGated, `projects rail is gated off for ${pathname}`);
  }
});
