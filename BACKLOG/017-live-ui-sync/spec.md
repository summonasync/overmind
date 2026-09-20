---
feature: 017
slug: live-ui-sync
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Spec 017 — Live UI sync: board/entries refresh on MCP-side changes

<decisions>
- MVP is React Query refetchInterval = 15s on the LIVE board-relevant queries: planning board list, feature detail (workspace incl. artifact bodies — the dogfood pain was spec/plan/tasks tabs going stale), entries list, drift panel. Tags query excluded (near-static; manager refetches on open anyway). Current-task endpoint excluded (drives STOP semantics; stale STOP text is safer than flip-flop) (decision delegated by the user 2026-09-20).
- refetchIntervalInBackground stays false (React Query default): a hidden tab stops polling — the pain being solved is "UI open and watched", not background sync.
- One shared constant POLL_INTERVAL_MS = 15_000 in a lib module (features consume it; single knob to tune later, obvious seam for the SSE swap in a future feature).
- No new endpoints, no server changes, no optimistic-concurrency — the 022/012 optimistic mutations keep working; polling is additive background refetch (React Query dedupes with active invalidations).
- Editors are safe by construction: modals initialize from the loaded snapshot (initial-data pattern), so a background refetch of the feature detail cannot clobber an open editor's local state.
- SSE/server-push is explicitly a LATER feature (the feature body's own "дальше MVP" clause) — this feature is the polling layer only.
</decisions>

<open-questions>
- none (user delegated autonomous decisions 2026-09-20)
</open-questions>

<requirements>
Web only:
- features/planning/queries.ts: board + feature queryOptions gain refetchInterval from the shared constant; features/entries queries (list) likewise; drift panel's query (wherever it lives in the planning slice) likewise.
- The constant lives in one lib module consumed by all three (e.g. features/planning/lib/poll-interval.ts — or shared/lib if entries importing from planning would cross FSD: pick the FSD-clean home, possibly shared/lib with a comment-free name like LIVE_SYNC_INTERVAL_MS).
- No changes to mutations, invalidations, or editors.
- Verify: npm run lint && npx tsc --noEmit && npm run build.
</requirements>

<acceptance-ideas>
- With the UI open, an MCP-side change (artifact status/body, feature status, board composition) appears within ~15s without any manual refresh.
- A backgrounded tab does not poll (devtools network idle when hidden).
- Editors keep their in-progress content while background refetches land.
</acceptance-ideas>
