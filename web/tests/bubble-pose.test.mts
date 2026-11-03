import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  isSmugActive,
  resolveBubblePose,
  SMUG_HOLD_MS,
} from "../src/widgets/agent-chat-bubble/lib/bubble-pose.ts";
import type { AgentChatActivity } from "../src/features/agent-chat/types.ts";

const NOW = 1_800_000_000_000;

const idle = (): AgentChatActivity["lastTurn"] => ({ kind: "idle" });
const completed = (delegated: boolean, ageMs = 0): AgentChatActivity["lastTurn"] => ({
  kind: "completed",
  delegated,
  completedAtMs: NOW - ageMs,
});
const failed = (ageMs = 0): AgentChatActivity["lastTurn"] => ({
  kind: "failed",
  failedAtMs: NOW - ageMs,
});

const poseOf = (
  open: boolean,
  unreadCount: number,
  activity: AgentChatActivity,
  nowMs = NOW,
) => resolveBubblePose({ open, unreadCount, activity, nowMs });

test("closed bubble with no unread sleeps", () => {
  assert.equal(poseOf(false, 0, { streaming: false, lastTurn: idle() }), "sleeping");
});

test("closed bubble with unread yells", () => {
  assert.equal(poseOf(false, 3, { streaming: false, lastTurn: idle() }), "yelling");
});

test("open panel with an active stream is surprised regardless of unread", () => {
  assert.equal(poseOf(true, 5, { streaming: true, lastTurn: idle() }), "surprised");
});

test("closed bubble keeps yelling semantics while streaming", () => {
  assert.equal(poseOf(false, 2, { streaming: true, lastTurn: idle() }), "yelling");
  assert.equal(poseOf(false, 0, { streaming: true, lastTurn: idle() }), "sleeping");
});

test("failed turn stays confused until the next turn", () => {
  const activity = { streaming: false, lastTurn: failed(60_000) };
  assert.equal(poseOf(false, 0, activity), "confused");
  assert.equal(poseOf(true, 0, activity), "confused");
});

test("delegated completion is smug only inside the hold window", () => {
  const fresh = { streaming: false, lastTurn: completed(true) };
  assert.equal(poseOf(false, 0, fresh), "smug");
  assert.equal(poseOf(true, 0, fresh), "smug");

  const aged = { streaming: false, lastTurn: completed(true, SMUG_HOLD_MS + 1) };
  assert.equal(poseOf(false, 0, aged), "sleeping");
  assert.equal(poseOf(false, 1, aged), "yelling");
});

test("unread beats a fresh smug on a closed bubble", () => {
  const fresh = { streaming: false, lastTurn: completed(true) };
  assert.equal(poseOf(false, 1, fresh), "yelling");
  assert.equal(poseOf(true, 1, fresh), "smug");
});

test("plain completion never turns smug", () => {
  const activity = { streaming: false, lastTurn: completed(false) };
  assert.equal(poseOf(false, 0, activity), "sleeping");
  assert.equal(isSmugActive(activity, NOW), false);
});

test("smug expiry lands exactly at the hold boundary", () => {
  const activity = { streaming: false, lastTurn: completed(true) };
  assert.equal(isSmugActive(activity, NOW + SMUG_HOLD_MS - 1), true);
  assert.equal(isSmugActive(activity, NOW + SMUG_HOLD_MS), false);
});
