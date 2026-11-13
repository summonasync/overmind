import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const settingsTypes = () =>
  readFileSync(new URL("../src/features/settings/types.ts", import.meta.url), "utf8");

const settingsApi = () =>
  readFileSync(new URL("../src/features/settings/api.ts", import.meta.url), "utf8");

const settingsMutations = () =>
  readFileSync(new URL("../src/features/settings/lib/use-settings.ts", import.meta.url), "utf8");

const digestSection = () =>
  readFileSync(
    new URL("../src/features/settings/components/human-digest-settings-section.tsx", import.meta.url),
    "utf8",
  );

const settingsPage = () =>
  readFileSync(new URL("../src/pages/settings-page/ui/settings-page.tsx", import.meta.url), "utf8");

const projectTypes = () =>
  readFileSync(new URL("../src/features/projects/types.ts", import.meta.url), "utf8");

const projectApi = () =>
  readFileSync(new URL("../src/features/projects/api.ts", import.meta.url), "utf8");

const projectHook = () =>
  readFileSync(new URL("../src/features/projects/lib/use-projects.ts", import.meta.url), "utf8");

const digestToggle = () =>
  readFileSync(
    new URL("../src/features/projects/components/project-human-digest-toggle.tsx", import.meta.url),
    "utf8",
  );

const projectsPage = () =>
  readFileSync(
    new URL("../src/pages/projects-settings-page/ui/projects-settings-page.tsx", import.meta.url),
    "utf8",
  );

test("the settings response carries the global digest flag", () => {
  const source = settingsTypes();
  assert.ok(source.includes("humanDigestEnabled: boolean"));
  assert.ok(source.includes("HumanDigestSettingsValues"));
});

test("the api layer sends only the digest field of the settings payload", () => {
  const source = settingsApi();
  const digestCall = source.slice(source.indexOf("updateHumanDigestSettings"));
  assert.ok(digestCall.includes('apiPut<UpdateSettingsResponse>("/settings"'));
  assert.ok(digestCall.includes("humanDigestEnabled: values.humanDigestEnabled"));
  assert.ok(!digestCall.includes("telegramEnabled"), "digest update must not touch other settings");
  assert.ok(!digestCall.includes("agentModel"), "digest update must not touch other settings");
});

test("the digest mutation invalidates the settings query", () => {
  const source = settingsMutations();
  assert.ok(source.includes("updateHumanDigestSettings(values)"));
  assert.ok(source.includes("digestSave"));
});

test("the settings hub renders the digest switch with the per-project hint", () => {
  const source = digestSection();
  assert.ok(source.includes('type="checkbox"'));
  assert.ok(source.includes("checked={settings.humanDigestEnabled}"));
  assert.ok(source.includes("disabled={digestSave.isPending}"));
  assert.ok(source.includes("humanDigestEnabled: event.target.checked"));
  assert.ok(source.includes("Per-project switches decide which projects opt in"));
});

test("the settings page renders the digest section", () => {
  const source = settingsPage();
  assert.ok(source.includes("<HumanDigestSettingsSection settings={settings} />"));
});

test("the project response carries the digest flag", () => {
  const source = projectTypes();
  assert.ok(source.includes("humanDigestEnabled: boolean"));
  assert.ok(source.includes("ProjectHumanDigestResponse"));
});

test("the api layer posts the enabled state to the human-digest endpoint", () => {
  const source = projectApi();
  assert.ok(source.includes("apiPost<ProjectHumanDigestResponse>(`/projects/${id}/human-digest`"));
  assert.ok(source.includes("{ enabled }"));
});

test("the toggle mutation invalidates the project list query", () => {
  const source = projectHook();
  assert.ok(source.includes("setProjectHumanDigest(id, enabled)"));
  assert.ok(source.includes("useProjectHumanDigestToggle"));
  assert.ok(source.includes("queryKey: projectKeys.lists()"));
});

test("the toggle renders the flag as a checkbox with the global-switch hint", () => {
  const source = digestToggle();
  assert.ok(source.includes('type="checkbox"'));
  assert.ok(source.includes("checked={project.humanDigestEnabled}"));
  assert.ok(source.includes("disabled={toggle.isPending}"));
  assert.ok(source.includes("enabled: event.target.checked"));
  assert.ok(source.includes("global digest switch in settings must also be on"));
});

test("the project settings page renders the digest section", () => {
  const source = projectsPage();
  assert.ok(source.includes("<ProjectHumanDigestToggle key={detail.id} project={detail} />"));
  assert.ok(source.includes("Human digest"));
});
