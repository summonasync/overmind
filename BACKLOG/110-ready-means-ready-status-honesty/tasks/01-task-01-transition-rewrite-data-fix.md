---
feature: 110
task: 01
slug: ready-means-ready-status-honesty
status: done
review: light
created: 2026-10-04
brief: Backend: transition rewrite (ceremony=ready, take=inProgress) + startup data-fix + tests
---

<log>
- 2026-10-05 [agent] — taken by orchestrator — dispatching dotnet-coder (sonnet) for transition rewrite + startup data-fix
- 2026-10-05 [agent] — AC ticked: pinned by UpsertArtifact_CreateSpec_OnDraftFeature_MakesFeatureReady, UpsertArtifact_CreatePlan_OnReadyFeature_StaysReady, UpsertArtifact_CreateSpec_WritesAutoTransitionLine + API UpsertTasks five-in-one pin
- 2026-10-05 [agent] — AC ticked: pinned by SetStatusAsync_TaskTaken_OnReadyFeature_PromotesFeatureToInProgress (service) + SetArtifactStatus_TaskTaken_PromotesReadyFeatureToInProgress (agent HTTP door)
- 2026-10-05 [agent] — AC ticked: full suites green after rewrite — review demote on doc invalidation, reopen on task create/delete, review rollback via door all passing (Infrastructure 683/683, Api 656/656, Core 365/365, Mcp 486/486)
- 2026-10-05 [agent] — AC ticked: implemented as startup backfill service (PlanningStatusBackfill, AppSettings-stamped one-shot, after backlog import in Program.cs) not an EF migration — [auto] log line and materialization dispatch need C#; matrix pinned in PlanningStatusBackfillTests (no tasks / all todo / taken inProgress-review-done / non-inProgress untouched / stamped no-op / soft-deleted task)
- 2026-10-05 [agent] — coder round complete: TransitionOnArtifactCreate keeps/makes features ready (draft → ready, "artifact created → ready"); task-take promotion in PlanningArtifactService.SetStatusAsync (either door, "task NN taken → inProgress", [auto], draft or ready); PlanningStatusBackfill startup service (AppSettings-stamped one-shot, after backlog import) demotes inProgress-without-taken-task to ready + materialization dispatch
- 2026-10-05 [agent] — tests: 10 pins updated to honest semantics, 3 new service pins (take promotes / second take no duplicate / review stays on task reopen), 7 backfill matrix tests, 1 API door pin; gates: solution build 0w/0e, Infrastructure 683/683, Api 656/656, Core 365/365, Mcp 486/486, format clean
- 2026-10-05 [agent] — coder round complete — full body re-upsert adding obstacles + completion log lines
- 2026-10-05 [agent] — light review PASS-WITH-NITS 0 blockers (ceremony keeps ready, take promotes via one shared path both doors, backfill idempotent + race-free with async materialization, 109 done-gate intact, legacy pins strengthened not weakened). Orchestrator fix round: nit 2 — direct draft-take pin added (import-shaped state via sourced SetStatus, promotion + [auto] line asserted). Gates re-run on fresh binaries: build 0 errors, Infra 684 (+1), Api 656, Mcp 486, format green. Nits 1+3 -> task 02 scope (import-liars consistency sweep + stale OnboardQuickstart line).
</log>
<goal>
ready means «not being worked on»; inProgress means a task is taken.
</goal>
<scope>
- Auto-transition «artifact create → inProgress» removed: ceremony writes (spec/plan upserts, validations) keep/make the feature ready (draft → ready on the first spec draft stays).
- First task-take (either door, task → inProgress) promotes the feature ready → inProgress with an [auto] line; review-rollback path untouched.
- One-time data-fix on startup: inProgress features with no taken task (none, or all todo) → ready with an [auto] log line; features with any taken task stay.
- No auto-demote (challengeable default recorded).
- Tests: ceremony keeps ready; take promotes; data-fix matrix (no tasks / all todo / one taken); review paths intact.
</scope>
<acceptance-criteria>
- [x] Spec/plan upserts + validation leave the feature ready — pinned.
- [x] Task-take promotes ready → inProgress with [auto] line — pinned.
- [x] Data-fix matrix pinned (startup migration).
- [x] Review/rollback paths green.
</acceptance-criteria>
<refs>
- Spec c323a41a; plan 9f3b98c1 (task 1); 091 phase planner (machine-state preference).
</refs>
<obstacles>
- Manual Ready → Review is now refused by the transitions map (review reachable only via inProgress, or via the auto all-done promotion which fires from Ready). One test had to take a task before moving to review. Task 02's consistency sweep should check no UI flow relies on hand-moving a ceremony-only feature straight to review.
- Take-promotion extended to Draft → inProgress (spec literally says ready → inProgress): a draft feature with a taken task is import-only (any artifact create makes it ready), so the literal reading would leave a lying state; transitions map already allows draft → inProgress. Recorded as a conservative honesty extension.
- OnboardQuickstart line still says "artifact create → inProgress" (auto-transitions summary) — now stale; left untouched because the plan assigns docs lines to task 02.
</obstacles>