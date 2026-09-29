import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  isHiddenNotification,
  repliedConversationsCount,
} from "../src/features/notifications/lib/conversation-archive.ts";
import type { NotificationItem } from "../src/features/notifications/types.ts";

const item = (overrides: Partial<NotificationItem>): NotificationItem => ({
  id: "n1",
  kind: "question",
  text: "which accent color ships?",
  agent: "coder",
  createdAt: "2026-09-29T12:00:00Z",
  ...overrides,
});

test("clear-conversations counts rows with a reply", () => {
  assert.equal(
    repliedConversationsCount([
      item({}),
      item({ replyText: "accent", repliedAt: "2026-09-29T13:00:00Z" }),
    ]),
    1,
  );
});

test("clear-conversations button stays disabled without replied rows", () => {
  assert.equal(repliedConversationsCount([]), 0);
  assert.equal(repliedConversationsCount([item({ replyText: null })]), 0);
  assert.equal(repliedConversationsCount([item({ replyText: "" })]), 1);
});

test("cleared archive rows carry hiddenAt", () => {
  assert.equal(isHiddenNotification(item({ hiddenAt: "2026-09-29T14:00:00Z" })), true);
});

test("rows without hiddenAt render in the default view undimmed", () => {
  assert.equal(isHiddenNotification(item({})), false);
  assert.equal(isHiddenNotification(item({ hiddenAt: null })), false);
});
