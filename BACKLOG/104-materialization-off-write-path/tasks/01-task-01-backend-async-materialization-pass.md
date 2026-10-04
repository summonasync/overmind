---
feature: 104
task: 01
slug: materialization-off-write-path
status: inprogress
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
- [ ] Mutation response no longer awaits the pass (async default), sync flag flips back — pinned.
- [ ] Queue drains in order; failure lands in lastAttempt — pinned.
- [ ] Manual materialize/import still synchronous — pinned.
- [ ] Docs strings updated and pinned.
</acceptance-criteria>

<refs>
- Spec 9d4e5c9b; plan 6b287810 (task 1); 074 diagnostics infra; 033 guard semantics.
</refs>

<log>
- 2026-10-04 [agent] — [agent] Claimed by Van (claude), taken by orchestrator — dispatching dotnet-coder (sonnet). Task 04 (render-corruption fix) already done ahead of this one per user's live repro.
</log>
