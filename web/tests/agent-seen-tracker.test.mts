import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  createSeenTracker,
  isReportableMessageId,
  latestReportableId,
  seenCandidate,
  type SeenFlushEntry,
} from "../src/features/agent-chat/lib/seen-tracker.ts";

const manualScheduler = () => {
  const pending: Array<() => void> = [];
  return {
    schedule(callback: () => void) {
      pending.push(callback);
      return () => {
        const index = pending.indexOf(callback);
        if (index >= 0) {
          pending.splice(index, 1);
        }
      };
    },
    runAll() {
      while (pending.length > 0) {
        pending.shift()?.();
      }
    },
    get size() {
      return pending.length;
    },
  };
};

const drain = () => Promise.resolve();

test("pending and empty message ids are never reportable", () => {
  assert.equal(isReportableMessageId("pending-user-2026"), false);
  assert.equal(isReportableMessageId("pending-assistant-0"), false);
  assert.equal(isReportableMessageId(""), false);
  assert.equal(isReportableMessageId("9d2b1f4e-0000-4000-8000-000000000001"), true);
});

test("candidate is the highest seen reportable message", () => {
  const ids = ["a", "pending-assistant-1", "b", "c"];
  assert.equal(seenCandidate(ids, new Set(["a", "c", "pending-assistant-1"]), null), "c");
  assert.equal(seenCandidate(ids, new Set(["a"]), null), "a");
  assert.equal(seenCandidate(ids, new Set(), null), null);
  assert.equal(seenCandidate(ids, new Set(["pending-assistant-1"]), null), null);
});

test("latest reportable id is the last non-pending entry", () => {
  assert.equal(latestReportableId(["a", "pending-assistant-1", "b"]), "b");
  assert.equal(latestReportableId(["a"]), "a");
  assert.equal(latestReportableId(["pending-user-1", "pending-assistant-2"]), null);
  assert.equal(latestReportableId([]), null);
});

test("candidate never regresses behind the sent watermark", () => {
  const ids = ["a", "b", "c"];
  assert.equal(seenCandidate(ids, new Set(["a", "b", "c"]), "b"), "c");
  assert.equal(seenCandidate(ids, new Set(["a", "b", "c"]), "c"), null);
  assert.equal(seenCandidate(ids, new Set(["a"]), "c"), null);
});

test("a sent id missing from the list releases the constraint", () => {
  const ids = ["x", "y"];
  assert.equal(seenCandidate(ids, new Set(["x", "y"]), "gone-from-list"), "y");
});

test("tracker batches candidates and marks them sent after a flush", async () => {
  const scheduler = manualScheduler();
  const tracker = createSeenTracker(scheduler);
  const flushed: SeenFlushEntry[][] = [];
  tracker.setFlusher(async (entries) => {
    flushed.push(entries);
  });

  tracker.markSeen("conv-1", "m1");
  tracker.markSeen("conv-1", "pending-assistant-2");
  tracker.observeCandidates("conv-1", ["m1", "pending-assistant-2"]);
  assert.equal(tracker.queuedSize(), 1);
  assert.equal(scheduler.size, 1);

  scheduler.runAll();
  await drain();
  assert.deepEqual(flushed, [[{ conversationId: "conv-1", lastReadMessageId: "m1" }]]);
  assert.equal(tracker.queuedSize(), 0);

  tracker.observeCandidates("conv-1", ["m1", "pending-assistant-2"]);
  assert.equal(tracker.queuedSize(), 0);
});

test("a newer seen message re-queues after the watermark advanced", async () => {
  const scheduler = manualScheduler();
  const tracker = createSeenTracker(scheduler);
  const flushed: SeenFlushEntry[][] = [];
  tracker.setFlusher(async (entries) => {
    flushed.push(entries);
  });

  tracker.markSeen("conv-1", "m1");
  tracker.markSeen("conv-1", "m2");
  tracker.observeCandidates("conv-1", ["m1", "m2"]);
  scheduler.runAll();
  await drain();
  assert.deepEqual(flushed[0], [{ conversationId: "conv-1", lastReadMessageId: "m2" }]);

  tracker.markSeen("conv-1", "m3");
  tracker.observeCandidates("conv-1", ["m1", "m2", "m3"]);
  scheduler.runAll();
  await drain();
  assert.deepEqual(flushed[1], [{ conversationId: "conv-1", lastReadMessageId: "m3" }]);
});

test("failed flushes retry with a cap", async () => {
  const scheduler = manualScheduler();
  const tracker = createSeenTracker(scheduler);
  let fail = true;
  let calls = 0;
  tracker.setFlusher(async () => {
    calls += 1;
    if (fail) {
      throw new Error("backend down");
    }
  });

  const pump = async (times: number) => {
    for (let round = 0; round < times; round += 1) {
      scheduler.runAll();
      await drain();
    }
  };

  tracker.markSeen("conv-1", "m1");
  tracker.observeCandidates("conv-1", ["m1"]);

  await pump(4);
  assert.equal(calls, 4);

  await pump(2);
  assert.equal(calls, 4);

  fail = false;
  tracker.markSeen("conv-1", "m2");
  tracker.observeCandidates("conv-1", ["m1", "m2"]);
  await pump(1);
  assert.equal(calls, 5);
  assert.equal(tracker.queuedSize(), 0);
});

test("flushNow drains the queue without waiting for the debounce", () => {
  const scheduler = manualScheduler();
  const tracker = createSeenTracker(scheduler);
  const flushed: SeenFlushEntry[][] = [];
  tracker.setFlusher(async (entries) => {
    flushed.push(entries);
  });

  tracker.markSeen("conv-2", "n1");
  tracker.observeCandidates("conv-2", ["n1"]);
  tracker.flushNow();
  assert.deepEqual(flushed, [[{ conversationId: "conv-2", lastReadMessageId: "n1" }]]);
  assert.equal(scheduler.size, 0);
});
