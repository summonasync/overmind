import { strict as assert } from "node:assert";
import { test } from "node:test";
import { isAwaitingNotification } from "../src/features/notifications/lib/awaiting.ts";
import type { NotificationItem } from "../src/features/notifications/types.ts";

const item = (overrides: Partial<NotificationItem>): NotificationItem => ({
  id: "n1",
  kind: "question",
  text: "which storage backend ships?",
  agent: "coder",
  createdAt: "2026-09-29T12:00:00Z",
  ...overrides,
});

test("unanswered approval and question rows stay in awaiting", () => {
  assert.equal(isAwaitingNotification(item({})), true);
  assert.equal(isAwaitingNotification(item({ kind: "approval" })), true);
});

test("answered rows drop out of awaiting", () => {
  assert.equal(isAwaitingNotification(item({ replyText: "approved" })), false);
  assert.equal(isAwaitingNotification(item({ replyText: "needs a migration first" })), false);
  assert.equal(isAwaitingNotification(item({ replyText: "" })), false);
});

test("read-but-unanswered rows stay in awaiting until a reply lands", () => {
  assert.equal(isAwaitingNotification(item({ readAt: "2026-09-29T13:00:00Z" })), true);
});

test("hidden and info rows never count as awaiting", () => {
  assert.equal(isAwaitingNotification(item({ hiddenAt: "2026-09-29T14:00:00Z" })), false);
  assert.equal(isAwaitingNotification(item({ kind: "info" })), false);
});
