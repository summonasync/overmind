---
feature: 011
task: 01
slug: side-menu-rework
status: done
review: light
created: 2026-09-19
---

# 011/01 — Dashboard route + UI rebrand

<goal>
The planning page lives at /dashboard with all its query params (project, feature, tab), /planning is an exported client-side redirect that preserves the full query string, and every user-visible SECTION string reads "Dashboard". All navigation still works after this task — the sidebar keeps its (retitled) Entries/Dashboard items; the sidebar cut is task 02.
</goal>

<context>
Feature 011 is a pure-web shell rework (spec/plan validated 2026-09-19): project tabs + right-panel unification + Planning → Dashboard rebrand in one feature. This task lands the route move and the string rebrand first so later tasks have a stable target. The rebrand is UI-ONLY by hard rule: MCP tools planning_*, API routes and domain names are NOT touched (Host↔MCP contract is additive-only). The executing subagent reads ONLY this file.

Current state (inventory 2026-09-19): routes are re-export shims — web/src/app/planning/page.page.tsx re-exports pages/planning-page/ui/planning-page.tsx (custom pageExtensions "page.page.tsx"). Query scoping is param-based: ?project=, and on the planning page also ?feature=&tab=. Static export (output: "export") — no server redirects exist.
</context>

<scope>
- Move the route: web/src/app/dashboard/page.page.tsx re-exporting the existing planning page component; delete the old planning page shim's page content (the route folder stays for the redirect, see below)
- web/src/app/planning/page.page.tsx — replace with a small client component that redirects to /dashboard preserving the FULL query string verbatim (useSearchParams().toString() passthrough, no param whitelist; router.replace, not push). Static export requires a Suspense boundary around useSearchParams — mirror the pattern existing pages (e.g. planning-page) already use
- String rebrand (section-naming strings ONLY):
  - web/src/widgets/app-shell/ui/app-sidebar.tsx (~line 45) and mobile-nav.tsx (~line 16): nav label "Planning" → "Dashboard", href → /dashboard (keep the ?project= last-active suffix logic as is)
  - web/src/pages/planning-page/ui/planning-page.tsx: title="Planning" (~line 88) → "Dashboard"; `Planning — ${project.name}` (~line 113) → `Dashboard — ${project.name}`
  - web/src/pages/planning-page/ui/planning-info-panel.tsx (~line 27): "Planning Overview" → "Dashboard Overview"
  - web/src/features/planning/components/feature-workspace.tsx (~lines 96, 132): "← Planning board" → "← Dashboard" (both render branches)
  - Every internal link builder targeting the /planning route (board cards, feature-workspace ?feature&tab URLs, back links): grep -rn '"/planning' and "'/planning" web/src and update to /dashboard, query-building logic unchanged
</scope>

<anti-scope>
- No backend/API/MCP/domain changes, no renames of anything named planning outside user-visible section strings and the UI route
- No sidebar item removal (task 02), no tab bar (task 02), no info-panel content changes beyond the heading string (task 03)
- Workflow/domain strings STAY: settings-page "Embedding & Planning", settings-form/workflow-settings "Planning workflow", settings-help-panel copy, projects-page "Planning workflow" modal, PLANNING_DESCRIPTION copy in planning-page (no literal section name)
- Do not rename pages/planning-page or features/planning directories, hooks, types, or query keys — code/domain names stay
</anti-scope>

<constraints>
- Next.js 16 static export, React 19, TS strict, Tailwind 4, FSD layering (app → pages → widgets/features/shared); no new npm packages
- useSearchParams in a statically exported page must sit under a Suspense boundary or `npm run build` fails at export
- Redirect preserves unknown/future params verbatim; no redirect loop possible (different target route)
- Verify in web/: `npm run lint && npx tsc --noEmit && npm run build` — all green
</constraints>

<acceptance-criteria>
- [ ] /dashboard renders the planning board page; ?project/?feature/?tab behave exactly as before on it
- [ ] /planning?project=X&feature=Y&tab=spec client-redirects to /dashboard?project=X&feature=Y&tab=spec (params intact, replace not push)
- [ ] Sidebar and mobile nav show "Dashboard" pointing at /dashboard; page title and panel heading read Dashboard; back links read "← Dashboard"
- [ ] grep finds no remaining internal href to the /planning route except the redirect page itself
- [ ] grep confirms workflow strings (settings, projects modal, PLANNING_DESCRIPTION) unchanged
- [ ] npm run lint && npx tsc --noEmit && npm run build green
</acceptance-criteria>

<refs>
- @web/src/app/planning/page.page.tsx — current route shim
- @web/src/pages/planning-page/ui/planning-page.tsx — Suspense/useSearchParams pattern to mirror
- @web/src/widgets/app-shell/ui/app-sidebar.tsx — nav items + last-active project link logic
- tasks/02-project-tabs-shell.md — depends on this task (tab bar targets /dashboard)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-19 — Created from validated plan 011
- 2026-09-19 — Started: dashboard route + UI rebrand (coder dispatched)
- 2026-09-19 — Coder done, lint+tsc+build green; light review dispatched
- 2026-09-19 — Light review PASS-WITH-NITS (0 blocker, 0 important, 3 nit), audits/review-011-01.md; runtime redirect test green
</log>
