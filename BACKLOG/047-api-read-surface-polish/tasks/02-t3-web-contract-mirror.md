---
feature: 047
task: 02
slug: api-read-surface-polish
status: todo
review: light
created: 2026-10-06
brief: Mirror taskCounts into web/src/features/planning/types.ts PlanningFeatureBoardItem; lint + tsc green. Type-only change.
---

# T3 — Web contract mirror

<goal>
The web TS mirror of the board DTO stays honest with the API contract: PlanningFeatureBoardItem carries taskCounts after taskDone.
</goal>

<scope>
- web/src/features/planning/types.ts — PlanningFeatureBoardItem gains: taskCounts: { todo: number; inProgress: number; review: number; done: number; blocked: number }; placed after taskDone.
</scope>

<anti-scope>
- No UI rendering of the breakdown (spec non-goal — MCP read surface only).
- No other web files, no queue-entry type (web does not call the queue endpoint).
</anti-scope>

<constraints>
- Type-only change; no runtime code.
- Plan set review "none"; board schema floor is light — treat as type-only, nothing to deep-review.
</constraints>

<acceptance-criteria>
- [ ] types.ts carries taskCounts with the five numeric keys.
- [ ] cd web && npm run lint && npx tsc --noEmit green.
</acceptance-criteria>

<refs>
- Spec 420cfc7d-0f9b-4883-a44d-130a911f8625 (item 1 — consumers, web mirror only).
- Plan c505ad37-2f6c-41a6-8f01-20d5ad4ecf96 (T3).
- Shape comes from Task 0 (T1) DTO.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan T3.
</log>