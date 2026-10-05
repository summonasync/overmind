import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const settings = () =>
  readFileSync(
    new URL("../src/features/projects/components/feature-number-settings.tsx", import.meta.url),
    "utf8",
  );

const numbering = () =>
  readFileSync(
    new URL("../src/features/projects/lib/use-project-numbering.ts", import.meta.url),
    "utf8",
  );

const api = () => readFileSync(new URL("../src/features/projects/api.ts", import.meta.url), "utf8");

const types = () =>
  readFileSync(new URL("../src/features/projects/types.ts", import.meta.url), "utf8");

const page = () =>
  readFileSync(new URL("../src/pages/projects-page/ui/projects-page.tsx", import.meta.url), "utf8");

const badge = () =>
  readFileSync(
    new URL("../src/features/planning/components/feature-number-badge.tsx", import.meta.url),
    "utf8",
  );

const taskNumber = () =>
  readFileSync(
    new URL("../src/features/planning/components/feature-number.tsx", import.meta.url),
    "utf8",
  );

const slugLabel = () =>
  readFileSync(
    new URL("../src/features/notifications/components/feature-slug-label.tsx", import.meta.url),
    "utf8",
  );

test("the response type carries the style and the prefix", () => {
  const source = types();
  assert.ok(source.includes("numberStyle: FeatureNumberStyle"));
  assert.ok(source.includes("numberPrefix: string | null"));
  assert.ok(source.includes("numberStyle?: FeatureNumberStyle"));
  assert.ok(source.includes("numberPrefix?: string"));
});

test("the update call only sends number fields when a style is chosen", () => {
  const source = api();
  assert.ok(source.includes("values.numberStyle !== undefined"));
  assert.ok(source.includes("values.numberStyle === \"key\" && values.numberPrefix !== undefined"));
  assert.ok(source.includes("{ numberPrefix: values.numberPrefix }"));
});

test("the section saves style always and prefix only for key", () => {
  const source = settings();
  assert.ok(source.includes(`numberStyle: style`));
  assert.ok(source.includes(`...(style === "key" ? { numberPrefix: trimmedPrefix } : {})`));
});

test("a key draft without a valid prefix cannot be saved", () => {
  const source = settings();
  assert.ok(source.includes("isValidFeatureNumberPrefix(trimmedPrefix)"));
  assert.ok(source.includes("disabled={update.isPending || !prefixReady}"));
  assert.ok(source.includes('maxLength={16}'));
});

test("the prefix input appears only for the key style", () => {
  const source = settings();
  assert.ok(source.includes('{style === "key" ? ('));
  assert.ok(source.includes('event.target.value.toUpperCase()'));
});

test("the preview renders on the real board slug", () => {
  const source = settings();
  assert.ok(source.includes("formatFeatureNumber(previewSlug, style, trimmedPrefix || null)"));
  assert.ok(source.includes('role="status"'));
  assert.ok(source.includes("{previewSlug}"));
});

test("the section follows server-side numbering changes", () => {
  const source = settings();
  assert.ok(source.includes("project.numberStyle !== syncedStyle"));
  assert.ok(source.includes("project.numberPrefix !== syncedPrefix"));
});

test("surfaces resolve numbering from the cached project list", () => {
  const source = numbering();
  assert.ok(source.includes("useProjectList()"));
  assert.ok(source.includes("DEFAULT_NUMBERING"));
  assert.ok(source.includes("{ style: project.numberStyle, prefix: project.numberPrefix }"));
});

test("the project settings surface renders the feature numbers section", () => {
  const source = page();
  assert.ok(source.includes("<FeatureNumberSettings key={detail.id} project={detail} previewSlug={previewSlug} />"));
  assert.ok(source.includes("Feature numbers"));
  assert.ok(source.includes("featureNumber(item.slug) !== null"));
});

test("every rendering surface formats through the shared formatter", () => {
  for (const source of [badge(), taskNumber(), slugLabel()]) {
    assert.ok(source.includes("formatFeatureNumber(slug, numbering.style, numbering.prefix)"));
    assert.ok(!source.includes("`#${"));
  }
});
