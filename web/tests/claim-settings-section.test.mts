import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const section = () =>
  readFileSync(
    new URL("../src/features/projects/components/claim-settings-section.tsx", import.meta.url),
    "utf8",
  );

const hook = () =>
  readFileSync(new URL("../src/features/projects/lib/use-claim-settings.ts", import.meta.url), "utf8");

const api = () => readFileSync(new URL("../src/features/projects/api.ts", import.meta.url), "utf8");

const types = () =>
  readFileSync(new URL("../src/features/projects/types.ts", import.meta.url), "utf8");

const page = () =>
  readFileSync(new URL("../src/pages/projects-page/ui/projects-page.tsx", import.meta.url), "utf8");

test("the response type carries the threshold and the rules text", () => {
  const source = types();
  assert.ok(source.includes("claimStaleThresholdMinutes: number"));
  assert.ok(source.includes("agentNamingRules: string | null"));
});

test("the api layer reads and writes the claim settings endpoint", () => {
  const source = api();
  assert.ok(source.includes("apiGet<ClaimSettingsResponse>(`/projects/${id}/claim-settings`)"));
  assert.ok(source.includes("apiPut<ClaimSettingsResponse>(`/projects/${id}/claim-settings`"));
  assert.ok(source.includes("agentNamingRules: values.agentNamingRules"));
});

test("the read hook resolves through the claim settings query options", () => {
  const source = hook();
  assert.ok(source.includes("useQuery(claimSettingsQueryOptions(projectId))"));
  assert.ok(source.includes("invalidateQueries({ queryKey: projectKeys.claimSettings(projectId) })"));
});

test("the section renders the saved rules text", () => {
  const source = section();
  assert.ok(source.includes('settings.agentNamingRules ?? ""'));
  assert.ok(source.includes("maxLength={2000}"));
  assert.ok(source.includes('aria-label="Agent naming rules"'));
});

test("saving posts the entered text and keeps the threshold untouched", () => {
  const source = section();
  assert.ok(source.includes("claimStaleThresholdMinutes: settings.claimStaleThresholdMinutes"));
  assert.ok(source.includes("agentNamingRules: rules"));
  assert.ok(source.includes("disabled={update.isPending}"));
});

test("the project settings surface renders the claim settings section", () => {
  const source = page();
  assert.ok(source.includes("<ClaimSettingsSection projectId={detail.id} />"));
  assert.ok(source.includes("Claim settings"));
});
