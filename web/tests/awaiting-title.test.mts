import { strict as assert } from "node:assert";
import { test } from "node:test";
import { awaitingTitle } from "../src/widgets/app-shell/lib/awaiting-title.ts";

test("counter prefixes the base title while items await", () => {
  assert.equal(awaitingTitle("Overmind", 1), "(1) Overmind");
  assert.equal(awaitingTitle("Overmind", 12), "(12) Overmind");
});

test("empty awaiting set keeps the bare title", () => {
  assert.equal(awaitingTitle("Overmind", 0), "Overmind");
});

test("negative counts degrade to the bare title", () => {
  assert.equal(awaitingTitle("Overmind", -3), "Overmind");
});
