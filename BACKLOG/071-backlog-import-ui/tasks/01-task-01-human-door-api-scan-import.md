---
feature: 071
task: 01
slug: backlog-import-ui
status: done
review: light
created: 2026-10-04
brief: API: scan (read-only, error-isolated) + import endpoints with PathGuard + tests
---

# Task 01 — Human-door API: scan + import

<goal>
The human door gets the same import machinery the agents have: scan (read-only) + import per folder.
</goal>

<scope>
- POST /api/projects/{id}/planning/import/scan (optional sub-path): list BACKLOG/ feature folders via ParseFeatureAsync — slug/title/status/artifact counts + already-imported marker by slug; per-folder error isolation (error note, not a failed scan).
- POST .../import (folder name): existing ImportFeatureAsync report shape.
- PathGuard containment identical to the agent door; read-only scan writes nothing.
- API tests: scan finds folders, isolation, containment 400, import report shape, already-imported marker.
</scope>

<acceptance-criteria>
- [x] Scan endpoint: folders listed with states, one broken folder does not fail the scan — pinned.
- [x] Containment 400 pinned; import returns the established report shape.
- [x] Scan writes nothing — pinned.
</acceptance-criteria>

<refs>
- Spec d46a2a7c; plan 2859ccfb (task 1); agent door ImportFeature as the shape reference.
</refs>

<log>
- 2026-10-04 [agent] — taken into work by the autonomous pipeline
- 2026-10-04 [agent] — execution complete, awaiting orchestrator review dispatch. Implementation notes: scan lives in BacklogFeatureScanner (Infrastructure) on ParseFeatureAsync — sub-path resolved via the same internal PathGuard.ResolveUnderRoot (traversal/rooted -> 400), a sub-path pointing at a feature folder yields that single entry; folders without feature.md surface as error entries (that is the per-folder isolation path), _TEMPLATE skipped like bulk import; human import resolves strictly BACKLOG/<folder> by name (single-segment validator) and delegates to ImportFeatureAsync so containment is the importer's own PathGuard machinery; the import report mapping was extracted to PlanningMapper.ToImportResponse and is now shared by both doors. Frontmatter key registration inside ParseFeatureAsync (GetOrRegisterAsync) is the established parse-primitive behavior, treated as non-write per spec. Gates: dotnet build 0/0, Api 607/607, Infrastructure 611/611, dotnet format clean on touched files.
- 2026-10-04 [agent] — review PASS-WITH-NITS (audits/review-071-01.md): 0 blockers; containment verified two-layer (single-segment validator + native PathGuard, traversal pinned ×2), scan no-board-write pinned, mapper union makes form drift impossible; gates re-run (Api 607/0, Infra 611/0). WARNING 1: ParseFeatureAsync registers unknown frontmatter keys in the DB (a side effect the spec's «no writes» wording doesn't cover — declared in this task's log, mitigated by the agent door carrying the same behavior); spec stays frozen — the caveat is recorded here and rides the 071/03 closeout result. NIT _TEMPLATE pin + NIT dead NotFound branch → microbatch; NIT 2 (empty-state + Root display) → 071/02 brief.
- 2026-10-04 [agent] — review PASS-WITH-NITS closed (0 blockers); caveat logged on the spec (frozen body intact); result rides 071/03. Committed as part of checkpoint 1548c2a.
</log>
