import { strict as assert } from "node:assert";
import { test } from "node:test";
import { projectAncestorPaths } from "../src/features/projects/lib/tree-paths.ts";

const ROOT = "/home/dev/workspaces";

test("a project directly inside a root expands only the root", () => {
  assert.deepEqual(projectAncestorPaths([ROOT], `${ROOT}/overmind`), [ROOT]);
});

test("nested projects expand every folder on the way down", () => {
  assert.deepEqual(projectAncestorPaths([ROOT], `${ROOT}/clients/acme/site`), [
    ROOT,
    `${ROOT}/clients`,
    `${ROOT}/clients/acme`,
  ]);
});

test("paths outside the registered roots expand nothing", () => {
  assert.deepEqual(projectAncestorPaths([ROOT], "/opt/elsewhere/project"), []);
});

test("a project sitting on a root needs no expansion", () => {
  assert.deepEqual(projectAncestorPaths([ROOT], ROOT), []);
});

test("the most specific root wins when roots are nested", () => {
  const nested = `${ROOT}/clients`;
  assert.deepEqual(projectAncestorPaths([ROOT, nested], `${nested}/acme/site`), [
    nested,
    `${nested}/acme`,
  ]);
});

test("trailing slashes do not break the chain", () => {
  assert.deepEqual(projectAncestorPaths([`${ROOT}/`], `${ROOT}/apps/app/`), [ROOT, `${ROOT}/apps`]);
});

test("a sibling sharing a name prefix is not treated as a parent", () => {
  assert.deepEqual(projectAncestorPaths([`${ROOT}/clients`], `${ROOT}/clients-archive/project`), []);
});

test("a filesystem root can serve as a tree root", () => {
  assert.deepEqual(projectAncestorPaths(["/"], "/srv/projects/app"), ["/", "/srv", "/srv/projects"]);
});

test("an empty project path expands nothing", () => {
  assert.deepEqual(projectAncestorPaths([ROOT], ""), []);
});
