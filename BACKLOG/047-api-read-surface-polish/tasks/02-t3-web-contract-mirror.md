---
feature: 047
task: 02
slug: api-read-surface-polish
status: done
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
- [x] types.ts carries taskCounts with the five numeric keys.
- [x] cd web && npm run lint && npx tsc --noEmit green.
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
- 2026-10-06 [agent] — 047/T3 coding round started
- 2026-10-06 [agent] — taskCounts?: {todo,inProgress,review,done,blocked:number} added to PlanningFeatureBoardItem after taskDone — optional so tsc stays green with or without T1 in the build. Gates: lint 0 errors (6 pre-existing warnings in unrelated files), tsc clean, tests 263/263, next build green. Commit 8e7667d (no push).
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 1 NOTE (optional `?` vs scope's required — deliberate: field absent until T1 lands, keeps tsc green in both states; can tighten after T1 lands). Shape verified against T1's in-tree DTO (camelCase 1:1, sum == taskTotal), types-only anti-scope clean, spot-checks green. Commit 8e7667d. (This done-flip corrects the alias mix-up that briefly landed on the gate task fda3356f — reopened there.)
</log>