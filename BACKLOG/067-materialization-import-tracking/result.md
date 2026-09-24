---
feature: 067
slug: materialization-import-tracking
status: draft
created: 2026-09-24
---

# Result 067 — Materialization × import: импорт регистрирует файлы в трекинге, skip-not-abort, planning_materialize MCP-триггер (OM-10)

<outcome>
OM-10 closed end-to-end across three tasks: the import path registers every file it reads into PlanningMaterializedFiles with its raw-bytes sha (strategy On only), per-file guard conflicts skip with a stated reason instead of aborting the pass (partial success stamps lastRun; all-skipped keeps the 409), and the agent surface caught up — planning_materialize MCP tool as a manual rerun trigger plus onboard skip visibility. The field acceptance core (import a DMT-shaped tree, mutate the board, watch the BACKLOG tree materialize without refusals) is pinned by tests.
</outcome>

<evidence>
- Task 01 (import registers rows): light review PASS-WITH-NITS — audits/review-067-01.md; single-read sha transport, atomic upsert seeding, raw-bytes invariant verified against the materializer's own hashing.
- Task 02 (skip-not-abort): light review PASS-WITH-NITS — audits/review-067-02.md; skip-never-writes verified at both guards, stamp honesty pinned both ways, 409 AllSkipped preserved, the 033 suite migrated test-by-test with byte invariants.
- Task 03 (MCP tool + onboard skips + docs): planning_materialize(projectId, force=false) wraps POST /api/projects/{id}/planning/materialize with a compact written/skipped/force echo and a 5-entry skipped head; description pinned exact (032 pattern) including the board-mutation-triggers-a-pass note. Onboard materialization block gained skippedCount + skipped head persisted on Project (LastMaterializationSkipCount/LastMaterializationSkipHead, migration 20260924143916_AddProjectLastMaterializationSkips), rendered by the MCP onboard tool; lastError stays for total failures. NIT-1 from review-067-02 closed with a pin: single-skip AllSkipped carries a relative OffendingPath (Sync_AllPlansGuardedSingleSkip_OffendingPathStaysRelative). CONTEXT.md gained the git-discipline paragraph for the mirror plus truthful skip-semantics in the materialization bullet. Light review of task 03 follows the cadence (audits/review-067-03.md).
- Gates: dotnet build Overmind.sln 0W/0E; dotnet test Overmind.sln 1218/1218 (Core 197 / Infra 377 / Api 349 / Mcp 295), known flakes (PlanningSetStatusDescription_QuotesAllowedStatusesPerKind, ResourceContractTests decisions) did not fire.
</evidence>

<deferred>
- Review NITs to microbatch: review-067-01 (CRLF remove-path conservatism, Archive InlineData); review-067-02 (archive-copy rows linger after an all-guarded ArchiveToOn pass — self-healing on the next pass, logged in task 02).
- planning_materialize does not echo force-pass warnings (written/skipped/force only); candidate microbatch if the field wants them.
</deferred>
