---
feature: 011
task: 03
slug: side-menu-rework
status: done
review: light
created: 2026-09-19
---

# 011/03 — InfoPanel content contract

<goal>
The right InfoPanel follows one content contract on every page: an "X Overview" stats block plus a contextual section. /entries gains its missing panel (Entries Overview), the dashboard panel no longer leaks stats into the no-project state, and panel sections share one structural pattern instead of per-page markup drift.
</goal>

<context>
Feature 011, third task (user decision 2026-09-19: "slot everywhere + content contract"). The executing subagent reads ONLY this file.

Current state (inventory 2026-09-19): InfoPanel (web/src/widgets/app-shell/ui/info-panel.tsx) is a single shared slot in AppShell; pages inject content via useInfoPanel(<XxxInfoPanel />) from web/src/widgets/app-shell/lib/info-panel-context.tsx. Behavior (xl+ only, collapsible, localStorage persist) is uniform and STAYS. Per-page content today: projects (stat cards + recent list), planning/dashboard (2×2 stat grid + status breakdown + ceremony explainer — and it leaks: useInfoPanel is called BEFORE the early returns in planning-page.tsx, so stats render next to the select-a-project prompt), kits (stats + drift text), search (static tips), settings (help text). /entries has NO panel — entries-page.tsx never calls useInfoPanel.
</context>

<scope>
- web/src/pages/entries-page/ui/entries-info-panel.tsx (new): "Entries Overview" — stats (total entries, breakdown by entry type, recently updated) + a contextual help/tips section matching the tone of the other panels; wire it in entries-page.tsx via useInfoPanel. Use the entries queries/data the page already has (React Query cache) — no new API endpoints; if a count needs data the page doesn't load, derive from the loaded list, do NOT add backend calls
- Fix the dashboard panel leak (web/src/pages/planning-page/ui/planning-page.tsx + planning-info-panel.tsx): panel content is set ONLY after a project is resolved — no-project state shows no stats panel (panel renders nothing)
- Shared section pattern: introduce one small presentational component for panel sections (heading + body, e.g. widgets/app-shell/ui/info-panel-section.tsx) and migrate the existing per-page panels onto it (projects, planning, kits, search, settings + the new entries one) so the recipe is structural
</scope>

<anti-scope>
- No changes to InfoPanel shell behavior (width, xl+ visibility, collapse, localStorage) — uniform and correct today
- No new backend endpoints or query keys beyond reusing existing entries queries
- No tab bar / sidebar / route changes (tasks 01/02)
- No redesign of panel visuals beyond aligning on the shared section component
</anti-scope>

<constraints>
- Next.js 16 static export, React 19, TS strict, Tailwind 4, FSD layering; no new npm packages
- No useEffect data fetching — panel stats come from React Query data the page already fetches
- Verify in web/: `npm run lint && npx tsc --noEmit && npm run build` — all green
</constraints>

<acceptance-criteria>
- [ ] /entries shows the right panel with Entries Overview (total, by type, recent) + help section
- [ ] /dashboard without ?project shows the select-a-project state and NO stats in the right panel
- [ ] /dashboard with a project shows Dashboard Overview exactly as before (no regression)
- [ ] All six page panels render through the shared section component
- [ ] npm run lint && npx tsc --noEmit && npm run build green
</acceptance-criteria>

<refs>
- @web/src/widgets/app-shell/ui/info-panel.tsx — shared slot
- @web/src/widgets/app-shell/lib/info-panel-context.tsx — useInfoPanel contract
- @web/src/pages/planning-page/ui/planning-page.tsx — leak site (useInfoPanel before early returns)
- @web/src/pages/planning-page/ui/planning-info-panel.tsx — richest existing panel (pattern reference)
- @web/src/pages/entries-page/ui/entries-page.tsx — wiring point + available data
- tasks/02-project-tabs-shell.md — independent, same feature
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-19 — Created from validated plan 011
- 2026-09-19 — Started: InfoPanel content contract (coder dispatched)
- 2026-09-19 — Coder done: entries panel + leak fix + InfoPanelSection/Stat shared pattern; lint/tsc/build green; light review dispatched
- 2026-09-19 — Light review PASS-WITH-NITS (0 blocker, 1 important non-blocker, 3 nit), audits/review-011-03.md; IMPORTANT (overview stats over filtered list) recorded to BACKLOG Deferred
</log>
