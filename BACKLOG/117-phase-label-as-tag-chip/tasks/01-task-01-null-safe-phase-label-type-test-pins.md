---
feature: 117
task: 01
slug: phase-label-as-tag-chip
status: done
review: light
created: 2026-10-06
brief: Widen phase to string|null, make cardPhaseLabel null-safe, extend board-card-context test pins (null/empty/whitespace → null, seven labels pass through).
---

# Task 01 — Null-safe phase label + type + test pins

<goal>
`PlanningFeatureBoardItem.phase` becomes `string | null` and `cardPhaseLabel` returns `string | null`: null or trimmed-empty phase maps to null, anything else passes the server label through verbatim. Test pins extended so the card swap in task 02 consumes a pinned signature.
</goal>

<scope>
- `web/src/features/planning/types.ts` — `phase: string | null` on `PlanningFeatureBoardItem` (only consumer today is `cardPhaseLabel`).
- `web/src/features/planning/lib/board-card-context.ts` — `cardPhaseLabel` return type `string | null` with the trimmed-empty guard.
- `web/tests/board-card-context.test.mts` — new cases: null / `""` / `"   "` → null; the seven server labels (drafting, writing spec, planning, plan validated, in progress, review, done) pass through; existing passthrough and done-card pins stay green (the `assert.notEqual(…, null)` done pin survives the signature change).
</scope>

<anti-scope>
- `web/src/features/planning/components/feature-board.tsx` — task 02.
- Server: `CeremonyPhasePlanner`, planning contracts, mapper.
- Any visual change to the card.
</anti-scope>

<constraints>
- English-only, no comments, no new dependencies.
- Gates in `web/`: `npm test`, `npx tsc --noEmit`.
</constraints>

<acceptance-criteria>
- [x] `phase` is `string | null` on `PlanningFeatureBoardItem`; `npx tsc --noEmit` green.
- [x] `cardPhaseLabel` returns null for null/empty/whitespace phase and the verbatim label otherwise.
- [x] Tests cover the null/empty/whitespace mapping and the seven-label passthrough; existing pins stay green.
- [x] `npm test` and `npx tsc --noEmit` pass in `web/`.
</acceptance-criteria>

<refs>
- spec e686fcab-11b4-426c-84b2-07ed7dc71fc6 — Decisions (null safety web-side only) + AC 6.
- plan 225972ec-e653-4d00-81a8-6eb0027ab096 — task 01.
</refs>

<obstacles>
- None at cut time.
</obstacles>

<log>
- 2026-10-06 [agent] — task cut from validated plan task 01.
- 2026-10-06 [agent] — 117/01 coding round started
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers. Mutation-verified pins (both guard halves load-bearing), seven labels verified against CeremonyPhasePlanner truth, no-trim divergence judged contract-consistent. The WARNING (tooltip null-coercion at feature-board.tsx:251) is already resolved — task 02 (1133e30) removed that span entirely. Commit 2f4cac2.
</log>