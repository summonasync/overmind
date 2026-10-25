import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  countUnread,
  historyWindowSize,
  HISTORY_PAGE_LIMIT,
  HISTORY_WINDOW_MAX,
  HISTORY_WINDOW_MIN,
  sliceByUnread,
  unreadExceedsWindow,
} from "../src/features/wave-chat/lib/history-window.ts";
import { loadLastSeen, loadSeenMap, markSeen } from "../src/features/wave-chat/lib/seen.ts";
import type { WaveMessage } from "../src/features/wave-chat/types.ts";

const message = (id: string, createdAt: string, text = "signal"): WaveMessage => ({
  id,
  room: "backend",
  authorAlias: "dan",
  authorRole: "worker",
  text,
  mentions: [],
  createdAt,
});

const page = (ids: string[], startMinute: number): WaveMessage[] =>
  ids.map((id, index) => message(id, stamp(startMinute + index)));

const stamp = (minute: number): string =>
  `2026-10-24T10:${String(minute).padStart(2, "0")}:00Z`;

test("the tail window never drops below twenty messages", () => {
  assert.equal(historyWindowSize(0), HISTORY_WINDOW_MIN);
  assert.equal(historyWindowSize(15), HISTORY_WINDOW_MIN);
});

test("the tail window covers the unread block plus five read context rows", () => {
  assert.equal(historyWindowSize(16), 21);
  assert.equal(historyWindowSize(30), 35);
});

test("the tail window caps at the server maximum of 500", () => {
  assert.equal(HISTORY_WINDOW_MAX, 500);
  assert.equal(HISTORY_PAGE_LIMIT, 20);
  assert.equal(historyWindowSize(499), 500);
  assert.equal(historyWindowSize(4000), 500);
});

test("extension triggers only while a full page holds unread at its oldest edge", () => {
  const fullUnread = page(["1", "2"], 0);
  assert.equal(unreadExceedsWindow(fullUnread, 2, Date.parse("2026-10-24T09:00:00Z")), true);
  const fullRead = page(["1", "2"], 0);
  assert.equal(unreadExceedsWindow(fullRead, 2, Date.parse("2026-10-24T10:00:00Z")), false);
  const shortPage = page(["1"], 0);
  assert.equal(unreadExceedsWindow(shortPage, 2, 0), false);
  assert.equal(unreadExceedsWindow([], 20, 0), false);
});

test("countUnread counts messages past the seen boundary", () => {
  const stream = page(["1", "2", "3"], 0);
  assert.equal(countUnread(stream, Date.parse("2026-10-24T10:01:00Z")), 1);
  assert.equal(countUnread(stream, 0), 3);
  assert.equal(countUnread(stream, Date.parse("2026-10-24T23:00:00Z")), 0);
});

test("slicing splits the stream into read, unread and live regions", () => {
  const stream = page(["1", "2", "3", "4", "5"], 0);
  const slices = sliceByUnread(stream, {
    from: "2026-10-24T10:00:00Z",
    to: "2026-10-24T10:03:00Z",
  });
  assert.deepEqual(slices.read.map((entry) => entry.id), ["1"]);
  assert.deepEqual(slices.unread.map((entry) => entry.id), ["2", "3", "4"]);
  assert.deepEqual(slices.live.map((entry) => entry.id), ["5"]);
});

test("an unseen room puts every fetched message into the unread block", () => {
  const stream = page(["1", "2"], 0);
  const slices = sliceByUnread(stream, { from: null, to: "2026-10-24T10:02:00Z" });
  assert.equal(slices.read.length, 0);
  assert.deepEqual(slices.unread.map((entry) => entry.id), ["1", "2"]);
});

test("an unset open anchor keeps late arrivals inside the unread block", () => {
  const stream = page(["1", "2"], 0);
  const slices = sliceByUnread(stream, { from: "2026-10-24T10:00:00Z", to: null });
  assert.deepEqual(slices.unread.map((entry) => entry.id), ["2"]);
  assert.equal(slices.live.length, 0);
});

interface FakeStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

function installFakeSession(): Map<string, string> {
  const store = new Map<string, string>();
  const storage: FakeStorage = {
    getItem: (key) => (store.has(key) ? (store.get(key) as string) : null),
    setItem: (key, value) => {
      store.set(key, value);
    },
  };
  (globalThis as { window?: unknown }).window = { sessionStorage: storage };
  return store;
}

const fakeStore = installFakeSession();

test("markSeen persists a room stamp that survives a reload", () => {
  fakeStore.clear();
  markSeen("proj-1", "backend", "2026-10-24T10:05:00Z");
  assert.equal(loadLastSeen("proj-1", "backend"), "2026-10-24T10:05:00Z");
  assert.equal(loadSeenMap("proj-1")["backend"], "2026-10-24T10:05:00Z");
});

test("seen stamps are scoped per project and room", () => {
  fakeStore.clear();
  markSeen("proj-1", "backend", "2026-10-24T10:05:00Z");
  markSeen("proj-1", "frontend", "2026-10-24T10:06:00Z");
  markSeen("proj-2", "backend", "2026-10-24T10:07:00Z");
  assert.equal(loadLastSeen("proj-1", "backend"), "2026-10-24T10:05:00Z");
  assert.equal(loadLastSeen("proj-1", "frontend"), "2026-10-24T10:06:00Z");
  assert.equal(loadLastSeen("proj-2", "backend"), "2026-10-24T10:07:00Z");
  assert.equal(loadLastSeen("proj-2", "frontend"), null);
});

test("rewriting a stamp advances the stored value", () => {
  fakeStore.clear();
  markSeen("proj-1", "backend", "2026-10-24T10:05:00Z");
  markSeen("proj-1", "backend", "2026-10-24T10:09:00Z");
  assert.equal(loadLastSeen("proj-1", "backend"), "2026-10-24T10:09:00Z");
});

test("corrupted storage falls back to an empty seen map", () => {
  fakeStore.clear();
  fakeStore.set("overmind.wave-chat.seen.proj-1", "{not json");
  assert.deepEqual(loadSeenMap("proj-1"), {});
  assert.equal(loadLastSeen("proj-1", "backend"), null);
});

test("a seen map with non-string values is refused", () => {
  fakeStore.clear();
  fakeStore.set("overmind.wave-chat.seen.proj-1", JSON.stringify({ backend: 7 }));
  assert.deepEqual(loadSeenMap("proj-1"), {});
});

test("without a browser session the helpers degrade to no-ops", () => {
  delete (globalThis as { window?: unknown }).window;
  assert.equal(loadLastSeen("proj-1", "backend"), null);
  markSeen("proj-1", "backend", "2026-10-24T10:05:00Z");
  assert.equal(loadLastSeen("proj-1", "backend"), null);
  installFakeSession();
});
