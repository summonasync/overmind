import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

// f8cc73a removed the sidebar; the projects surface now lives in the rail and the tree.
const rail = () =>
  readFileSync(new URL("../src/widgets/app-shell/ui/projects-rail.tsx", import.meta.url), "utf8");

const tree = () =>
  readFileSync(new URL("../src/features/projects/components/project-tree.tsx", import.meta.url), "utf8");

test("project rows carry the same highlight pair across the tree and the rail list", () => {
  // f8cc73a split the old nav rhythm into two row sites; both must keep the active/idle tones.
  for (const source of [tree(), rail()]) {
    assert.ok(source.includes('"border-primary bg-primary/10 text-primary"'));
    assert.ok(source.includes('"border-transparent text-muted hover:bg-surface-raised hover:text-foreground"'));
  }
});

test("the folder chevron lives inside the row with a stateful label", () => {
  // f8cc73a moved the projects chevron from the nav row into each tree folder row, left of the label.
  const source = tree();
  const from = source.indexOf(
    '"group flex min-w-0 items-center gap-0.5 rounded text-muted transition-colors hover:text-foreground"',
  );
  const chevronAt = source.indexOf("aria-expanded={expanded}", from);
  const labelAt = source.indexOf("title={entry.path}", chevronAt);
  assert.ok(from > -1);
  assert.ok(chevronAt > from);
  assert.ok(labelAt > chevronAt);
  assert.ok(source.includes("aria-label={expanded ? `Collapse ${entry.name}` : `Expand ${entry.name}`}"));
  assert.ok(source.includes("{expanded ? <ChevronDown size={14} aria-hidden /> : <ChevronRight size={14} aria-hidden />}"));
});

test("a project route mounts the rail over the default contour", () => {
  // f8cc73a replaced the force-open section with rail mounting; a ?project route still gets the rail.
  const source = rail();
  assert.ok(source.includes('const onIssues = pathname === "/issues" && (searchParams?.has("project") ?? false);'));
  assert.ok(source.includes("if (!onContour && !onIssues) {\n    return null;\n  }"));
});

test("collapsing a route-implied folder stays visit-local, user toggles persist", () => {
  // f8cc73a killed the visit-local section toggle; the tree toggle keeps the split on route-implied paths.
  const source = tree();
  assert.ok(
    source.includes(
      "if (autoExpandedPaths.includes(path)) {\n        setRouteCollapsed((paths) => [...paths, path]);",
    ),
  );
  assert.ok(source.includes('} else {\n        setExpandedPaths((paths) => paths.filter((value) => value !== path));'));
  assert.equal(source.includes("saveRouteCollapsed"), false);
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

test("tree project rows render the rail rhythm with the primary highlight", () => {
  // f8cc73a re-contracted the row from the compact nav rhythm to the rail rhythm.
  const source = tree();
  assert.ok(
    source.includes(
      '"flex min-w-0 flex-1 items-center gap-2 rounded-md border-l-2 px-2 py-2 text-sm font-medium transition-colors"',
    ),
  );
  assert.ok(source.includes('? "border-primary bg-primary/10 text-primary"'));
  assert.ok(source.includes(': "border-transparent text-muted hover:bg-surface-raised hover:text-foreground"'));
  assert.ok(source.includes('aria-current={active ? "page" : undefined}'));
});
