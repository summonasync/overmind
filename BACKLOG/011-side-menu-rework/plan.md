---
feature: 011
slug: side-menu-rework
status: validated
created: 2026-09-19
validated: 2026-09-19
---

# Plan 011 — Side menu rework (right panel consistency)

<approach>
Route-first, then shell, then panel, then live closeout. The /dashboard route and the string rebrand land first while the sidebar still links everything (no dead navigation window); the tab bar and the sidebar cut land together in one task so Entries/Dashboard stay reachable throughout. Pure-web feature — zero backend work, contract tests untouched. The /planning redirect is a client-side page (static export has no server redirects), mirroring how existing pages already wrap useSearchParams in Suspense.
</approach>

<tasks>
- [ ] 01-dashboard-route-rebrand — move the planning page to /dashboard (same query params: project, feature, tab); /planning becomes an exported client page redirecting to /dashboard with the full query string preserved; sidebar + mobile-nav item retitled "Dashboard" pointing at /dashboard; page titles ("Dashboard — <project>"), panel heading ("Dashboard Overview"), "← Planning board" back links and every internal link builder targeting /planning updated (grep the whole web/src); section-naming strings change, workflow/domain strings (settings "Planning workflow", "Embedding & Planning") stay → tasks/01-dashboard-route-rebrand.md. Verify: npm run lint && npx tsc --noEmit && npm run build; manual /planning?project=X&feature=Y&tab=spec redirect check
- [ ] 02-project-tabs-shell — generic Tabs component in shared/ui (tablist/tab roles, keyboard arrows, active state matching existing design tokens); ProjectTabs bar in app-shell rendered above content on /entries and /dashboard only: active tab from usePathname, links carry ?project=<id> resolved from URL searchParams with getLastActiveProjectId() fallback (client-only read, no hydration mismatch — same pattern the sidebar uses), disabled + title hint when no project resolvable; sidebar and mobile-nav drop Entries and Dashboard items (remaining: Projects tree, Kits, Search, Settings) → tasks/02-project-tabs-shell.md. Verify: npm run lint && npx tsc --noEmit && npm run build
- [ ] 03-info-panel-contract — new entries-info-panel.tsx per the contract: "Entries Overview" stats (total, by type, recently updated) + contextual help section, wired via useInfoPanel on /entries; fix the dashboard panel no-project leak (panel content set only after a project is resolved — today useInfoPanel runs before the early returns); align panel section markup across pages onto one shared section pattern (heading + body) so the recipe is structural, not copy-paste → tasks/03-info-panel-contract.md. Verify: npm run lint && npx tsc --noEmit && npm run build
- [ ] 04-live-dogfood-closeout — full gates (lint + tsc + static export build; dotnet test Overmind.sln untouched-green), deploy web/out to the live install (rsync ritual), live click-through: tabs navigation keeps project, /planning redirect with query, entries panel present, no-project states clean, labels read Dashboard; then materialize BACKLOG, write the result artifact, close the feature → tasks/04-live-dogfood-closeout.md. Verify: live scenario + dotnet test Overmind.sln
</tasks>

<risks>
- useSearchParams in statically exported pages requires a Suspense boundary — the redirect page and ProjectTabs must mirror the pattern existing pages already use, or `npm run build` fails at export.
- Query preservation on redirect: pass through searchParams verbatim (do not whitelist) so future params survive; loop-guard not needed (target is a different route).
- getLastActiveProjectId reads localStorage — client-only; reading it during SSR/first paint causes hydration mismatch. Sidebar already solves this; reuse its approach, do not invent a new one.
- Internal link builders (feature-workspace ?feature&tab URLs, board card links, back links) are easy to miss — the grep for "/planning" in task 01 is the guard; the closeout live click-through is the net.
- Entries page currently has no select-a-project empty state like planning has (verify in task 02; if missing, tabs disabled state still works because the tab bar is shell-level, and the page keeps its current behavior — do not scope-creep a new empty state unless it already exists).
</risks>

<verification>
- npm run lint && npx tsc --noEmit && npm run build in web/ — green after every task
- dotnet build Overmind.sln && dotnet test Overmind.sln — untouched, green at closeout
- Live: sidebar shows Projects/Kits/Search/Settings only; /entries and /dashboard show the tab bar with correct active state and project carried in the URL; /planning?project=X&feature=Y&tab=spec lands on /dashboard with params intact; /entries has an Overview panel; /dashboard without project shows select-a-project with no stats panel; all section labels read "Dashboard"
- BACKLOG/011-side-menu-rework materializes clean
</verification>
