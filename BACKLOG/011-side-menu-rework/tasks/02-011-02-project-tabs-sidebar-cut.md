---
feature: 011
task: 02
slug: side-menu-rework
status: done
review: light
created: 2026-09-19
---

# 011/02 — Project tabs + sidebar cut

<goal>
Project-scoped pages (/entries, /dashboard) show a shared tab bar at the top of the content area with tabs "Entries" and "Dashboard" — active tab from the route, project carried in the URL with last-active fallback, disabled when no project is resolvable. The global left sidebar and mobile nav are cut to Projects (tree), Kits, Search, Settings. A generic Tabs component exists in shared/ui for reuse.
</goal>

<context>
Feature 011, second task. Task 01 already moved the planning page to /dashboard and retitled the nav items. Now the project-scoped sections leave the global nav and become top tabs (user decision 2026-09-19: tabs = Entries + Dashboard only). The executing subagent reads ONLY this file.

Current state (inventory 2026-09-19): AppShell (web/src/widgets/app-shell/ui/app-shell.tsx) composes AppSidebar (left, fixed w-60) → column with AppTopbar, MobileNav, DegradedBanner → flex row of <main> + <InfoPanel>. Sidebar nav items live in NAV_ITEMS (app-sidebar.tsx ~lines 42-49), duplicated in mobile-nav.tsx (~lines 13-20). Active project is NOT global state: pages read ?project= themselves; sidebar links suffix ?project=<getLastActiveProjectId()> from tree-session (app-sidebar.tsx ~lines 63-67). No generic tab component exists in shared/ui (only badge, button, empty-state, error-text, field, input, loading-state, modal, page-header, select, textarea, zerling). A hand-rolled tablist exists inside features/planning/components/feature-workspace.tsx (PLANNING_TABS) — it stays as is, but is a useful style reference.
</context>

<scope>
- web/src/shared/ui/tabs.tsx (new): generic, controlled-by-link Tabs component — role="tablist"/role="tab", aria-selected, keyboard arrow navigation per APG, active styling matching existing design tokens (cf. sidebar active item: border-primary bg-primary/10 text-primary; match the app's radius/typography). Export through the shared/ui barrel if one exists
- web/src/widgets/app-shell/ui/project-tabs.tsx (new): shell-level tab bar — renders ONLY on /entries and /dashboard (usePathname); tabs "Entries" → /entries, "Dashboard" → /dashboard; each tab link carries ?project=<id> where id = current ?project= searchParam, falling back to getLastActiveProjectId() (client-only read, same pattern the sidebar uses — no localStorage read during first paint/SSR); when no id resolves, tabs render disabled with a title hint ("Select a project first")
- app-shell.tsx: mount ProjectTabs above <main> (inside the content column, so it aligns with the page content, not the sidebar)
- app-sidebar.tsx NAV_ITEMS: remove Entries and Dashboard entries — remaining: Projects (expandable tree, unchanged), Kits, Search, Settings. Remove now-dead last-active suffix logic if it only served the removed items
- mobile-nav.tsx: mirror the same item set (Projects, Kits, Search, Settings)
</scope>

<anti-scope>
- No changes to /planning redirect, page titles, or info-panel content (tasks 01/03)
- No new empty/select-a-project state on the entries page — if entries has no no-project state today, it keeps its current behavior; the disabled tab bar is shell-level and sufficient
- No changes to feature-workspace's internal tablist (spec/plan/tasks/result) — different level of navigation
- No backend changes
</anti-scope>

<constraints>
- Next.js 16 static export, React 19, TS strict, Tailwind 4, FSD layering; no new npm packages
- usePathname/useSearchParams under Suspense where required by static export (mirror existing pages)
- Hydration: localStorage (getLastActiveProjectId) must not be read during SSR/first client paint
- Verify in web/: `npm run lint && npx tsc --noEmit && npm run build` — all green
</constraints>

<acceptance-criteria>
- [ ] /entries and /dashboard render the tab bar with the correct active tab; /, /kits, /search, /settings do NOT
- [ ] Clicking a tab navigates with ?project=<id> preserved (URL param or last-active fallback)
- [ ] With no resolvable project the tabs are visibly disabled and non-navigating
- [ ] Sidebar and mobile nav show exactly: Projects, Kits, Search, Settings
- [ ] shared/ui Tabs is generic (no project/entries coupling) and passes a11y roles/keyboard check
- [ ] npm run lint && npx tsc --noEmit && npm run build green
</acceptance-criteria>

<refs>
- @web/src/widgets/app-shell/ui/app-shell.tsx — shell composition
- @web/src/widgets/app-shell/ui/app-sidebar.tsx — NAV_ITEMS, active-item styling, last-active pattern
- @web/src/widgets/app-shell/lib/tree-session.ts — getLastActiveProjectId
- @web/src/features/planning/components/feature-workspace.tsx — existing hand-rolled tablist (style reference only)
- tasks/01-dashboard-route-rebrand.md — predecessor (route + labels)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-19 — Created from validated plan 011
- 2026-09-19 — Started: project tabs + sidebar cut (coder dispatched); 01 moved to review
- 2026-09-19 — Coder done: shared/ui Tabs + ProjectTabs + sidebar cut; lint/tsc/build green; light review dispatched
- 2026-09-19 — Light review PASS-WITH-NITS (0 blocker, 0 important, 6 nit), audits/review-011-02.md; NIT-1 (empty ?project=) fixed in-tree
</log>
