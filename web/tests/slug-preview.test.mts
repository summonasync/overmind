import { strict as assert } from "node:assert";
import { test } from "node:test";
import { slugPreview } from "../src/features/planning/lib/slug-preview.ts";

test("a blank slug with a title previews the composed slug", () => {
  assert.equal(
    slugPreview({ title: "Hello World", slug: "", takenSlugs: ["001-alpha", "002-beta"] }),
    "would create: 003-hello-world",
  );
});

test("the preview recomputes against the taken slugs", () => {
  assert.equal(
    slugPreview({ title: "Привет мир", slug: "", takenSlugs: ["168-one"] }),
    "would create: 169-privet-mir",
  );
  assert.equal(
    slugPreview({ title: "Привет мир", slug: "", takenSlugs: [] }),
    "would create: 001-privet-mir",
  );
});

test("a manual slug overrides the preview", () => {
  assert.equal(
    slugPreview({ title: "Hello World", slug: "042-my-slug", takenSlugs: ["001-alpha"] }),
    null,
  );
});

test("a blank title hides the preview", () => {
  assert.equal(slugPreview({ title: "", slug: "", takenSlugs: [] }), null);
  assert.equal(slugPreview({ title: "   ", slug: "", takenSlugs: [] }), null);
});

test("whitespace around the inputs is trimmed before composing", () => {
  assert.equal(
    slugPreview({ title: "  Hello World  ", slug: " ", takenSlugs: [] }),
    "would create: 001-hello-world",
  );
});
