export const BELL_AUTO_READ_MS = 3000;

type TimeoutHandle = ReturnType<typeof setTimeout>;

export interface BellAutoReadScheduler {
  set(handler: () => void, timeoutMs: number): TimeoutHandle;
  clear(handle: TimeoutHandle): void;
}

const timeoutScheduler: BellAutoReadScheduler = {
  set: (handler, timeoutMs) => setTimeout(handler, timeoutMs),
  clear: (handle) => clearTimeout(handle),
};

export interface BellAutoRead {
  arm(id: string): void;
  cancel(): void;
}

export function createBellAutoRead(
  read: (id: string) => void,
  scheduler: BellAutoReadScheduler = timeoutScheduler,
): BellAutoRead {
  const pending = new Map<string, TimeoutHandle>();

  return {
    arm(id: string): void {
      if (pending.has(id)) {
        return;
      }
      const handle = scheduler.set(() => {
        pending.delete(id);
        read(id);
      }, BELL_AUTO_READ_MS);
      pending.set(id, handle);
    },
    cancel(): void {
      for (const handle of pending.values()) {
        scheduler.clear(handle);
      }
      pending.clear();
    },
  };
}
