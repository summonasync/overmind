import { strict as assert } from "node:assert";
import { test } from "node:test";
import { validateComposerText, WAVE_TEXT_MAX_LENGTH } from "../src/features/wave-chat/lib/composer.ts";
import { absorbMessages, roomStream } from "../src/features/wave-chat/lib/feed-store.ts";
import { instantOf } from "../src/features/wave-chat/lib/instant.ts";
import { mergeMessages } from "../src/features/wave-chat/lib/message-stream.ts";
import { ALL_ROOM, sortRooms } from "../src/features/wave-chat/lib/room-order.ts";
import { unreadCounts } from "../src/features/wave-chat/lib/unread-counts.ts";
import type { WaveMessage, WaveRoom } from "../src/features/wave-chat/types.ts";

const message = (id: string, room: string, createdAt: string, text = "signal"): WaveMessage => ({
  id,
  room,
  authorAlias: "dan",
  authorRole: "worker",
  text,
  mentions: [],
  createdAt,
});

const room = (name: string, lastActivityAt: string | null = null): WaveRoom => ({
  room: name,
  lastActivityAt,
});

test("the hall room pins first even when a lane is more recent", () => {
  const sorted = sortRooms([
    room("backend", "2026-10-24T12:00:00Z"),
    room("all", "2026-10-24T11:00:00Z"),
  ]);
  assert.deepEqual(sorted.map((entry) => entry.room), [ALL_ROOM, "backend"]);
});

test("lanes order by their latest activity, newest first", () => {
  const sorted = sortRooms([
    room("backend", "2026-10-24T10:00:00Z"),
    room("frontend", "2026-10-24T11:00:00Z"),
    room("all", null),
  ]);
  assert.deepEqual(sorted.map((entry) => entry.room), ["all", "frontend", "backend"]);
});

test("rooms without any activity sink below active ones and tie on the name", () => {
  const sorted = sortRooms([
    room("zeta"),
    room("alpha", "2026-10-24T09:00:00Z"),
    room("beta"),
    room("all"),
  ]);
  assert.deepEqual(sorted.map((entry) => entry.room), ["all", "alpha", "beta", "zeta"]);
});

test("equal activity stamps fall back to the room name", () => {
  const sorted = sortRooms([
    room("gamma", "2026-10-24T09:00:00Z"),
    room("beta", "2026-10-24T09:00:00Z"),
  ]);
  assert.deepEqual(sorted.map((entry) => entry.room), ["beta", "gamma"]);
});

test("sorting never mutates the caller's array", () => {
  const original = [room("backend"), room("all")];
  sortRooms(original);
  assert.deepEqual(original.map((entry) => entry.room), ["backend", "all"]);
});

test("a zoneless stamp still parses as UTC", () => {
  assert.notEqual(instantOf("2026-10-24T09:00:00"), 0);
  assert.equal(instantOf("2026-10-24T09:00:00"), instantOf("2026-10-24T09:00:00Z"));
  assert.equal(instantOf(null), 0);
  assert.equal(instantOf("not a date"), 0);
});

test("a room counts the messages newer than its last seen stamp", () => {
  const streams = {
    backend: [message("1", "backend", "2026-10-24T10:00:00Z"), message("2", "backend", "2026-10-24T10:01:00Z")],
    all: [message("3", "all", "2026-10-24T10:02:00Z")],
  };
  const counts = unreadCounts(streams, { backend: "2026-10-24T10:00:00Z" });
  assert.equal(counts.backend, 1);
  assert.equal(counts.all, 1);
});

test("a room never opened counts its whole stream", () => {
  const streams = { backend: [message("1", "backend", "2026-10-24T10:00:00Z")] };
  const counts = unreadCounts(streams, {});
  assert.equal(counts.backend, 1);
});

test("a stamp newer than the stream never drives a negative badge", () => {
  const streams = { backend: [message("1", "backend", "2026-10-24T10:00:00Z")] };
  const counts = unreadCounts(streams, { backend: "2026-10-24T23:00:00Z" });
  assert.equal(counts.backend, 0);
});

test("opening a room stamps its newest message, which clears the badge", () => {
  const streams = {
    backend: [message("1", "backend", "2026-10-24T10:00:00Z"), message("2", "backend", "2026-10-24T10:01:00Z")],
  };
  const newest = streams.backend[streams.backend.length - 1];
  const opened = unreadCounts(streams, { backend: newest?.createdAt ?? "" });
  assert.equal(opened.backend, 0);
});

test("messages merge across polls without duplicates and stay chronological", () => {
  const first = [message("2", "all", "2026-10-24T10:01:00Z"), message("1", "all", "2026-10-24T10:00:00Z")];
  const second = [message("3", "all", "2026-10-24T09:59:00Z"), message("2", "all", "2026-10-24T10:01:00Z")];
  const merged = mergeMessages(mergeMessages([], first), second);
  assert.deepEqual(merged.map((entry) => entry.id), ["3", "1", "2"]);
});

test("a later poll refreshes an already known message", () => {
  const merged = mergeMessages([message("1", "all", "2026-10-24T10:00:00Z", "old")], [
    message("1", "all", "2026-10-24T10:00:00Z", "new"),
  ]);
  assert.equal(merged.length, 1);
  assert.equal(merged[0]?.text, "new");
});

test("a mention delivered by a foreign room lands in its own room stream", () => {
  const projectId = "panel-logic-mention";
  absorbMessages(projectId, [
    message("1", "backend", "2026-10-24T10:00:00Z", "cross-room ping @owner"),
    message("2", "all", "2026-10-24T10:01:00Z"),
  ]);
  assert.deepEqual(roomStream(projectId, "backend").map((entry) => entry.id), ["1"]);
  assert.deepEqual(roomStream(projectId, "all").map((entry) => entry.id), ["2"]);
  assert.deepEqual(roomStream(projectId, "missing"), []);
});

test("reabsorbing the same poll does not duplicate stream rows", () => {
  const projectId = "panel-logic-dedupe";
  const batch = [message("1", "all", "2026-10-24T10:00:00Z")];
  absorbMessages(projectId, batch);
  absorbMessages(projectId, batch);
  assert.equal(roomStream(projectId, "all").length, 1);
});

test("blank and whitespace-only drafts are refused", () => {
  assert.equal(validateComposerText("").valid, false);
  const whitespace = validateComposerText("   \n  ");
  assert.equal(whitespace.valid, false);
  assert.ok(whitespace.valid === false && whitespace.reason.length > 0);
});

test("a valid draft is trimmed, not rejected", () => {
  const validation = validateComposerText("  coordinate the shared file  ");
  assert.deepEqual(validation, { valid: true, text: "coordinate the shared file" });
});

test("the cap matches the server limit: 500 passes, 501 refuses", () => {
  assert.equal(validateComposerText("x".repeat(WAVE_TEXT_MAX_LENGTH)).valid, true);
  const over = validateComposerText("x".repeat(WAVE_TEXT_MAX_LENGTH + 1));
  assert.equal(over.valid, false);
  assert.ok(over.valid === false && over.reason.includes(String(WAVE_TEXT_MAX_LENGTH)));
});
