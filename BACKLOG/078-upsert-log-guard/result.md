---
feature: 078
slug: upsert-log-guard
status: draft
created: 2026-09-25
---

# Result 078 — Upsert log guard: full-body upsert may only append to <log>, never rewrite history

<goal>
Guard the <log> section on full-body upsert doors: tail-append only, rewrites rejected with diagnostics.
</goal>

<outcome>
Implemented at the entity level — PlanningArtifact.UpdateBody calls RequireLogHistoryAppendable before mutation: the stored log (parsed via PlanningLog.Parse) must survive as an exact ordered prefix of the incoming log, compared on trim-normalized PlanningLogLine records. New PlanningFailureCode.LogHistoryNotAppendable with diagnostics (first divergent index, stored/incoming counts, tool hints), mirroring the 039 patch-door pattern. All three doors inherit the guard with zero service-layer change: single upsert (PlanningArtifactService.UpsertAsync), batch (StageTask), re-import (PlanningFeatureService.ImportAsync, atomic via tx).
</outcome>

<deviations>
AC4 was internally contradictory — the pre-existing re-import test encoded silent drift-line dropping, the exact behavior the anti-scope rejects. Test split into a rejection case (rollback verified) + a positive rematerialized-mirror case; reviewer confirmed legitimate, verbatim restore semantics (058) unaffected via MatchesImportedBody tolerance.
</deviations>

<verification>
dotnet build 0w/0e; dotnet test 1344/1344 (coder run + independent reviewer rerun). 13 tests added: 9 entity, 3 service (incl. batch rollback), 2 import (split of one).
</verification>

<follow-ups>
- Feature-body door PlanningFeature.Update still unguarded (feature.md log can be rewritten by stale re-import) — microbatch candidate #74.
- Batch rejection wraps as tasks[N]: — fine, but a dedicated batch-diagnostics polish could name the task brief; cosmetic.
</follow-ups>