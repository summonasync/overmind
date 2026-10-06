---
feature: 047
slug: api-read-surface-polish
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Result 047 — API read-surface polish: per-status breakdown в list_features + три докгэпа описаний тулов

## Shipped

**Item 1 — per-status task counts (main).** `taskCounts: {todo, inProgress, review, done, blocked}` — five keys, invariant sum == taskTotal — landed on both projections: `PlanningFeatureBoardItem` and `PlanningQueueItem` (Host contracts + `PlanningMapper.ToBoardItem`/`ToQueueItem`, zero-task features return all zeros, never null). MCP `planning_list_features` and `planning_task_queue` pick it up through their existing pass-through; the «4/7» blindness is gone — blocked and unplanned are now distinguishable in one read, no feature-body fetch needed.

**Items 2 + 4 — doc-gap texts, verbatim per spec.** `planning_list_features`/`planning_task_queue` descriptions mention `taskCounts`; the `planning_upsert_artifact` body parameter no longer reads «with its XML sections» — replaced by the pointer to the per-kind section list (plain markdown accepted, only `<log>` machine-parsed for documents); both upsert descriptions and the `order` parameter carry the holes-are-legal sentence (order = sort key AND stable handle for blockedBy edges and `T{order}` aliases; never renumber to close a hole). All texts pinned in `PlanningToolContractTests`, canned board/queue JSON in `Canned.cs` carries consistent counts, pass-through asserted.

**Item 3 — `overmind://resources` pointer:** superseded by 070, tombstoned in the spec, no code — as planned.

**Web contract mirror.** `PlanningFeatureBoardItem` in `web/src/features/planning/types.ts` carries `taskCounts` — rendering stays a non-goal (MCP read surface only).

## Verification

- Full gate: `dotnet build Overmind.sln` + full serial `dotnet test Overmind.sln` — 2246/2246 green.
- Web: `npm run lint && npx tsc --noEmit` green.
- Consistency sweep over `taskTotal` mirrors done — no straggler mirror left asserting the old shape.
- All four tasks approved (light review each); gate counts corroborated by lane reviews.
- Compat held: additive fields only, `taskTotal`/`taskDone` untouched; old adapters and the web board see no difference.

## Commits

- `caa6f31` — T1: taskCounts on Host projections + Api.Tests mirrors.
- `3aca50c` — T2: MCP description texts + Canned + contract pins.
- `8e7667d` — T3: web types mirror.

## For the human

1. **Spec needs a re-tap** (notification `80e2a9a1`): ticking the spec's ACs required a full upsert (markdown ACs are otherwise untickable — tooling gap), which demoted the spec to draft v2. Content unchanged — checkboxes only. Re-approve at will.
2. **Ledger findings filed:** #267 — `planning_patch_artifact` still carries a stale phrase of the same family (out of this feature's scope); #268 — the demotion-on-AC-tick tooling gap itself, candidate for a small tooling feature.

<log>
- 2026-10-06 [agent] — result drafted: all four items shipped per validated spec/plan; gate 2246/2246 + web green; commits caa6f31, 3aca50c, 8e7667d; human doors: spec re-tap (80e2a9a1), ledger #267/#268.
</log>