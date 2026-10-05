import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const sidebar = () =>
  readFileSync(new URL("../src/widgets/app-shell/ui/app-sidebar.tsx", import.meta.url), "utf8");

const tree = () =>
  readFileSync(new URL("../src/features/projects/components/project-tree.tsx", import.meta.url), "utf8");

test("the projects row shares the nav row geometry", () => {
  const source = sidebar();
  const geometry =
    "flex items-center gap-2.5 rounded-md border-l-2 px-3 py-2 text-sm font-medium transition-colors";
  assert.equal(source.split(geometry).length - 1, 2);
  const activeTone = "border-primary bg-primary/10 text-primary";
  assert.equal(source.split(activeTone).length - 1, 2);
  const idleTone = "border-transparent text-muted hover:bg-surface-raised hover:text-foreground";
  assert.equal(source.split(idleTone).length - 1, 2);
});

test("the chevron lives inside the row, right of the label", () => {
  const source = sidebar();
  const from = source.indexOf('item.href === "/") {');
  const branch = source.slice(from, source.indexOf('item.href === "/attention"', from));
  const linkAt = branch.indexOf("<a");
  const chevronAt = branch.indexOf("aria-expanded");
  assert.ok(linkAt > -1);
  assert.ok(chevronAt > linkAt);
  assert.ok(
    branch.includes(
      'className="flex min-w-0 flex-1 items-center gap-2.5 rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"',
    ),
  );
  assert.ok(
    branch.includes('aria-label={projectsVisible ? "Collapse projects tree" : "Expand projects tree"}'),
  );
});

test("a project route holds the section open over the saved preference", () => {
  const source = sidebar();
  assert.ok(source.includes("const projectsVisible = openOverride ?? (routeHasProject || projectsOpen);"));
  assert.ok(source.includes('setRouteHasProject(new URLSearchParams(window.location.search).has("project"))'));
});

test("toggling on a project page stays visit-local, elsewhere it persists", () => {
  const source = sidebar();
  assert.ok(
    source.includes(
      "if (routeHasProject) {\n      setOpenOverride(next);\n      return;\n    }\n    setProjectsOpen(next);\n    saveProjectsSectionOpen(next);",
    ),
  );
});

test("the tree expands toward the open project on top of the saved paths", () => {
  const source = tree();
  assert.ok(source.includes("expandedPaths.includes(path) || autoExpandedPaths.includes(path)"));
  assert.ok(source.includes("projectAncestorPaths("));
  assert.ok(source.includes("projects.find((project) => project.id === activeProjectId)?.diskPath ?? null"));
});

test("collapsing an auto-expanded folder stays local to the route", () => {
  const source = tree();
  assert.ok(source.includes("setRouteCollapsed((paths) => [...paths, path])"));
  assert.ok(source.includes("setRouteCollapsed((paths) => paths.filter((value) => value !== path))"));
});

test("auto-expansion never rewrites the saved session state", () => {
  const source = tree();
  assert.ok(source.includes("saveExpandedPaths(expandedPaths)"));
  assert.equal(source.includes("saveExpandedPaths(autoExpandedPaths"), false);
});

test("the selection follows the project parameter on any page", () => {
  const source = tree();
  assert.ok(source.includes("activeProjectId={activeProjectId}"));
  assert.equal(source.includes("treeActiveProjectId"), false);
  assert.equal(source.includes('pathname === "/entries"'), false);
});

test("project rows render the nav rhythm with the primary highlight", () => {
  const source = tree();
  assert.ok(
    source.includes(
      '"flex min-w-0 flex-1 items-center gap-1.5 rounded-md border-l-2 px-1 py-1 text-xs font-medium transition-colors"',
    ),
  );
  assert.ok(source.includes('"border-primary bg-primary/10 text-primary"'));
  assert.ok(source.includes('"border-transparent text-muted hover:bg-surface-raised hover:text-foreground"'));
  assert.ok(source.includes('aria-current={active ? "page" : undefined}'));
});
