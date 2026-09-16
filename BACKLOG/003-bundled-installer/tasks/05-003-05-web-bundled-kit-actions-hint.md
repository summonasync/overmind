---
feature: 003
task: 05
slug: bundled-installer
status: done
review: light
created: 2026-09-16
---

# 003/05 — Web: bundled kit actions + hint

<goal>
Bundled kits in the kits UI show their origin badge (already exists) but no Re-import and no Clear-source actions, plus a one-line hint that editing forks the kit into a personal copy. Everything else about bundled kits stays indistinguishable from other kits.
</goal>

<context>
Spec D6/R8: refresh for bundled kits happens automatically at distribution upgrade (task 02), and clear-source is refused server-side (task 01) — the UI must not offer dead-end actions. The edit-fork conversion (task 03) flips the badge via data, no UI work needed for the flip itself. Executor reads ONLY this file.
</context>

<scope>
- `web/src/widgets/kit-workspace/ui/kit-source-section.tsx` — hide Re-import and Clear-source controls when `sourceKind === 'bundled'`; render the hint line ("Edits fork this kit into your personal copy") in the Source section
- Kits list origin badge: no changes expected (bundled case exists); verify no bundled-specific dead actions render elsewhere (grep for re-import/clear-source usages)
- Follow the house rules: React Query, FSD, strict TS, existing house components
</scope>

<anti-scope>
- No backend changes; no new endpoints or DTO changes
- No redesign of the kits screens
- No `.omkit` (002b) or 003b work
</anti-scope>

<constraints>
- `npm run lint && npx tsc --noEmit && npm run build` green in `web/` (0 lint errors; known warnings may remain)
- TypeScript strict, no `any`; no comments
- No new npm packages; no auto-commits
</constraints>

<acceptance-criteria>
- [x] Bundled kit detail: no Re-import, no Clear-source, hint line visible
- [x] Imported/manual kits: actions unchanged
- [x] lint + tsc + build green
</acceptance-criteria>

<refs>
- @../spec.md — D6, R8 (reviewer reads, not executor)
- @../plan.md — task 05 row
- tasks/03-edit-fork.md — the behavior the hint describes
</refs>

<obstacles>
</obstacles>

<log>
2026-09-16 — `web/src/widgets/kit-workspace/ui/kit-source-section.tsx`: added the hint line ("Edits fork this kit into your personal copy.") under the origin line for `sourceKind === 'bundled'` in the non-imported early-return branch. Re-import/Clear-source were already gated behind `sourceKind === 'imported'` — no dead actions reach bundled kits; verified via grep that re-import/clear-source usages elsewhere (import wizard, candidate row, use-kit-import, reimport report) are import-flow machinery, not kit-detail actions. Kit list origin badge already renders the bundled case; no changes needed. Verification: `npm run lint` — 0 errors, 6 pre-existing warnings (img/location-assign in unrelated files); `npx tsc --noEmit` — clean, exit 0; `npm run build` — compiled successfully, 9/9 static pages.
</log>
