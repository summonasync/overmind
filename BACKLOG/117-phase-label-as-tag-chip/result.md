---
feature: 117
slug: phase-label-as-tag-chip
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Result 117 — Board card: phase label as tag chip after the slug

> Both tasks done and approved; feature auto-promoted to review. Commits 2f4cac2 (task 01), 1133e30 (task 02).

## Shipped

On the board card, the ceremony phase label left the right-side header group (where it sat as plain uppercase text between the blocked badge and the status select) and now renders as a shared `Badge` chip — default neutral tone — on the slug line, immediately after the slug, inside the header link. The old `text-[10px] uppercase` span and its `Ceremony phase: …` tooltip are removed.

## Design decisions (flag round)

- **Neutral chip for every phase, no phase→color mapping** — the request prescribed none; a mapping can come later as its own groomed change.
- **Tooltip dropped** — the chip text is self-describing, like every other chip on the card.
- **Null safety is web-side only:** the server contract (`CeremonyPhasePlanner.DeriveLabel`, 091) always returns one of the seven labels — closed set pinned by tests — so `PlanningFeatureBoardItem.phase: string | null` and the null-safe `cardPhaseLabel` are defensive web typing; a null/absent/blank phase renders no chip at all, no empty chip box, no layout shift.
- Reused `shared/ui/badge` — no new chip variant, no claim-chip-style span copy.

## Verification

- Gates green in `web/`: `npm run lint`, `npx tsc --noEmit`, `npm test` (257 passing), `npm run build`.
- Light reviews ×2, 0 blockers (tasks 01, 02); spec-compliance checked against the validated spec AC 1–6.
- Mutation-checked pins: `board-card-context.test.mts` extended (null / `""` / `"   "` → null, seven-label passthrough) and verified by mutating the guard.

## For the human (visual tap)

- Chip rendering on the dashboard board card: `capitalize` (Badge default, same as «Spec Draft» chips) vs the old all-uppercase label — confirm the look sits right with the version/spec/plan chips.
- Long-slug card: slug still truncates, chip visible, single line.

<log>
- 2026-10-06 [agent] — result drafted: 01 (2f4cac2) + 02 (1133e30) approved, gates green, reviews clean; human visual tap pending (capitalize vs uppercase, truncation).
</log>