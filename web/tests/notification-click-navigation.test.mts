import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import {
  ANCHOR_POLL_INTERVAL_MS,
  ANCHOR_POLL_TIMEOUT_MS,
  NOTIFICATION_HIGHLIGHT_MS,
  NOTIFICATION_PARAM,
  attentionHref,
  notificationAnchorSelector,
} from "../src/features/notifications/lib/notification-anchor.ts";

test("attention href targets the attention tab with the notification param", () => {
  assert.equal(attentionHref("abc-123"), "/attention?notification=abc-123");
  assert.equal(new URL(attentionHref("x"), "http://localhost").pathname, "/attention");
});

test("anchor selector is attribute based and quote safe", () => {
  assert.equal(notificationAnchorSelector("n1"), '[data-notification-id="n1"]');
  assert.equal(notificationAnchorSelector('a"b'), '[data-notification-id="a\\"b"]');
});

test("highlight is temporary and polling is bounded", () => {
  assert.ok(NOTIFICATION_HIGHLIGHT_MS > 0 && NOTIFICATION_HIGHLIGHT_MS <= 5000);
  assert.ok(ANCHOR_POLL_TIMEOUT_MS >= ANCHOR_POLL_INTERVAL_MS);
  assert.equal(NOTIFICATION_PARAM, "notification");
});

const card = () =>
  readFileSync(
    new URL("../src/widgets/app-shell/ui/bell-notification-card.tsx", import.meta.url),
    "utf8",
  );

test("bell row click navigates to attention, never to the feature", () => {
  const source = card();
  assert.ok(source.includes(`href={attentionHref(item.id)}`));
  assert.ok(!source.includes(`href={notificationHref(item.featureSlug ?? "")}`));
  const anchorZone = source.slice(
    source.indexOf(`href={attentionHref(item.id)}`),
    source.indexOf("</a>"),
  );
  assert.ok(!anchorZone.includes("readAt"));
  assert.ok(anchorZone.includes("Open feature") === false);
});

const page = () =>
  readFileSync(
    new URL("../src/pages/attention-page/ui/attention-page.tsx", import.meta.url),
    "utf8",
  );

test("attention page anchors the highlighted row across sections", () => {
  const source = page();
  assert.ok(source.includes("useNotificationAnchor()"));
  assert.ok(source.includes(`<AwaitingSection highlightId={highlightId} />`));
  assert.ok(source.includes(`<NotificationCenter highlightId={highlightId} />`));
});

const hook = () =>
  readFileSync(
    new URL("../src/pages/attention-page/lib/use-notification-anchor.ts", import.meta.url),
    "utf8",
  );

test("missing anchor degrades to landing at the tab top without errors", () => {
  const source = hook();
  assert.ok(source.includes("if (anchor === null)"));
  assert.ok(source.includes("ANCHOR_POLL_TIMEOUT_MS"));
  assert.ok(!source.includes("throw"));
  assert.ok(!source.includes("Error("));
});

test("rows carry the anchor attribute and highlight ring", () => {
  const awaitingRow = readFileSync(
    new URL("../src/pages/attention-page/ui/awaiting-row.tsx", import.meta.url),
    "utf8",
  );
  const notificationRow = readFileSync(
    new URL("../src/widgets/app-shell/ui/notification-row.tsx", import.meta.url),
    "utf8",
  );
  for (const source of [awaitingRow, notificationRow]) {
    assert.ok(source.includes(`data-notification-id={item.id}`));
    assert.ok(source.includes(`highlighted && "ring-2 ring-primary/50"`));
  }
});

test("highlight works regardless of read state", () => {
  const notificationRow = readFileSync(
    new URL("../src/widgets/app-shell/ui/notification-row.tsx", import.meta.url),
    "utf8",
  );
  const anchorPage = page();
  assert.ok(anchorPage.includes(`highlightId={highlightId}`));
  assert.ok(!notificationRow.includes("highlighted && item.readAt"));
});
