import { strict as assert } from "node:assert";
import { test } from "node:test";
import { unreadNotifications } from "../src/features/notifications/lib/bell-preview.ts";
import type { NotificationItem } from "../src/features/notifications/types.ts";

const item = (overrides: Partial<NotificationItem>): NotificationItem => ({
  id: "n1",
  kind: "question",
  text: "spec is waiting for validation",
  agent: "coder",
  createdAt: "2026-09-29T12:00:00Z",
  ...overrides,
});

test("bell preview shows only unread rows and keeps newest-first order", () => {
  const unread = unreadNotifications([
    item({ id: "a" }),
    item({ id: "b", readAt: "2026-09-29T13:00:00Z" }),
    item({ id: "c" }),
  ]);
  assert.deepEqual(
    unread.map((row) => row.id),
    ["a", "c"],
  );
});

test("bell preview empties out once everything is read", () => {
  assert.equal(
    unreadNotifications([item({ readAt: "2026-09-29T13:00:00Z" }), item({ readAt: null })]).length,
    1,
  );
});
