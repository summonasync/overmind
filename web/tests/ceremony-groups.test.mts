import { strict as assert } from "node:assert";
import { test } from "node:test";
import { groupCeremoniesByProject } from "../src/features/attention/lib/ceremony-groups.ts";
import type { CeremonyFeature } from "../src/features/attention/types.ts";

const feature = (overrides: Partial<CeremonyFeature>): CeremonyFeature => ({
  projectId: "p1",
  projectName: "Overmind",
  featureSlug: "101-spec-wait",
  number: "101",
  title: "Spec waits",
  reasons: ["spec awaiting validation"],
  url: "http://overmind.localhost/dashboard?project=p1&feature=101-spec-wait",
  ...overrides,
});

test("ceremonies group by project keeping first-seen order", () => {
  const groups = groupCeremoniesByProject([
    feature({}),
    feature({ projectId: "p2", projectName: "Squeezr", featureSlug: "07-reindex", number: "07" }),
    feature({ featureSlug: "102-plan-wait", number: "102", title: "Plan waits" }),
  ]);

  assert.deepEqual(
    groups.map((group) => group.projectName),
    ["Overmind", "Squeezr"],
  );
  assert.deepEqual(
    groups[0].features.map((row) => row.featureSlug),
    ["101-spec-wait", "102-plan-wait"],
  );
});

test("empty ceremony list collapses to no groups", () => {
  assert.deepEqual(groupCeremoniesByProject([]), []);
});
