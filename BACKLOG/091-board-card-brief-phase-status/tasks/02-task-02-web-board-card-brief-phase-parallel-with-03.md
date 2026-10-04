---
feature: 091
task: 02
slug: board-card-brief-phase-status
status: done
review: light
created: 2026-10-03
depends_on: 01
brief: Card renders brief + phase label from the new payload fields, presence tests
---

# Task 02 — Web board card: brief + phase (parallel with 03)

<goal>
Card renders the ceremony context: brief between priority/version row and task counter, phase label next to the status column.
</goal>

<scope>
- web feature board card components: brief line (from the new payload fields) rendered between the priority/version row and the task counter; phase label rendered next to the status column — consistent tone/styling with existing chips.
- Done cards follow the existing done treatment (phase shows done or is suppressed — match the current done-card look, do not invent a second representation).
- Presence tests: brief renders when present, absent when null; phase label renders per state; card without body renders without brief line.
</scope>

<anti-scope>
No queue work (task 03), no backend changes, no new packages.
</anti-scope>

<constraints>
FSD, English UI, no comments by default, no auto-commits; gates lint/tsc/build/test real numbers.
</constraints>

<acceptance-criteria>
- [x] lint + tsc + build + test green
- [x] Brief renders between priority/version row and task counter when present; absent when null
- [x] Phase label renders per state; done cards match existing done treatment
- [x] Presence tests pin the three render branches
</acceptance-criteria>

<refs>
- Spec: d066ec12 (Surfaces).
- Task 01 (order 1) — payload fields.
</refs>

<obstacles>
- Done-card treatment finding: the existing done card has NO special visual treatment (status select just disables via empty transition set; card renders normally) — so the phase label renders "done" like on any card, pinned as passthrough in cardPhaseLabel (no suppression branch invented). Matches the reviewer precedent by construction.
- Phase styling is deliberately NOT a chip: plain uppercase micro-label (text-[10px] uppercase tracking-wide text-muted, title carries the full sentence) so it cannot read as a second lifecycle status next to the status select.
- cardBriefLine defensively trims and null-checks the ready-made excerpt (null/""/whitespace → no line) — the front performs zero phase/brief derivation, both helpers are contract pins, not logic.
</obstacles>

<log>
- 2026-10-03 [agent] — dispatched to web-coder (same session as 101 wave — house card patterns)
- 2026-10-03 [agent] — implemented: PlanningFeatureBoardItem + briefExcerpt (string|null) and phase (string) in web types; feature-board card renders briefExcerpt as a muted one-liner between the priority/version row and the task counter (cardBriefLine: trim+null-check — null/blank → nothing, no placeholders), and the ready-made phase as an uppercase micro-label next to the status select (deliberately not a chip so it can't read as a second lifecycle status; title carries the full sentence). Done cards: existing done treatment is "no special visual" (select just disables), so phase renders "done" like any card — pinned as passthrough, no second done representation. 5 node tests pin the branches (present/null/blank excerpt, phase passthrough incl. done). Front performs zero derivation — helpers are contract pins. Gates: lint 0 errors / 6 pre-existing warnings, tsc 0, tests 67/67 (62+5), build success.
- 2026-10-03 [agent] — task 02 complete — 4/4 AC ticked, gates green (lint 0e/6 pre-existing w, tsc 0, tests 67/67, build ok); zero front-side derivation, done-card passthrough pinned, phase micro-label styled distinct from the status select; queue surface untouched (task 03 lane). Status review.
- 2026-10-03 [agent] — 2026-10-04 [agent] — reviewed by web-code-reviewer: PASS-WITH-NITS — hierarchy per spec (brief text-xs muted between priority/version and task counter, null → nothing; phase uppercase micro-label text-[10px] by the status select with full-sentence title), contract matches backend 1:1 (Phase/BriefExcerpt), null-semantics robust (trim + optional chaining handles undefined old payloads), no scope creep; 2 micro-nits → microbatch (undefined-branch test pin, DONE micro-label double-representation — visual veto available); report audits/review-091-02-web.md.
- 2026-10-03 [agent] — delta review PASS-WITH-NITS (audits/review-091-02-web.md): 0 blockers, micro-nits to microbatch; AC 5/5 (coder ticked). Closing as done.
</log>