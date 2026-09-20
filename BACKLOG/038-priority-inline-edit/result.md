---
feature: 038
slug: priority-inline-edit
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 038 — Priority inline edit: селект прио на карточке и в хедере фичи

<summary>
Feature 038 done: priority is editable inline in one click on both surfaces — the board card's priority chip became a chip-toned mini-select, and the feature workspace header gained a priority select beside the status select. Shared FeaturePrioritySelect component with its own mutation (022 pattern: cancelQueries, dual cache snapshot, optimistic update, rollback with undefined-guards, onSettled invalidation, disabled while pending — on both surfaces). The board re-sorts immediately on change. Tone classes extracted to a shared source (select-tone-classes.ts) so status and priority selects share one supplier — byte-identical for the status select.
</summary>

<tasks>
01 — Inline priority select (single task): shared component + board card select (size sm, chip tones) + workspace header select + shared tone-classes lib. Review PASS (audits/review-038-01.md), gates green.
</tasks>

<evidence>
- Reviewer verified all 7 claims point-by-point against the status-select precedent; the 022 header pending-disable asymmetry deliberately not repeated.
- PUT payload is priority-only (other fields null = untouched, backend null-guard from 034/01).
- Instant re-sort confirmed: optimistic patch hits planningKeys.board, per-render composite sort, no memoization in the path.
- Gates: lint 0 errors, tsc clean, next build 10/10 static pages.
</evidence>

<deviations>
- None. INFO notes from review (raw select styling choice in header; aria-label style divergence inherited from 022) accepted, no action.
</deviations>

<follow-ups>
- Microbatch candidate: aria-label style divergence in workspace header (status without title, priority with) — 022 legacy.
</follow-ups>

<log>
</log>