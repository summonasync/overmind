---
feature: 010
task: 06
slug: planning-board-filters
status: done
review: light
created: 2026-09-18
---

# 010/06 — Fix: board filters dead on static export (useRouter no-op)

<goal>
Board filters actually work on the statically exported UI: clicking a chip/select/toggle immediately re-renders the filtered board, and the URL still reflects the filter state (shareable, survives reload).
</goal>

<context>
Live dogfood of 0.1.2 (2026-09-18): user clicks status chips — nothing happens. Root cause confirmed by code inspection: web/src/features/planning/lib/use-board-filters.ts derives filters purely from useSearchParams() and writes them via router.replace from next/navigation. On output:"export" the Next client router is broken upstream (recorded in STRUCTURE.md as a known limitation; useRouter effectively no-ops), so searchParams never change and the view never updates. Review WARNING-1 of task 04 predicted exactly this. The executing subagent reads ONLY this file.
</context>

<scope>
- web/src/features/planning/lib/use-board-filters.ts — rework the hook: filters live in React state (useState initialized from the initial useSearchParams read — reload-with-params must still work); writes go through window.history.replaceState(null, "", url) so the URL stays shareable without touching the broken client router; a popstate listener (browser API useEffect — allowed) re-syncs state on back/forward
- Keep the public hook shape ({ filters, isActive, setFilters, clearFilters }) — board-filter-bar.tsx and planning-page.tsx should need NO changes; if a small prop change is unavoidable, adjust both call sites
- The hardcoded "/planning" path when composing the URL: keep behavior, but build it from window.location.pathname instead of a literal
</scope>

<anti-scope>
- No backend changes
- No redesign of the bar or filter semantics
- No attempt to "fix" the Next client router globally; do not remove the STRUCTURE.md note — update it only to record that board filters use history.replaceState deliberately
</anti-scope>

<constraints>
- No useRouter anywhere in the planning slice after this fix (grep must confirm)
- TS strict, no any, no new packages
- Verify in web/: npm run lint && npx tsc --noEmit && npm run build — green
- Runtime verification is done by the orchestrator after deploy (static export has no dev-server fidelity for this), but do a smoke via npm run build output serving if trivially possible
</constraints>

<acceptance-criteria>
- [ ] Chip click re-renders the filtered board instantly (state-driven)
- [ ] URL updates via replaceState; reload with params restores the same view (initial parse intact)
- [ ] Back/forward (popstate) re-syncs the filters
- [ ] Reserved params project/feature preserved on every write
- [ ] grep -r "useRouter" web/src/features/planning → empty
- [ ] lint + tsc + build green
</acceptance-criteria>

<refs>
- @web/src/features/planning/lib/board-filters.ts — parseBoardFilters/EMPTY_BOARD_FILTERS (reuse, do not change semantics)
- @web/src/features/planning/components/board-filter-bar.tsx — sole consumer of the hook besides the page
- @STRUCTURE.md — the static-export router limitation note
- tasks/04-010-04-web-board-filter-bar.md — original implementation (review WARNING-1)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-18 — Bug filed from live dogfood 0.1.2: filter chips dead (useRouter no-op on static export)
- 2026-09-18 — Started: replacing useRouter with local state + history.replaceState
- 2026-09-18 — Done: useRouter out, local state + replaceState + board-filters:change sync event; re-review PASS; user confirmed runtime fix works
</log>
