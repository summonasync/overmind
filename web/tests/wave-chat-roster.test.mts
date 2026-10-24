import { strict as assert } from "node:assert";
import { test } from "node:test";
import { ALL_ROOM, roomLabel } from "../src/features/wave-chat/lib/room-order.ts";
import { agentsOf, ownerOf, presenceLine } from "../src/features/wave-chat/lib/roster.ts";
import type { WaveParticipant } from "../src/features/wave-chat/types.ts";

const owner = (alias = "johan"): WaveParticipant => ({
  kind: "owner",
  lane: null,
  alias,
  vendor: null,
  claimState: null,
});

const agent = (alias: string, lane: string, vendor = "claude"): WaveParticipant => ({
  kind: "agent",
  lane,
  alias,
  vendor,
  claimState: "alive",
});

test("the owner stays pinned first no matter how the payload orders rows", () => {
  const participants = [agent("guldan", "frontend"), owner("johan"), agent("thrall", "backend")];
  assert.deepEqual(ownerOf(participants), owner("johan"));
  assert.deepEqual(
    agentsOf(participants).map((entry) => entry.alias),
    ["guldan", "thrall"],
  );
});

test("an agent row carries its alias, vendor and lane", () => {
  const [row] = agentsOf([owner(), agent("guldan", "frontend", "claude")]);
  assert.deepEqual(row, {
    kind: "agent",
    lane: "frontend",
    alias: "guldan",
    vendor: "claude",
    claimState: "alive",
  });
});

test("rows of an unknown kind land neither in the owner slot nor the agent rows", () => {
  const ghost = { kind: "ghost", lane: null, alias: "?", vendor: null, claimState: null };
  const participants = [owner(), ghost as WaveParticipant];
  assert.deepEqual(ownerOf(participants), owner());
  assert.deepEqual(agentsOf(participants), []);
});

test("an owner-less payload still resolves to empty slots without throwing", () => {
  assert.equal(ownerOf([agent("guldan", "backend")]), null);
  assert.deepEqual(ownerOf([]), null);
  assert.deepEqual(agentsOf([]), []);
});

test("presence counts agents plus the owner", () => {
  assert.equal(presenceLine([owner("johan")]), "Owner in the wave");
  assert.equal(presenceLine([owner(), agent("guldan", "backend")]), "1 agent + owner in the wave");
  assert.equal(
    presenceLine([owner(), agent("guldan", "backend"), agent("thrall", "frontend")]),
    "2 agents + owner in the wave",
  );
});

test("presence ignores duplicate agent rows on different lanes as separate seats", () => {
  const participants = [owner(), agent("guldan", "backend"), agent("guldan", "frontend")];
  assert.equal(presenceLine(participants), "2 agents + owner in the wave");
});

test("the hall keeps its friendly label while lanes render as channels", () => {
  assert.equal(roomLabel(ALL_ROOM), "All lanes");
  assert.equal(roomLabel("backend"), "#backend");
});
