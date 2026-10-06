---
feature: 047
task: 00
slug: api-read-surface-polish
status: done
review: light
created: 2026-10-06
brief: Add PlanningTaskCounts (5 keys) to BoardItem+QueueItem DTOs and PlanningMapper; extend Api.Tests mirrors with mixed-status and zero-task assertions.
---

# T1 — TaskCounts on Host projections (board + queue)

<goal>
Both Host projections carry a per-status task breakdown so a feature row is self-sufficient: "taskCounts": {"todo": 3, "inProgress": 2, "review": 1, "done": 4, "blocked": 1} — five keys, always summing to taskTotal, never null.
</goal>

<scope>
- src/Overmind.Host/Api/Contracts/PlanningContracts.cs — new sealed record PlanningTaskCounts(int Todo, int InProgress, int Review, int Done, int Blocked); add PlanningTaskCounts TaskCounts to PlanningFeatureBoardItem (immediately after TaskDone) and PlanningQueueItem (immediately after TaskTotal).
- src/Overmind.Host/Api/Mapping/PlanningMapper.cs — private TaskCounts(IReadOnlyCollection<PlanningArtifact>) counting kind==Task by status across all five buckets (every key present even at zero); wire into ToBoardItem and ToQueueItem. Zero tasks → all-zeros object.
- tests/Overmind.Api.Tests/PlanningApiTests.cs — extend the local board mirror record (~line 1410) with TaskCounts; fixture with mixed task statuses (todo/inProgress/review/done/blocked) asserts exact counts and sum == taskTotal; zero-task feature asserts all zeros.
- tests/Overmind.Api.Tests/PlanningQueueApiTests.cs — same for the queue mirror records (~lines 566/573) plus one count assertion.
</scope>

<anti-scope>
- taskTotal/taskDone semantics and positions stay untouched.
- No MCP-layer or web changes here (T2/T3).
- No UI rendering, no changes to statuses or transitions (spec non-goals).
</anti-scope>

<constraints>
- Additive only — no field removed or renamed.
- Fresh tasks start Todo (PlanningArtifact.cs:96); draft/validated are document-only — the five buckets always sum to taskTotal.
- JSON camelCase, matching existing contract style.
- No comments in code; no commits.
</constraints>

<acceptance-criteria>
- [x] taskCounts with 5 keys present on PlanningFeatureBoardItem and PlanningQueueItem; sum equals taskTotal on a mixed-status fixture.
- [x] Zero-task feature returns an all-zeros object, not null.
- [x] Board endpoint and queue endpoint both serialize taskCounts (Api tests pass).
- [x] dotnet build Overmind.sln green; dotnet test tests/Overmind.Api.Tests green.
</acceptance-criteria>

<refs>
- Spec 420cfc7d-0f9b-4883-a44d-130a911f8625 (item 1 — contract + shape decision).
- Plan c505ad37-2f6c-41a6-8f01-20d5ad4ecf96 (T1).
- Task 1 (T2) consumes these DTOs via pass-through; Task 3 (T3) mirrors the shape into TS.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan T1.
- 2026-10-06 [agent] — 047/T1 coding round started
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers. Five-bucket invariant verified to domain level (TaskStatuses closed set, invalid statuses throw — sum == taskTotal by construction), passthrough confirmed (Mcp needs nothing for T1), mutation-checked tests, spot-check 253/253. Commit caa6f31.
</log>