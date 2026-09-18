---
feature: 010
task: 03
slug: planning-board-filters
status: done
review: light
created: 2026-09-18
---

# 010/03 — Web: current pin on the feature board

<goal>
A user can pin/unpin the current feature from the board card; the current card is visually accented and always sorts first.
</goal>

<context>
Backend (tasks 010/01-02) exposes POST /api/projects/{projectId}/planning/features/{slug}/current and the board item now carries `isCurrent: boolean`. This task wires it into the web UI. Stack: Next.js 16 static export, React 19, TS strict, Tailwind 4, React Query 5, FSD layering. The executing subagent reads ONLY this file.
</context>

<scope>
- web/src/features/planning/types.ts — `isCurrent: boolean` on PlanningFeatureBoardItem
- web/src/features/planning/api.ts — `setPlanningFeatureCurrent(projectId, slug, isCurrent)` via apiPost
- The mutations hook used by the planning page (find usePlanningMutations — likely web/src/features/planning/lib/ or components/) — add a setCurrent mutation that invalidates planningKeys.board(projectId)
- web/src/features/planning/components/feature-board.tsx —
  * pin toggle button on each card (lucide Pin / PinOff, ghost variant like Edit/Delete), aria-pressed reflects state
  * current card accent: a "current" Badge (use an existing tone that reads as active — check Badge tones) + a distinct border class (e.g. border-primary)
  * render order: current feature first, then the rest in the incoming order (sort a COPY of the props array)
</scope>

<anti-scope>
- No filter bar (task 04)
- No backend changes
- No renames of existing UI text/routes (the Planning → Dashboard relabel belongs to feature 011)
</anti-scope>

<constraints>
- No new npm packages; lucide-react is already in use
- FSD: everything inside features/planning slice; exported via the slice's index.ts only if consumed outside
- Verify in web/: `npm run lint && npx tsc --noEmit` and `npm run build` — all green
</constraints>

<acceptance-criteria>
- [ ] Pin button toggles via the API and the board reflects it after invalidation (no manual reload)
- [ ] Current card visibly accented and first in the grid
- [ ] Only one card can be current (backend invariant; UI just renders it)
- [ ] lint + tsc + build green
</acceptance-criteria>

<refs>
- @web/src/pages/planning-page/ui/planning-page.tsx — how board + mutations are wired today
- @web/src/features/planning/queries.ts — planningKeys.board invalidation key
- tasks/02-current-api-mcp.md — the API shape (done)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-18 — Done: pin button + accent + current-first sort; lint/tsc/build green; light review PASS (2 NIT: cn() duplication in feature-board, sticky mutation error banner)
</log>
