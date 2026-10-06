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
<li>dotnet build Overmind.sln: 0 warnings 0 errors. dotnet test: 677/677 passed (baseline kept, backend untouched).</li>
<li>npm run lint: 0 errors (6 pre-existing warnings in untouched files); npx tsc --noEmit clean; npm run build static export ok.</li>
</ul>
<h2>Reviews</h2>
<p>Tasks 01–03 light-reviewed (audits/review-011-01.md … -03.md): PASS, 0 blockers; nits fixed (project-tabs.tsx ?? → || null).</p>
</result>