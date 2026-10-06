import { strict as assert } from "node:assert";
import { test } from "node:test";
import { attentionToastCopy } from "../src/widgets/app-shell/lib/attention-toast-copy.ts";

const event = (overrides: Partial<Parameters<typeof attentionToastCopy>[1]> = {}) => ({
  agent: "coder",
  featureSlug: "095-attention",
  text: "Spec needs your validation",
  ...overrides,
});

test("the project leads, feature and agent stay secondary", () => {
  const copy = attentionToastCopy("Mindlash Studio", event());
  assert.equal(copy.title, "Mindlash Studio");
  assert.equal(copy.body, "095-attention · coder\nSpec needs your validation");
});

test("no project resolves to the app name", () => {
  assert.equal(attentionToastCopy(null, event()).title, "Overmind");
});

test("missing feature slug keeps the agent line", () => {
  const copy = attentionToastCopy("Mindlash Studio", event({ featureSlug: null }));
  assert.equal(copy.body, "coder\nSpec needs your validation");
});

test("long request text truncates inside the toast body", () => {
  const text = "x".repeat(300);
  const copy = attentionToastCopy("Mindlash Studio", event({ text }));
  assert.ok(copy.body.startsWith("095-attention · coder\n"));
  assert.ok(copy.body.endsWith("…"));
  const bodyText = copy.body.slice(copy.body.indexOf("\n") + 1);
  assert.equal(bodyText.length, 241);
});
