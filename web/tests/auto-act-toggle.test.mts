import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const component = () =>
  readFileSync(
    new URL("../src/features/projects/components/project-auto-act-toggle.tsx", import.meta.url),
    "utf8",
  );

const hook = () =>
  readFileSync(new URL("../src/features/projects/lib/use-projects.ts", import.meta.url), "utf8");

const api = () => readFileSync(new URL("../src/features/projects/api.ts", import.meta.url), "utf8");

const types = () =>
  readFileSync(new URL("../src/features/projects/types.ts", import.meta.url), "utf8");

const page = () =>
  readFileSync(
    new URL("../src/pages/projects-settings-page/ui/projects-settings-page.tsx", import.meta.url),
    "utf8",
  );

test("the project response carries the auto-act flag", () => {
  const source = types();
  assert.ok(source.includes("autoActOnReplies: boolean"));
});

test("the api layer posts the enabled state to the auto-act endpoint", () => {
  const source = api();
  assert.ok(source.includes("apiPost<ProjectAutoActResponse>(`/projects/${id}/auto-act`"));
  assert.ok(source.includes("{ enabled }"));
});

test("the toggle mutation invalidates the project list query", () => {
  const source = hook();
  assert.ok(source.includes("setProjectAutoAct(id, enabled)"));
  assert.ok(
    source.includes(
      "mutationFn: ({ id, enabled }: { id: string; enabled: boolean }) =>",
    ),
  );
  assert.ok(source.includes("queryKey: projectKeys.lists()"));
});

test("the toggle renders the flag as a checkbox with the hint copy", () => {
  const source = component();
  assert.ok(source.includes('type="checkbox"'));
  assert.ok(source.includes("checked={project.autoActOnReplies}"));
  assert.ok(source.includes("Auto-act on replies"));
  assert.ok(
    source.includes("A human reply on an approval notification validates the artifact"),
  );
});

test("the toggle disables the checkbox while the mutation is pending", () => {
  const source = component();
  assert.ok(source.includes("disabled={toggle.isPending}"));
  assert.ok(source.includes("enabled: event.target.checked"));
});

test("the project settings page renders the auto-act section", () => {
  const source = page();
  assert.ok(source.includes("<ProjectAutoActToggle key={detail.id} project={detail} />"));
  assert.ok(source.includes("Auto-act"));
});
