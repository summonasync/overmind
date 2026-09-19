---
feature: 011
slug: side-menu-rework
status: validated
created: 2026-09-19
validated: 2026-09-19
---

# Result 011 — Side menu rework (right panel consistency)

<result>
<h1>011 Side menu rework — result</h1>
<p>Shipped 2026-09-19. Web-only feature; backend untouched. Deployed to the live dogfood install (~/apps/overmind/web via rsync of web/out, no host restart).</p>
<h2>What shipped</h2>
<ul>
<li>Task 01 — /dashboard route (re-export of the planning board) + UI rebrand: nav labels, page titles, right-panel heading "Dashboard Overview", "← Dashboard" back links. /planning?... redirects to /dashboard preserving query.</li>
<li>Task 02 — Project tabs (Entries/Dashboard) on entries + dashboard pages; sidebar cut to exactly Projects/Kits/Search/Settings, mobile nav mirrors; shared tabs component.</li>
<li>Task 03 — InfoPanel content contract: per-page right-panel sections (Entries Overview stats on /entries, Dashboard Overview planning stats on /dashboard, page-specific help on the rest); no-project states render select-a-project with no stats panel.</li>
</ul>
<h2>Live verification evidence (headless Chrome against live host http://127.0.0.1:4761, 2026-09-19)</h2>
<ul>
<li>Sidebar nav on /entries: exactly Projects(/), Kits, Search, Settings — rendered twice (desktop sidebar + mobile nav mirror).</li>
<li>/entries?project=ff313f29…: tab bar Entries(active, border-primary)/Dashboard(link carries ?project=…). Right panel "Entries Overview" + Total Entries / With Tags / By Type stats.</li>
<li>/dashboard?project=…&feature=011-side-menu-rework&tab=spec: Dashboard tab active, Entries tab link carries same project; 011 feature workspace renders (spec body, status pills, "← Dashboard" back link) — /planning?...&tab=spec lands on /dashboard with project/feature/tab params intact.</li>
<li>/dashboard without project: "Select a project first.", NO stats panel, page h1 "Dashboard".</li>
<li>/dashboard with project: right panel "Dashboard Overview" — Features 15, Tasks done 61/62, Specs validated 9, Status breakdown, Ceremony section.</li>
</ul>
<h2>Gates</h2>
<ul>
<li>dotnet build Overmind.sln: 0 warnings 0 errors. dotnet test: 677/677 passed (baseline kept, backend untouched).</li>
<li>npm run lint: 0 errors (6 pre-existing warnings in untouched files); npx tsc --noEmit clean; npm run build static export ok.</li>
</ul>
<h2>Reviews</h2>
<p>Tasks 01–03 light-reviewed (audits/review-011-01.md … -03.md): PASS, 0 blockers; nits fixed (project-tabs.tsx ?? → || null).</p>
</result>