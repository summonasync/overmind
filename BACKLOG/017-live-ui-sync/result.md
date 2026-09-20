---
feature: 017
slug: live-ui-sync
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 017 — Live UI sync: board/entries refresh on MCP-side changes

<outcome>
017-live-ui-sync shipped (MVP polling layer): a single shared constant LIVE_SYNC_INTERVAL_MS = 15_000 (web/src/shared/lib/live-sync.ts) drives refetchInterval on the four live queries — planning board, feature detail (workspace incl. artifact bodies — the dogfood pain), entries list, and the drift panel. MCP-side changes now appear in an open UI within ~15s without any manual refresh. refetchIntervalInBackground stays false (hidden tabs stop polling); tags, versions, workflow, and the current-task endpoint are deliberately excluded; mutations/invalidations/editors untouched — polling is additive background refetch. SSE/server-push stays a separate future feature per the spec decision.
</outcome>

<evidence>
- Review: audits/review-017-01.md — PASS, 0 blockers. Scope verified exactly four query options (board :27, feature :35, drift :57 in planning/queries.ts; entries list :21), single 15_000 literal in web/src (grep), no refetchIntervalInBackground anywhere, no cross-feature imports.
- Polling vs the 022/012 optimistic mutations reasoned and accepted: the only theoretical window is a board poll tick mid-mutation before onSuccess invalidation — self-healing, and within the spec's "no optimistic concurrency" decision.
- Drift panel's enabled-guard verified intact (polling silenced when the panel is off).
- lint 0 errors / tsc 0 / build green (10/10 routes).
- Live-timing dogfood (change visible within 15s) deferred to the next packed release — the production host still runs the pre-feature web bundle.
</evidence>

<deferred>
- Microbatch: kits-slice drift query carries no interval (kit drift goes stale the same way) — add the same constant; health's own 30_000 literal could migrate to a shared knob.
- SSE/server-push instead of polling — separate future feature (the feature body's own "дальше MVP" clause); the shared constant is the seam.
</deferred>
