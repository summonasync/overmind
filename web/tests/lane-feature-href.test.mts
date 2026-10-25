import { strict as assert } from "node:assert";
import { test } from "node:test";
import { featureHref } from "../src/features/lanes/lib/lane-feature-href.ts";

test("the deep link opens the feature view on the dashboard, the route the board itself uses", () => {
  assert.equal(featureHref("p1", "028-x"), "/dashboard?project=p1&feature=028-x");
});

test("ids land in the query string whole even when they carry reserved characters", () => {
  assert.equal(
    featureHref("proj/one", "issue&9 x"),
    "/dashboard?project=proj%2Fone&feature=issue%269+x",
  );
});
