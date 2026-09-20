---
feature: 017
slug: live-ui-sync
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Plan 017 — Live UI sync: board/entries refresh on MCP-side changes

<approach>
One web-only task: add a shared 15s refetchInterval to the four live queries (board, feature detail, entries list, drift). No server work, no editor changes — the polling layer is three small edits plus one constant. Closeout rides the orchestrator.
</approach>

<contract>
- Shared constant LIVE_SYNC_INTERVAL_MS = 15_000 in the FSD-clean home (shared/lib if features would otherwise cross-import).
- refetchInterval on: planning board queryOptions, planning feature detail queryOptions, entries list queryOptions, drift query. refetchIntervalInBackground NOT set (defaults false).
- Nothing else changes.
</contract>

<tasks>
- [ ] 01-live-polling — constant + three query edits + verification → tasks/01-live-polling.md
</tasks>

<risks>
- Feature-023 work touches the planning slice in parallel (backend first, web later) — land this before 023/03 starts or coordinate the shared files (queries.ts is the overlap).
</risks>

<verification>
- lint + tsc + build green; live check on the next packed release: MCP-side change visible within ~15s without reload.
</verification>
