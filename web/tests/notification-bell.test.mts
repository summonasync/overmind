import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const bell = () =>
  readFileSync(
    new URL("../src/widgets/app-shell/ui/notification-bell.tsx", import.meta.url),
    "utf8",
  );

const card = () =>
  readFileSync(
    new URL("../src/widgets/app-shell/ui/bell-notification-card.tsx", import.meta.url),
    "utf8",
  );

test("popup rows carry a Mark read button wired to the shared mutation", () => {
  const source = card();
  assert.ok(source.includes(`onClick={() => onMarkRead(item.id)}`));
  assert.ok(source.includes("Mark read"));
  const bellSource = bell();
  assert.ok(bellSource.includes(`const { markRead } = useNotificationMutations();`));
  assert.ok(bellSource.includes(`onMarkRead={markRead.mutate}`));
});

test("auto-read arms only while the popup is open and reuses the mark-read mutation", () => {
  const source = bell();
  assert.ok(source.includes(`useBellAutoRead(open, unreadIds, markRead.mutate)`));
});

const hook = () =>
  readFileSync(
    new URL("../src/features/notifications/lib/use-bell-auto-read.ts", import.meta.url),
    "utf8",
  );

test("auto-read hook cancels pending timers when the popup closes", () => {
  const source = hook();
  assert.ok(source.includes("if (!open) {"));
  assert.ok(source.includes(".cancel()"));
  assert.ok(source.includes("createBellAutoRead"));
});

test("popup header shows the feature in the board-card format", () => {
  const source = card();
  assert.ok(source.includes(`FeatureNumberBadge slug={item.featureSlug} numbering={numbering}`));
  assert.ok(source.includes(`text-sm font-semibold text-foreground`));
  assert.ok(source.includes(`useProjectNumbering(projectId)`));
  assert.ok(source.includes(`useFeatureTitle(projectId, item.featureSlug ?? "")`));
});

test("Open feature is the only feature jump from the popup", () => {
  const source = card();
  assert.ok(source.includes("Open feature →"));
  assert.ok(source.includes(`href={notificationHref(item)}`));
  const bellSource = bell();
  assert.ok(!bellSource.includes(`notificationHref`));
});
