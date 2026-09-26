import "./tz.ts";
import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  formatBacklogAge,
  formatCalendarDate,
  formatNotificationTime,
  formatRelativeTime,
  parseInstant,
} from "../src/shared/lib/relative-time.ts";

const isoUtcNoZone = (date: Date): string => date.toISOString().replace("Z", "");

test("z-less utc timestamp ten seconds old renders as just now", () => {
  const timestamp = isoUtcNoZone(new Date(Date.now() - 10_000));
  assert.equal(formatNotificationTime(timestamp), "just now");
});

test("z-less utc timestamp one hour in the future renders as just now", () => {
  const timestamp = isoUtcNoZone(new Date(Date.now() + 3_600_000));
  assert.equal(formatNotificationTime(timestamp), "just now");
});

test("z-suffixed utc timestamp ten seconds old renders as just now", () => {
  const timestamp = new Date(Date.now() - 10_000).toISOString();
  assert.equal(formatRelativeTime(timestamp), "just now");
});

test("z-less utc timestamp three hours old lands in the hour bucket", () => {
  const timestamp = isoUtcNoZone(new Date(Date.now() - (3 * 3_600_000 + 10_000)));
  assert.equal(formatRelativeTime(timestamp), "3h ago");
});

test("parseInstant reads z-less timestamps as utc", () => {
  const timestamp = isoUtcNoZone(new Date("2026-09-26T12:00:00.500Z"));
  assert.equal(parseInstant(timestamp), Date.parse("2026-09-26T12:00:00.500Z"));
});

test("parseInstant keeps explicit offsets untouched", () => {
  assert.equal(parseInstant("2026-09-26T15:00:00+03:00"), Date.parse("2026-09-26T12:00:00Z"));
});

test("parseInstant rejects garbage", () => {
  assert.equal(parseInstant("not a date"), null);
});

test("backlog age keeps the minute bucket for z-less input", () => {
  const timestamp = isoUtcNoZone(new Date(Date.now() - 100_000));
  assert.equal(formatBacklogAge(timestamp), "2m");
});

test("parseInstant reads z-less fractional timestamps as utc", () => {
  const frozenAt = "2026-09-26T21:30:00.1234567";
  assert.equal(parseInstant(frozenAt), Date.parse(`${frozenAt}Z`));
});

test("z-less frozenAt with fractional seconds renders the utc calendar date", () => {
  assert.equal(
    formatCalendarDate("2026-09-26T21:30:00.1234567"),
    new Date(Date.UTC(2026, 8, 26, 21, 30, 0, 123)).toLocaleDateString(),
  );
});

test("z-less utc instant past the date boundary renders the shifted local date", () => {
  assert.equal(
    formatCalendarDate("2026-09-26T21:00:00"),
    new Date(Date.UTC(2026, 8, 26, 21, 0)).toLocaleDateString(),
  );
});
