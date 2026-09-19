---
feature: 011
slug: side-menu-rework
status: validated
created: 2026-09-19
validated: 2026-09-19
---

# Spec 011 — Side menu rework (right panel consistency)

<decisions>
- 011 is ONE shell-rework feature: project tabs (UI-2), right-panel unification and the Planning → Dashboard rebrand ride together — they all touch the same app-shell surface (user decision 2026-09-19).
- Left sidebar becomes project picker + global sections: Projects (tree), Kits, Search, Settings. The project-scoped sections leave the global nav and become top tabs: Entries, Dashboard (user decision 2026-09-19).
- Right panel pattern = "slot everywhere + content contract": the shared InfoPanel renders on EVERY page (including /entries, the only page that lacks it today), each page fills it by the same recipe — an "X Overview" stats block + a contextual section (actions / help / tips). The panel hides only when there is genuinely nothing to show (user decision 2026-09-19).
- Rebrand is UI-ONLY: nav labels, page titles, panel heading, back links, and the route /planning → /dashboard with a redirect. MCP tools (planning_*), API routes and domain names are NOT touched — the Host↔MCP contract is additive-only and renames are forbidden by hard rule (user decision 2026-09-18).
- Tabs are project-scoped: with no active project they render disabled and the page shows the existing "select a project" state (user decision 2026-09-19).
</decisions>

<open-questions>
- none
</open-questions>

<requirements>
Navigation / app shell:
- Left sidebar (`widgets/app-shell/ui/app-sidebar.tsx`) drops Entries and Planning from the global NAV_ITEMS; remaining items: Projects (expandable tree, unchanged), Kits, Search, Settings. Mobile nav (`mobile-nav.tsx`) mirrors the same item set.
- New reusable tab bar component in `shared/ui` (no generic tabs exist today; the only tabs are hand-rolled inside feature-workspace — that one stays as is).
- Project tab bar renders at the top of the content area on project-scoped pages (/entries, /dashboard): tabs "Entries" and "Dashboard", active tab derived from the route, tab links carry the active project (`?project=<id>`, falling back to the last-active project id from tree-session, same resolution the sidebar uses today). With no resolvable project the tabs are disabled and the page shows its select-a-project state.
- Route rename: the planning page moves to /dashboard (same query params: project, feature, tab). /planning keeps an exported page that performs a client-side redirect to /dashboard preserving the full query string (static export has no server redirects; concrete mechanism is a plan-level detail).
- Rebrand of user-visible section strings: nav labels, page titles ("Dashboard — <project name>"), the right-panel heading ("Dashboard Overview"), the "← Planning board" back links in feature-workspace. Rule for secondary copy: strings that name the SECTION change; strings that name the planning workflow/domain (settings forms "Planning workflow", "Embedding & Planning", MCP/API-facing text) stay.

Right panel (InfoPanel):
- /entries gains panel content per the contract: "Entries Overview" stats (total, by type, recently updated) + contextual help, matching the shape of the other pages.
- Planning/dashboard panel no longer leaks into the no-project state: when no project is selected the panel renders nothing (today `useInfoPanel` is called before the early returns, so overview stats show next to the select-a-project prompt).
- Unchanged behavior: single shared slot in AppShell, per-page content via `useInfoPanel`, visible at xl+ only, collapsible with localStorage persistence.

Backend / contract:
- Zero backend change: no API, MCP, domain or EF work in this feature. Contract tests keep passing untouched.
</requirements>

<acceptance-ideas>
- Sidebar shows Projects/Kits/Search/Settings only; /entries and /dashboard render the tab bar with correct active tab; switching tabs keeps the same active project in the URL.
- Opening /planning?project=X&feature=Y&tab=spec lands on /dashboard with project/feature/tab preserved.
- /entries shows the right panel with Entries Overview; /dashboard without ?project shows the select-a-project state and NO stats panel.
- Every user-visible section label reads "Dashboard"; `planning_*` MCP tools and API routes unchanged (existing contract tests green).
- `npm run lint && npx tsc --noEmit` green; `npm run build` static export green incl. the /planning redirect page; `dotnet test Overmind.sln` untouched and green.
</acceptance-ideas>
