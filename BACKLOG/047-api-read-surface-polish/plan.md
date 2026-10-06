---
feature: 047
slug: api-read-surface-polish
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Plan 047 — API read-surface polish: per-status breakdown в list_features + три докгэпа описаний тулов

## Strategy

Three code surfaces, ordered by blast radius: (1) Host DTO + mapper — the single source both MCP doors mirror, (2) MCP description texts + their pins, (3) web TS contract mirror. Description texts are written ONCE here, pinned verbatim by tests; implementers copy from the spec, not from memory. After (1)–(3): full-solution gate. Additive-only compat rule governs every step.

Exact description texts live in the spec (items 2 and 4) — implementers take them verbatim from there, pinned substrings chosen at implementation time and asserted in `PlanningToolContractTests`.

## Task breakdown

### T1 — TaskCounts on Host projections (board + queue)

Files:
- `src/Overmind.Host/Api/Contracts/PlanningContracts.cs` — new `sealed record PlanningTaskCounts(int Todo, int InProgress, int Review, int Done, int Blocked)`; add `PlanningTaskCounts TaskCounts` to `PlanningFeatureBoardItem` (after `TaskDone`) and `PlanningQueueItem` (after `TaskTotal`).
- `src/Overmind.Host/Api/Mapping/PlanningMapper.cs` — private `TaskCounts(IReadOnlyCollection<PlanningArtifact>)` counting kind==Task by status (all five buckets always present); wire into `ToBoardItem` and `ToQueueItem`. Zero tasks → all zeros.
- `tests/Overmind.Api.Tests/PlanningApiTests.cs` — extend the local board mirror record (~:1410) with `TaskCounts`; fixture with mixed task statuses (todo/inProgress/review/done/blocked) asserts exact counts and `sum == taskTotal`; zero-task feature asserts all-zeros object.
- `tests/Overmind.Api.Tests/PlanningQueueApiTests.cs` — same for the queue mirror records (~:566/:573) + one count assertion.

Verify: `dotnet build Overmind.sln` then `dotnet test tests/Overmind.Api.Tests`. Review: light.

### T2 — MCP descriptions + pass-through pins

Files:
- `src/Overmind.Mcp/OvermindPlanningTools.cs`:
  - `planning_list_features` description (~:97): mention `taskCounts` (todo/inProgress/review/done/blocked, sums to taskTotal) in the fields sentence.
  - `planning_task_queue` description (~:501): insert `taskCounts` into the enumerated field list right after `taskDone, taskTotal`.
  - `planning_upsert_artifact` `body` parameter (~:246): replace «Markdown body of the artifact with its XML sections.» with the spec item-2 pointer text.
  - `planning_upsert_artifact` description (~:241) + `planning_upsert_tasks` description (~:282): append the spec item-4 holes-are-legal sentence to the order block; `order` parameter (~:247) gets the short handle/gaps note.
- `tests/Overmind.Mcp.Tests/Canned.cs` — board (~:122-123) and queue (~:236, 241, 246) canned JSON gain `taskCounts` values consistent with their `taskTotal`/`taskDone`.
- `tests/Overmind.Mcp.Tests/PlanningToolContractTests.cs` — pins: `taskCounts` mentioned in both read-tool descriptions; body-param pointer fragment present AND the old «with its XML sections» phrase absent from the body param description; holes-are-legal fragment present in both upsert descriptions. Pass-through: `list_features` and `task_queue` tool output carries `taskCounts` from the canned responses.

Verify: `dotnet test tests/Overmind.Mcp.Tests`. Review: light.

### T3 — Web contract mirror

Files:
- `web/src/features/planning/types.ts` — `PlanningFeatureBoardItem` gains `taskCounts: { todo: number; inProgress: number; review: number; done: number; blocked: number };` after `taskDone`. No UI rendering (spec non-goal), no other web files.

Verify: `cd web && npm run lint && npx tsc --noEmit`. Review: none (type-only).

### T4 — Full gate

`dotnet build Overmind.sln` + full serial `dotnet test Overmind.sln` (flake per AGENTS.md: cold-CLR `0x80131506` → `dotnet build-server shutdown`, retry once). Sweep for stragglers: `grep -rn "taskTotal" src tests web/src` to catch any mirror record or doc the counts should also touch (AgentSessionLockApiTests/FeatureDependencyApiTests local mirrors deserialize JSON leniently — extend only if a count assertion is wanted there, otherwise leave).

## Verification gates

- Per task: the commands above.
- Feature close: build + full test suite green, web lint/tsc green, all spec AC ticked.

## Compat note

Additive only, by construction: no field removed or renamed; `taskTotal`/`taskDone` keep position and semantics; `taskCounts` is a new object both doors mirror. Pass-through consumers (MCP adapters, web) ignore unknown JSON fields — old adapters see no difference. Description edits are text-only; tool names, parameters and behavior untouched. The only behavior-adjacent risk is positional-record constructor churn in Host contracts — internal call sites only (PlanningMapper + two Api files), compiler-enforced.

## Risks

- Contract-pin drift: description edits break existing substring pins in `PlanningToolContractTests` (e.g. pins on surrounding text) — T2 runs the whole Mcp.Tests suite, failures resolve by updating the pin to the new text, never by loosening the assertion.
- Canned JSON inconsistency (counts not summing) would teach the wrong invariant — T2 pins consistency while editing Canned.cs.

<log>
- 2026-10-06 [agent] — plan drafted from spec 420cfc7d: three surfaces (Host DTO/mapper → MCP descriptions+pins → web mirror) + full gate; texts taken verbatim from the spec; additive-only compat rule stated.
- 2026-10-06 [agent] — validated with human proof: notification 25b83593-6332-4841-a85d-7b21bfe9e9b8 replied 2026-10-06 13:05:12 UTC
</log>