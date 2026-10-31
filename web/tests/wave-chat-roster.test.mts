import { strict as assert } from "node:assert";
import { test } from "node:test";
import { ALL_ROOM, roomLabel } from "../src/features/wave-chat/lib/room-order.ts";
import { agentsOf, ownerOf, presenceSeats } from "../src/features/wave-chat/lib/roster.ts";
import { roleBadgeToneOf, roleTextClassOf } from "../src/features/wave-chat/lib/role-color.ts";
import type { WaveParticipant } from "../src/features/wave-chat/types.ts";

const owner = (alias = "johan"): WaveParticipant => ({
  kind: "owner",
  lane: null,
  alias,
  vendor: null,
  claimState: null,
  role: "owner",
  lastSeenAt: null,
});

const agent = (
  alias: string,
  lane: string,
  vendor = "claude",
  role: WaveParticipant["role"] = "worker",
): WaveParticipant => ({
  kind: "agent",
  lane,
  alias,
  vendor,
  claimState: "alive",
  role,
  lastSeenAt: "2026-10-25T09:30:00Z",
});

const claimlessAgent = (
  alias: string,
  role: WaveParticipant["role"],
  vendor = "claude",
): WaveParticipant => ({
  kind: "agent",
  lane: null,
  alias,
  vendor,
  claimState: null,
  role,
  lastSeenAt: "2026-10-31T09:30:00Z",
});

test("the owner stays pinned first no matter how the payload orders rows", () => {
  const participants = [agent("guldan", "frontend"), owner("johan"), agent("thrall", "backend")];
  assert.deepEqual(ownerOf(participants), owner("johan"));
  assert.deepEqual(
    agentsOf(participants).map((entry) => entry.alias),
    ["guldan", "thrall"],
  );
});

test("an agent row carries its alias, vendor, lane and role", () => {
  const [row] = agentsOf([owner(), agent("guldan", "frontend", "claude", "marshal")]);
  assert.deepEqual(row, {
    kind: "agent",
    lane: "frontend",
    alias: "guldan",
    vendor: "claude",
    claimState: "alive",
    role: "marshal",
    lastSeenAt: "2026-10-25T09:30:00Z",
  });
});

test("rows of an unknown kind land neither in the owner slot nor the agent rows", () => {
  const ghost = { kind: "ghost", lane: null, alias: "?", vendor: null, claimState: null, lastSeenAt: null };
  const participants = [owner(), ghost as WaveParticipant];
  assert.deepEqual(ownerOf(participants), owner());
  assert.deepEqual(agentsOf(participants), []);
});

test("an owner-less payload still resolves to empty slots without throwing", () => {
  assert.equal(ownerOf([agent("guldan", "backend")]), null);
  assert.deepEqual(ownerOf([]), null);
  assert.deepEqual(agentsOf([]), []);
});

test("presence seats list agents first and the owner last, roles attached", () => {
  assert.deepEqual(presenceSeats([owner("johan")]), [{ alias: "johan", role: "owner" }]);
  assert.deepEqual(presenceSeats([owner(), agent("guldan", "backend")]), [
    { alias: "guldan", role: "worker" },
    { alias: "johan", role: "owner" },
  ]);
  assert.deepEqual(presenceSeats([]), []);
});

test("presence ignores duplicate agent rows on different lanes as separate seats", () => {
  const participants = [owner(), agent("guldan", "backend"), agent("guldan", "frontend")];
  assert.equal(presenceSeats(participants).length, 3);
});

test("a claim-less null-lane session lands in the agent rows with its role badge", () => {
  const marshal = claimlessAgent("van-darkholme", "marshal");
  const [row] = agentsOf([owner(), marshal]);
  assert.deepEqual(row, marshal);
  assert.equal(roleBadgeToneOf(row.role), "marshal");
  assert.deepEqual(presenceSeats([owner(), marshal]), [
    { alias: "van-darkholme", role: "marshal" },
    { alias: "johan", role: "owner" },
  ]);
});

test("holder rows keep their lane and claim state beside claim-less ones", () => {
  const holder = agent("guldan", "frontend", "claude", "marshal");
  const claimless = claimlessAgent("guldan", "worker", "kimi");
  const participants = [owner(), holder, claimless];
  assert.deepEqual(agentsOf(participants), [holder, claimless]);
  assert.equal(presenceSeats(participants).length, 3);
});

test("roles map onto their text and badge colors", () => {
  assert.equal(roleTextClassOf("owner"), "text-primary");
  assert.equal(roleTextClassOf("worker"), "text-agent");
  assert.equal(roleTextClassOf("marshal"), "text-marshal");
  assert.equal(roleBadgeToneOf("worker"), "agent");
  assert.equal(roleBadgeToneOf("marshal"), "marshal");
  assert.equal(roleBadgeToneOf("owner"), "primary");
});

test("the hall keeps its friendly label while lanes render as channels", () => {
  assert.equal(roomLabel(ALL_ROOM), "All lanes");
  assert.equal(roomLabel("backend"), "#backend");
});
