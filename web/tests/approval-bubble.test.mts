import { strict as assert } from "node:assert";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import {
  APPROVAL_NAG_TTL_MS,
  bubbleCandidates,
  bubbleMoreCount,
  bubbleSurface,
  dismissBubble,
  expandBubble,
  initialBubbleDiscipline,
} from "../src/widgets/app-shell/lib/approval-bubble-state.ts";

const NOW = 1_800_000_000_000;
const MINUTE_MS = 60_000;

const candidate = (id: string, ageMs: number) => ({ id, createdAtMs: NOW - ageMs });

test("fifo pick orders unresolved approvals oldest first", () => {
  const candidates = bubbleCandidates(
    [candidate("mid", 1000), candidate("oldest", 5000), candidate("newest", 500)],
    NOW,
  );
  assert.deepEqual(candidates.map((entry) => entry.id), ["oldest", "mid", "newest"]);
});

test("approvals older than the ttl gate stay out of the bubble", () => {
  const fresh = candidate("fresh", APPROVAL_NAG_TTL_MS - MINUTE_MS);
  const stale = candidate("stale", APPROVAL_NAG_TTL_MS + MINUTE_MS);
  assert.deepEqual(bubbleCandidates([stale, fresh], NOW).map((entry) => entry.id), ["fresh"]);
});

test("approval exactly at the ttl boundary ages out", () => {
  assert.deepEqual(bubbleCandidates([candidate("edge", APPROVAL_NAG_TTL_MS)], NOW), []);
});

test("badge count is the eligible approvals beyond the shown one", () => {
  const candidates = bubbleCandidates(
    [candidate("a", 3000), candidate("b", 2000), candidate("c", 1000)],
    NOW,
  );
  assert.equal(bubbleMoreCount(candidates), 2);
  assert.equal(bubbleMoreCount([candidates[0]]), 0);
  assert.equal(bubbleMoreCount([]), 0);
});

test("quiet mode pops immediately and hides on dismiss", () => {
  const only = candidate("only", MINUTE_MS);
  assert.equal(bubbleSurface(initialBubbleDiscipline, "quiet", [only], NOW), "bubble");
  const dismissed = dismissBubble([only.id], NOW, 10);
  assert.equal(bubbleSurface(dismissed, "quiet", [only], NOW), "hidden");
});

test("quiet dismissal survives queue advance and returns only for a new approval", () => {
  const oldest = candidate("oldest", 5000);
  const queued = candidate("queued", 1000);
  const dismissed = dismissBubble([oldest.id, queued.id], NOW, 10);
  assert.equal(bubbleSurface(dismissed, "quiet", [queued], NOW), "hidden");
  assert.equal(bubbleSurface(dismissed, "quiet", [queued, candidate("new", 10)], NOW), "bubble");
});

test("nag mode re-pops on the configured interval and re-arms on every dismiss", () => {
  const only = candidate("only", MINUTE_MS);
  const intervalMinutes = 10;
  let discipline = dismissBubble([only.id], NOW, intervalMinutes);
  assert.equal(bubbleSurface(discipline, "nag", [only], NOW + 10 * MINUTE_MS - 1), "hidden");
  assert.equal(bubbleSurface(discipline, "nag", [only], NOW + 10 * MINUTE_MS), "bubble");
  discipline = dismissBubble([only.id], NOW + 10 * MINUTE_MS, intervalMinutes);
  assert.equal(bubbleSurface(discipline, "nag", [only], NOW + 20 * MINUTE_MS - 1), "hidden");
  assert.equal(bubbleSurface(discipline, "nag", [only], NOW + 20 * MINUTE_MS), "bubble");
});

test("nag dismissal ignores fresh arrivals until the interval elapses", () => {
  const dismissed = dismissBubble(["old"], NOW, 10);
  assert.equal(bubbleSurface(dismissed, "nag", [candidate("new", 10)], NOW + MINUTE_MS), "hidden");
});

test("mini mode collapses to the corner icon and re-expands on click", () => {
  const only = candidate("only", MINUTE_MS);
  const dismissed = dismissBubble([only.id], NOW, 10);
  assert.equal(bubbleSurface(dismissed, "mini", [only], NOW), "icon");
  assert.equal(bubbleSurface(expandBubble(dismissed), "mini", [only], NOW), "bubble");
});

test("no eligible approval hides the surface in every mode", () => {
  for (const mode of ["quiet", "nag", "mini"] as const) {
    assert.equal(bubbleSurface(initialBubbleDiscipline, mode, [], NOW), "hidden");
    assert.equal(bubbleSurface(dismissBubble(["any"], NOW, 10), mode, [], NOW), "hidden");
  }
});

test("ttl aging re-admits nothing once the shown approval leaves the gate", () => {
  const dismissed = dismissBubble(["gone"], NOW, 10);
  assert.equal(bubbleSurface(dismissed, "quiet", [candidate("gone", APPROVAL_NAG_TTL_MS + 1)], NOW), "hidden");
});

test("bubble renders the sliced text without a line clamp", () => {
  const source = readFileSync(
    new URL("../src/widgets/app-shell/ui/approval-bubble.tsx", import.meta.url),
    "utf8",
  );
  assert.ok(!source.includes("line-clamp-4"));
  assert.ok(source.includes("line-clamp-none"));
  assert.ok(source.includes("bubbleText(item.text)"));
  assert.ok(source.includes("sm:w-80"));
});
