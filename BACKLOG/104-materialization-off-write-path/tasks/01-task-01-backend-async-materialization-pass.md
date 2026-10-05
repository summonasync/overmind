---
feature: 104
task: 01
slug: materialization-off-write-path
status: done
review: light
created: 2026-10-04
brief: Backend: Channel+BackgroundService async pass, materializationSync flag, failure surfaces in lastAttempt
---

# Task 01 — Backend: async materialization pass

<goal>
Mutations stop waiting for the mirror: materialization moves to an in-host async pass with a sync escape hatch.
</goal>

<scope>
- Channel + BackgroundService consumer (sequential, bounded); PlanningServiceGuard.SyncMaterializationAsync becomes enqueue-or-inline per planning.materializationSync (default false = async).
- Manual planning_materialize and import stay synchronous (waiting is their purpose).
- Async pass failure lands in 074 lastAttempt/lastError — pinned.
- Docs strings: eventual-consistency wording in onboarding trigger text and relevant descriptions.
- Tests: write returns before the pass runs; queue drains in order; sync flag restores inline; failure surfaces.
</scope>

<acceptance-criteria>
- [x] Mutation response no longer awaits the pass (async default), sync flag flips back — pinned.
- [x] Queue drains in order; failure lands in lastAttempt — pinned.
- [x] Manual materialize/import still synchronous — pinned.
- [x] Docs strings updated and pinned.
</acceptance-criteria>

<refs>
- Spec 9d4e5c9b; plan 6b287810 (task 1); 074 diagnostics infra; 033 guard semantics.
</refs>

<log>
- 2026-10-04 [agent] — [agent] Claimed by Van (claude), taken by orchestrator — dispatching dotnet-coder (sonnet). Task 04 (render-corruption fix) already done ahead of this one per user's live repro.
- 2026-10-04 [agent] — Coding round complete (dotnet-coder, sonnet): Channel+BackgroundService async pass, enqueue-or-inline dispatcher on planning.materializationSync (default off), import/manual materialize stay sync, failure → 074 lastAttempt, docs say eventual consistency. 21 call sites across 6 services; dead IPlanningMaterializer deps removed from five ctors. Gates: build 0w, Core 316, Infra 633 (+11 new), Api 623 (+1), Mcp 446, format clean. Checkpoint fe0119a. Dispatching light review (dotnet-code-reviewer).
- 2026-10-04 [agent] — [agent] Light review PASS-WITH-NITS (0 blockers, report audits/review-104-01.md; gates re-run independently: build 0w, Core 316, Infra 633, Api 623, Mcp 446, format clean). W1 fixed in-tree (7366ff1): queue-full inline fallback detached from the request token (CancellationToken.None) — sync-flag mode keeps the request token by contract; Infra re-run 633/633, format clean. N1/N2 (worker-loop non-OCE exception surface, shutdown-drain vs host timeout) → microbatch. All 4 AC ticked. Closing task.
</log>
