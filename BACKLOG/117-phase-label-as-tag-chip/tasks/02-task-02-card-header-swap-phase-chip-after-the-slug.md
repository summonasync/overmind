---
feature: 117
task: 02
slug: phase-label-as-tag-chip
status: done
review: light
created: 2026-10-06
depends_on: 01
brief: Card header swap in feature-board.tsx: slug row gains a neutral Badge phase chip after the slug; old uppercase span and Ceremony phase tooltip removed.
---

# Task 02 — Card header swap: phase chip after the slug

<goal>
On the board card, the phase label renders as a shared `Badge` chip (default neutral tone) on the slug line, immediately after the slug, inside the header link; the old plain-text span and its `Ceremony phase: …` tooltip are removed from the right-side group.
</goal>

<scope>
- `web/src/features/planning/components/feature-board.tsx` — the slug line inside the header link becomes an inline flex row: slug span (`min-w-0 truncate font-mono text-xs text-muted`) + phase chip (`Badge`, neutral, `shrink-0`) rendered only when `cardPhaseLabel` returns non-null; delete the old `text-[10px] uppercase tracking-wide` span and its title tooltip from the right-side group — blocked badge and status select keep their order.
</scope>

<anti-scope>
- `board-card-context.ts`, `types.ts`, tests — task 01.
- Server: `CeremonyPhasePlanner`, planning contracts, mapper.
- Claim chip, artifact chips, priority select, status select, board filters/ordering/search.
- Phase→color mapping — not prescribed, out of scope.
</anti-scope>

<constraints>
- Reuse `shared/ui/badge` — no new chip variant, no local span copy of the claim-chip pattern.
- English-only, no comments, no new dependencies.
- Gates in `web/`: `npm run lint && npx tsc --noEmit && npm test && npm run build`.
- The human visual tap on the dashboard board card (chip after the slug; header row unchanged on a null-phase card) rides the feature close — not provable by the coder.
</constraints>

<acceptance-criteria>
- [x] Phase chip renders on the slug line immediately after the slug, inside the card header link, styled with the shared `Badge` neutral tone — consistent with the version/spec/plan chips.
- [x] All seven phase values render through the chip.
- [x] Null/absent/blank phase renders no chip at all — no empty chip box, no layout shift in the header row or slug line.
- [x] The old plain-text phase span and its `Ceremony phase: …` tooltip are gone from the right-side group; blocked badge and status select keep their order.
- [x] Long slug still truncates on one line with the chip visible; slug and chip never wrap.
- [x] `npm run lint && npx tsc --noEmit && npm test && npm run build` green in `web/`.
</acceptance-criteria>

<refs>
- spec e686fcab-11b4-426c-84b2-07ed7dc71fc6 — Scope, Decisions (Badge neutral reuse, tooltip dropped), AC 1–6.
- plan 225972ec-e653-4d00-81a8-6eb0027ab096 — task 02.
- task 01 (this feature) — consumes the null-safe `cardPhaseLabel` signature.
</refs>

<obstacles>
- None at cut time. Main session sequences coders — 120 touches the same file, do not start without the coordinator's go.
</obstacles>

<log>
- 2026-10-06 [agent] — task cut from validated plan task 02; blockedBy task 01 per the plan's 01 → 02 chain.
- 2026-10-06 [agent] — 117/02 coding round started
- 2026-10-06 [agent] — 117/02: chip wired on the slug line — flex row inside the header link, shared Badge default neutral tone, shrink-0
- 2026-10-06 [agent] — 117/02: chip renders cardPhaseLabel verbatim — no mapping or filtering, every phase value flows through the same path
- 2026-10-06 [agent] — 117/02: chip renders only when phaseLabel !== null; slug span keeps min-w-0 truncate so a null phase leaves the header row untouched
- 2026-10-06 [agent] — 117/02: old uppercase span and Ceremony phase tooltip deleted from the right-side group; blocked badge then FeatureStatusSelect order preserved
- 2026-10-06 [agent] — 117/02: slug row is flex nowrap — slug min-w-0 truncate, chip shrink-0, so a long slug collapses before pushing the chip out
- 2026-10-06 [agent] — 117/02: gates green in web/ — eslint 0 errors (6 pre-existing warnings elsewhere), tsc clean, 257/257 tests, next build ok
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 1 OBSERVATION (Badge capitalize renders «Grooming» vs old uppercase — inherent to Badge reuse, noted for the human's visual tap). Spot-check 16/16. Commit 1133e30.
</log>