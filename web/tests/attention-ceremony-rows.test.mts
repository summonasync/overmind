import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const section = () =>
  readFileSync(
    new URL("../src/pages/attention-page/ui/ceremonies-section.tsx", import.meta.url),
    "utf8",
  );

test("ceremony rows render the feature number through the shared badge", () => {
  const source = section();
  assert.ok(source.includes(`import { FeatureNumberBadge } from "@/features/planning";`));
  assert.ok(source.includes(`<FeatureNumberBadge slug={feature.featureSlug} />`));
  assert.ok(!source.includes(`>{feature.number}</span>`));
});

test("ceremony row titles link to the feature url with card-style hover", () => {
  const source = section();
  assert.ok(source.includes(`href={feature.url}`));
  assert.ok(source.includes("group-hover:text-primary"));
  assert.ok(source.includes("focus-visible:ring-2 focus-visible:ring-primary/40"));
});

test("the Open affordance stays untouched on ceremony rows", () => {
  const source = section();
  assert.ok(source.includes("Open →"));
  assert.ok(source.includes("text-xs font-medium text-primary hover:underline"));
});
