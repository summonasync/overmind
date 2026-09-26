---
feature: 068
task: 04
slug: mutation-transparency
status: done
review: light
created: 2026-09-26
brief: current_task tells result states apart, skips review/done with state held, carries soft spec/plan warnings
---

# 068/04 — Honest current_task

<goal>
Resolver truthfulness (spec R4–R6 / OM-13 + D3, MindlashStudio intake): current_task stops handing out unworkable work and tells the agent what the feature actually awaits.
</goal>

<scope>
- Result states (resolver lives in Host/Api AgentPlanningApi.Current per 030/04): state stays "empty", artifact null, reasons verbatim — no result: "All tasks are done — write the result artifact."; result draft: "Result artifact drafted — validate it."; result validated: "Result validated — close the feature."
- Task picking: candidates are todo/inProgress by Order only; review/done skipped. If none workable remains and review tasks exist → state "held", artifact null, reason "<n> in review — awaiting review completion."; the all-blocked branch keeps its existing reason.
- Soft warnings on task payloads: "spec missing" when no spec artifact; "spec not validated" when spec is draft; "plan not validated" when plan is missing or draft. Never STOP.
- overmind:// current resource texts and the MCP tool description updated to match (PlanningResourceContractTests pins).
</scope>

<anti-scope>
- Feature-level dependency handling (030 owns it); gate STOP steps unchanged; no new endpoints.
</anti-scope>

<constraints>
- .NET 10 / C# 14; no comments; English-only; no git commit/push; dotnet build + dotnet test green.
</constraints>

<acceptance-criteria>
- [x] Three result states produce the three verbatim reasons (Api test pins incl. existing Current_AllTasksDone updated)
- [x] Feature with only review/blocked tasks → state "held" with the naming reason
- [x] Task payload warnings appear/correctly absent in all four spec/plan combinations
- [x] Resource + tool description texts pinned in Mcp tests
- [x] dotnet build + test green
</acceptance-criteria>

<refs>
- Spec 9f1fa31e R4–R6/D3; 006/04 resolver contract; 030/04 location finding; MindlashStudio gap 2 intake.
</refs>

<obstacles>
- Warnings attach to every "task" payload, including the all-blocked branch (blocked artifact + warnings) — reading "task payloads" broadly on purpose; held/empty payloads carry no warnings (their reasons already state the situation).
- held reason counts tasks in Review only; a blocked-only feature is never held (existing branch preserved per scope).
- Result-state detection reads the FIRST result artifact's status; the model allows several result-kind artifacts — a second result loses to the first (flag for a follow-up if multi-result needs a rule).
- MCP planning_current_task passes the payload through LeadWithStopReason, which reorders reason-first for "stop" only — held/empty payloads keep feature-first ordering in the tool output (the reason is present; the resource leads with HELD/NOTHING TO DO). Noted behavior, not changed.
- The existing Current_AllTasksDone test already pinned the no-result verbatim reason, so it survived unchanged as that pin; draft/validated result states got their own tests.
</obstacles>

<log />

<log>
- 2026-09-26 [agent] — execution started: honest current_task — three result states, held state, todo/inProgress picking, soft spec/plan warnings, resource + tool texts
- 2026-09-26 — 068/04 implemented. Resolver (Host/Api AgentPlanningApi.Current): task picking among todo/inProgress only by Order — review/done skipped; only review tasks remain → state "held", artifact null, reason "<n> in review — awaiting review completion."; review+blocked mix → held (review count); all-blocked keeps the existing branch (state "task" + blocked artifact + "The next task is blocked."); three result states on all-done (state "empty", artifact null): no result → "All tasks are done — write the result artifact.", result draft → "Result artifact drafted — validate it.", result validated → "Result validated — close the feature." Soft warnings on task payloads (SpecPlanWarnings): "spec missing" / "spec not validated" / "plan not validated", independent checks, null (absent via JsonIgnore WhenWritingNull) when none — never STOP, gate STOP steps and dependency stop untouched. PlanningCurrentResponse gained optional Warnings. Resource (OvermindPlanningResources.RenderCurrent): "held" renders "HELD: {reason}", task payloads list "Warnings: …" beneath the header; resource description mentions HELD + warnings. MCP planning_current_task description updated (held, three all-done states, soft warnings). Canned: PlanningCurrentTaskJson now carries warnings ["plan not validated"], new PlanningCurrentHeldJson + PlanningHeldSlug + stub route. Pins: Api — result draft/validated states, held, review+blocked held, all-four warnings combinations (existing Current_AllTasksDone stays as the no-result pin; Current_AllRemainingBlocked keeps the blocked branch); Mcp — current-task payload warnings pin, held-state tool payload pin, resource HELD + Warnings render pins. Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1480 passed / 0 failed (Core 215, Infrastructure 484, Api 429, Mcp 352); format clean on touched files.
- 2026-09-26 [agent] — 068/04 done: all 5 AC ticked. current_task picks among todo/inProgress only (review/done skipped), returns state "held" with "<n> in review — awaiting review completion." when only review/blocked remain (all-blocked branch preserved), distinguishes three all-done result states with verbatim reasons, and carries soft spec/plan warnings on task payloads (never STOP; gate + dependency stops untouched). overmind:// current resource renders HELD and Warnings lines; planning_current_task description updated; Canned + stub route extended. Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1480 passed / 0 failed (Core 215, Infrastructure 484, Api 429, Mcp 352); format clean on touched files. Review label: light. Edge notes in <obstacles> (blocked-branch warnings, first-result-wins, LeadWithStopReason stop-only reordering).
- 2026-09-26 — Micro fix-round for review PASS-WITH-NITS (audits/review-068-04.md) — test-only, no production changes. S3: new pin Current_WorkableTodoBeatsHeld_ReturnsTheTodoTask (task1 review + task2 todo → state "task", artifact = todo task order 2, no held — the precedence keeping one parked task from blocking a feature) and the blocked-branch pin extended with the payload warnings assert (Current_AllRemainingBlocked now also checks ["spec missing", "plan not validated"]). S2: new wire-level pin Current_CleanSpecPlan_RawWirePayload_OmitsTheWarningsKey — raw JSON of a task response over validated spec+plan parses into a JsonObject with ContainsKey("warnings") false (DTO-independent). S1/S4 untouched (microbatched). Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1482 passed / 0 failed (Core 215, Infrastructure 484, Api 431, Mcp 352); format clean on the touched file.
</log>
