import { strict as assert } from "node:assert";
import { test } from "node:test";
import { notificationProjectId } from "../src/features/notifications/lib/notification-project-id.ts";
import type { NotificationItem } from "../src/features/notifications/types.ts";

const item = (overrides: Partial<NotificationItem>): NotificationItem => ({
  id: "n1",
  kind: "info",
  text: "ping",
  agent: "coder",
  createdAt: "2026-10-04T12:00:00Z",
  ...overrides,
});

test("present projectId passes through to the badge resolver", () => {
  assert.equal(notificationProjectId(item({ projectId: "board-1" })), "board-1");
});

test("absent and null projectId collapse to the resolver empty marker", () => {
  assert.equal(notificationProjectId(item({})), "");
  assert.equal(notificationProjectId(item({ projectId: null })), "");
});
