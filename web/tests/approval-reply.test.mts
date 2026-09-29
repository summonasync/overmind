import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  APPROVAL_REPLY,
  approvalChoice,
  isAwaitingApproval,
  isAwaitingResponse,
} from "../src/features/notifications/lib/approval-reply.ts";
import type { NotificationItem } from "../src/features/notifications/types.ts";

const item = (overrides: Partial<NotificationItem>): NotificationItem => ({
  id: "n1",
  kind: "approval",
  text: "approve the rollout?",
  agent: "coder",
  createdAt: "2026-09-28T12:00:00Z",
  ...overrides,
});

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

test("unread unreplied approval awaits a response", () => {
  assert.equal(isAwaitingResponse(item({})), true);
});

test("unread unreplied question awaits a response", () => {
  assert.equal(isAwaitingResponse(item({ kind: "question" })), true);
});

test("replied approval and question stop awaiting", () => {
  assert.equal(isAwaitingResponse(item({ replyText: "approved", repliedAt: "2026-09-28T13:00:00Z" })), false);
  assert.equal(isAwaitingResponse(item({ kind: "question", replyText: "42" })), false);
});

test("read but unreplied items drop out of the awaiting set", () => {
  assert.equal(isAwaitingResponse(item({ readAt: "2026-09-28T13:00:00Z" })), false);
  assert.equal(isAwaitingResponse(item({ kind: "question", readAt: "2026-09-28T13:00:00Z" })), false);
});

test("info kind never awaits a response", () => {
  assert.equal(isAwaitingResponse(item({ kind: "info" })), false);
});

test("bell filter stays approval-only and read-tolerant", () => {
  assert.equal(isAwaitingApproval(item({ kind: "question" })), false);
  assert.equal(isAwaitingApproval(item({ readAt: "2026-09-28T13:00:00Z" })), true);
});
