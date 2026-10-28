import { strict as assert } from "node:assert";
import { test } from "node:test";
import {
  BELL_AUTO_READ_MS,
  createBellAutoRead,
  type BellAutoReadScheduler,
} from "../src/features/notifications/lib/bell-auto-read.ts";

interface FakeScheduler extends BellAutoReadScheduler {
  advance(ms: number): void;
  pendingCount(): number;
}

function fakeScheduler(): FakeScheduler {
  let now = 0;
  let nextHandle = 1;
  const pending = new Map<number, { at: number; handler: () => void }>();
  return {
    set(handler, timeoutMs) {
      const handle = nextHandle++;
      pending.set(handle, { at: now + timeoutMs, handler });
      return handle as unknown as ReturnType<typeof setTimeout>;
    },
    clear(handle) {
      pending.delete(handle as unknown as number);
    },
    advance(ms) {
      now += ms;
      const due = [...pending.entries()]
        .filter(([, entry]) => entry.at <= now)
        .sort((a, b) => a[1].at - b[1].at);
      for (const [handle, entry] of due) {
        pending.delete(handle);
        entry.handler();
      }
    },
    pendingCount: () => pending.size,
  };
}

test("auto-read threshold is the 3s constant", () => {
  assert.equal(BELL_AUTO_READ_MS, 3000);
});

test("notification held open past the threshold is marked read", () => {
  const scheduler = fakeScheduler();
  const reads: string[] = [];
  const autoRead = createBellAutoRead((id) => reads.push(id), scheduler);

  autoRead.arm("n1");
  scheduler.advance(2999);
  assert.deepEqual(reads, []);
  scheduler.advance(1);
  assert.deepEqual(reads, ["n1"]);
});

test("popup closed before the threshold cancels the timer", () => {
  const scheduler = fakeScheduler();
  const reads: string[] = [];
  const autoRead = createBellAutoRead((id) => reads.push(id), scheduler);

  autoRead.arm("n1");
  autoRead.arm("n2");
  scheduler.advance(1000);
  autoRead.cancel();
  scheduler.advance(5000);
  assert.deepEqual(reads, []);
  assert.equal(scheduler.pendingCount(), 0);
});

test("arming the same notification twice keeps a single timer", () => {
  const scheduler = fakeScheduler();
  const reads: string[] = [];
  const autoRead = createBellAutoRead((id) => reads.push(id), scheduler);

  autoRead.arm("n1");
  autoRead.arm("n1");
  assert.equal(scheduler.pendingCount(), 1);
  scheduler.advance(BELL_AUTO_READ_MS);
  assert.deepEqual(reads, ["n1"]);
});

test("notifications opened at different moments fire independently", () => {
  const scheduler = fakeScheduler();
  const reads: string[] = [];
  const autoRead = createBellAutoRead((id) => reads.push(id), scheduler);

  autoRead.arm("n1");
  scheduler.advance(2000);
  autoRead.arm("n2");
  scheduler.advance(1000);
  assert.deepEqual(reads, ["n1"]);
  scheduler.advance(2000);
  assert.deepEqual(reads, ["n1", "n2"]);
});

test("re-arming after a fired read schedules it again", () => {
  const scheduler = fakeScheduler();
  const reads: string[] = [];
  const autoRead = createBellAutoRead((id) => reads.push(id), scheduler);

  autoRead.arm("n1");
  scheduler.advance(BELL_AUTO_READ_MS);
  autoRead.arm("n1");
  assert.equal(scheduler.pendingCount(), 1);
  scheduler.advance(BELL_AUTO_READ_MS);
  assert.deepEqual(reads, ["n1", "n1"]);
});
