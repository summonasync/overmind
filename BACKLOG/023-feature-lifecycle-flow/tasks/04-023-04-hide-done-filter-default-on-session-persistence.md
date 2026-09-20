---
feature: 023
task: 04
slug: feature-lifecycle-flow
status: done
review: light
created: 2026-09-20
---

# 023/04 — Hide-done filter (default ON) + session persistence

<goal>
The board hides done features by default; a toggle in the filter bar controls it; the full filter object persists across project switches within the session (sessionStorage), restoring on mount when the URL carries no filter params.
</goal>

<context>
Feature 023, web half 2. Depends on task 03 (review is a valid filter status). Filter model today: web/src/features/planning/lib/board-filters.ts (PlanningBoardFilters, EMPTY_BOARD_FILTERS, parseBoardFilters drops unknown statuses, filterBoardItems) + use-board-filters.ts (URL params + popstate + custom event; setFilters writes status/spec/plan/current via history.replaceState).
</context>

<scope>
- board-filters.ts — model gains hideDone (default true in EMPTY_BOARD_FILTERS); filterBoardItems skips done when hideDone; parseBoardFilters reads hidedone=0 as false (absent → true; the URL records deviations only); review joins the valid status values.
- use-board-filters.ts — setFilters writes the hidedone deviation param; persistence: write the full filter object to sessionStorage key "overmind.board.filters" on every setFilters (tree-session.ts readJson/writeJson helpers); on mount, when the URL has NO filter params (status/spec/plan/current/hidedone absent), initialize from sessionStorage when present.
- board-filter-bar.tsx — "Hide done" toggle next to "Current only", reflecting hideDone.
- Verify: npm run lint && npx tsc --noEmit && npm run build.
</scope>

<anti-scope>
- No localStorage (session scope per the feature body — the decision is recorded in the spec), no per-project filter memory, no backend.
</anti-scope>

<constraints>
- URL remains the shareable source when present; session storage only fills the blank-URL case.
- TypeScript strict; English-only.
</constraints>

<acceptance-criteria>
- [ ] Fresh board (no URL params) hides done features; the 15 done features disappear until toggled.
- [ ] hidedone=0 in URL shows done; the toggle reflects it.
- [ ] Filters (incl. hideDone) survive switching to another project's board and back within the session.
- [ ] lint + tsc + build green.
</acceptance-criteria>

<refs>
- Spec: feature 023 — filter decisions (default ON, URL deviations, sessionStorage).
- Pattern refs: board-filters.ts, use-board-filters.ts:18–72, board-filter-bar.tsx:101–109 (Current only toggle), features/projects/lib/tree-session.ts (readJson/writeJson precedent).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Execution started — next-js-coder subagent (03 code in tree; review of 03 running in parallel).
- 2026-09-20 — Coder done: hideDone default ON (EMPTY_BOARD_FILTERS), hidedone=0 deviation param, statuses-selection-wins semantics (hideDone applies only when statuses empty — recorded decision), filter object persisted to sessionStorage overmind.board.filters with restore-on-blank-URL via rAF idiom; coerceBoardFilters guards stale storage; Hide-done chip (aria-pressed); readJson/writeJson extracted to shared/lib/session-json.ts (FSD — features may not import features); dead fast-path removed. lint 0 / tsc 0 / build green. To review.
- 2026-09-20 — Review PASS (audits/review-023-04.md): 0 blockers, 3 INFO (back-nav resets filters — URL-wins by design; stale storage shape restores defaults silently; hide-done chip can read ON while done visible under statuses-wins — polish candidate). Statuses-wins verified as the only sane composition; restore fires only on fully-blank URL (5-param check incl. hidedone); persistence across projects confirmed via the MPA full-page-load model; FSD extraction closes the documented precedent instead of adding one. Done.
</log>
