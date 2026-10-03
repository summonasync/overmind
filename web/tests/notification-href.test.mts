import { strict as assert } from "node:assert";
import { test } from "node:test";
import { notificationHref } from "../src/features/notifications/lib/notification-href.ts";
import type { NotificationItem } from "../src/features/notifications/types.ts";

const item = (overrides: Partial<NotificationItem>): NotificationItem => ({
  id: "n1",
  kind: "info",
  text: "issue filed",
  agent: "coder",
  createdAt: "2026-10-03T12:00:00Z",
  ...overrides,
});

test("issues tab routes to the Issues Board detail", () => {
  assert.equal(
    notificationHref(item({ projectId: "board-1", featureSlug: "issue-9", tab: "issues" })),
    "/issues?project=board-1&issue=issue-9",
  );
});

test("issues tab without a project still carries the issue id", () => {
  assert.equal(
    notificationHref(item({ featureSlug: "issue-9", tab: "issues" })),
    "/issues?issue=issue-9",
  );
});

test("planning tabs keep the dashboard deep link", () => {
  assert.equal(
    notificationHref(item({ projectId: "p1", featureSlug: "028-x", tab: "spec" })),
    "/dashboard?project=p1&feature=028-x&tab=spec",
  );
});

test("items without a tab land on the dashboard with an empty feature param", () => {
  assert.equal(
    notificationHref(item({ projectId: "p1", featureSlug: "028-x" })),
    "/dashboard?project=p1&feature=028-x",
  );
});
