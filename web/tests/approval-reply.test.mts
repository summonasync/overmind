import { strict as assert } from "node:assert";
import { test } from "node:test";
import { APPROVAL_REPLY, approvalChoice } from "../src/features/notifications/lib/approval-reply.ts";

test("canonical replies are approved and rejected", () => {
  assert.equal(APPROVAL_REPLY.approve, "approved");
  assert.equal(APPROVAL_REPLY.reject, "rejected");
});

test("canonical approved reply resolves to the approve choice", () => {
  assert.equal(approvalChoice("approved"), "approve");
});

test("canonical rejected reply resolves to the reject choice", () => {
  assert.equal(approvalChoice("rejected"), "reject");
});

test("free-form reply text carries no approval choice", () => {
  assert.equal(approvalChoice("need more context on the rollout"), null);
});

test("absent reply carries no approval choice", () => {
  assert.equal(approvalChoice(null), null);
  assert.equal(approvalChoice(undefined), null);
});
