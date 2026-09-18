---
feature: 010
task: 04
slug: planning-board-filters
status: done
review: light
created: 2026-09-18
---

# 010/04 — Web: board filter bar

<goal>
The planning board has a filter bar: by feature status (multi), by spec/plan chip state, and a "current only" toggle. Filters compose (AND), live in URL search params, and a zero-match state offers a reset.
</goal>

<context>
User ask from dogfooding: as the board grows (10+ features) it needs filters. Backend is untouched — the board endpoint already returns all features with status/specStatus/planStatus/isCurrent; filtering is client-side (spec decision). The page already uses useSearchParams for `project` and `feature` — those two names are RESERVED. The executing subagent reads ONLY this file.
</context>

<scope>
- New component in web/src/features/planning/components/ (e.g. board-filter-bar.tsx):
  * feature status multi-select: draft / ready / inProgress / done (check the PlanningFeatureStatus union in types.ts for exact literals)
  * per-artifact filters for spec and plan: any | validated | not validated | none ("not validated" = artifact exists but status is not validated; "none" = no such artifact)
  * "current only" toggle
  * a clear-filters control, visible when any filter is active
- Filter state in URL search params: `status` (comma-separated), `spec`, `plan`, `current=1`. Read/write via useSearchParams + router.replace (no page reload, scroll:false). Empty/absent params = no filtering = today's behavior
- Filtering + composition logic in a small pure helper (features/planning/lib/) so it is trivially reviewable
- web/src/pages/planning-page/ui/planning-page.tsx — render the bar above FeatureBoard (board view only; when `feature` param selects the workspace, the bar is not shown and filter params are ignored — do not strip them)
- Zero matches → empty state (reuse EmptyState) with a "Clear filters" action
</scope>

<anti-scope>
- No backend/API changes
- No persistence beyond URL params (no localStorage, no server prefs)
- No changes to card content (pin/accent was task 03)
</anti-scope>

<constraints>
- TS strict, no any; Tailwind 4 utilities consistent with the existing bar/button styling
- No new npm packages
- Verify in web/: `npm run lint && npx tsc --noEmit` and `npm run build` — green
</constraints>

<acceptance-criteria>
- [ ] Status multi-select + spec/plan filters + current-only compose with AND
- [ ] Reload with params in the URL restores the identical filtered view
- [ ] No filters → byte-identical board behavior to before
- [ ] Zero-match shows empty state with working reset
- [ ] Workspace view (?feature=...) unaffected by filter params
- [ ] lint + tsc + build green
</acceptance-criteria>

<refs>
- @web/src/pages/planning-page/ui/planning-page.tsx — existing searchParams usage
- @web/src/features/planning/components/feature-board.tsx — receives the filtered list
- tasks/03-web-current-pin.md — isCurrent on the item type (done)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-18 — Coder done: filter bar + URL params + empty state; awaiting light review
- 2026-09-18 — Done: filter bar + URL params + zero-match reset; review PASS; WARNING-1 (useRouter on static export) deferred to 010/05 live runtime check
</log>
