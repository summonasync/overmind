const PENDING_ID_PREFIX = "pending-";

export const SEEN_FLUSH_DELAY_MS = 1_000;
export const SEEN_FLUSH_MAX_ATTEMPTS = 3;

export interface SeenFlushEntry {
  conversationId: string;
  lastReadMessageId: string;
}

export type SeenFlusher = (entries: SeenFlushEntry[]) => Promise<void>;

export interface SeenScheduler {
  schedule(callback: () => void, delayMs: number): () => void;
}

export const timeoutScheduler: SeenScheduler = {
  schedule(callback, delayMs) {
    const timer = setTimeout(callback, delayMs);
    return () => clearTimeout(timer);
  },
};

export function isReportableMessageId(messageId: string): boolean {
  return messageId.length > 0 && !messageId.startsWith(PENDING_ID_PREFIX);
}

export function seenCandidate(
  orderedIds: readonly string[],
  seenIds: ReadonlySet<string>,
  sentId: string | null,
): string | null {
  const sentIndex = sentId === null ? -1 : orderedIds.indexOf(sentId);
  let candidate: string | null = null;
  for (let index = 0; index < orderedIds.length; index += 1) {
    const messageId = orderedIds[index];
    if (index > sentIndex && seenIds.has(messageId) && isReportableMessageId(messageId)) {
      candidate = messageId;
    }
  }
  return candidate;
}

export function createSeenTracker(scheduler: SeenScheduler = timeoutScheduler) {
  const seen = new Map<string, Set<string>>();
  const sent = new Map<string, string>();
  const queued = new Map<string, string>();
  const attempts = new Map<string, number>();
  let flusher: SeenFlusher | null = null;
  let cancelScheduled: (() => void) | null = null;

  function enqueue(conversationId: string, messageId: string): void {
    queued.set(conversationId, messageId);
    scheduleFlush();
  }

  function scheduleFlush(): void {
    if (cancelScheduled !== null) {
      return;
    }
    cancelScheduled = scheduler.schedule(flush, SEEN_FLUSH_DELAY_MS);
  }

  function flush(): void {
    cancelScheduled = null;
    if (queued.size === 0 || flusher === null) {
      return;
    }
    const entries = [...queued].map(([conversationId, lastReadMessageId]) => ({
      conversationId,
      lastReadMessageId,
    }));
    queued.clear();
    void flusher(entries).then(
      () => {
        for (const entry of entries) {
          sent.set(entry.conversationId, entry.lastReadMessageId);
          attempts.delete(entry.conversationId);
        }
      },
      () => {
        for (const entry of entries) {
          const failedAttempts = (attempts.get(entry.conversationId) ?? 0) + 1;
          attempts.set(entry.conversationId, failedAttempts);
          if (failedAttempts > SEEN_FLUSH_MAX_ATTEMPTS) {
            attempts.delete(entry.conversationId);
            continue;
          }
          enqueue(entry.conversationId, entry.lastReadMessageId);
        }
      },
    );
  }

  return {
    setFlusher(next: SeenFlusher): void {
      flusher = next;
    },
    markSeen(conversationId: string, messageId: string): void {
      if (!isReportableMessageId(messageId)) {
        return;
      }
      let ids = seen.get(conversationId);
      if (ids === undefined) {
        ids = new Set();
        seen.set(conversationId, ids);
      }
      ids.add(messageId);
    },
    observeCandidates(conversationId: string, orderedIds: readonly string[]): void {
      const candidate = seenCandidate(
        orderedIds,
        seen.get(conversationId) ?? new Set<string>(),
        sent.get(conversationId) ?? null,
      );
      if (candidate !== null && queued.get(conversationId) !== candidate) {
        enqueue(conversationId, candidate);
      }
    },
    flushNow(): void {
      if (cancelScheduled !== null) {
        cancelScheduled();
      }
      flush();
    },
    queuedSize(): number {
      return queued.size;
    },
  };
}

export const seenTracker = createSeenTracker();
