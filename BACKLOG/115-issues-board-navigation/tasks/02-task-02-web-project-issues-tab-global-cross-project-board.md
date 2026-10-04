---
feature: 115
task: 02
slug: issues-board-navigation
status: todo
review: light
created: 2026-10-04
depends_on: 01
brief: Web: project issues tab + global cross-project list
---

# Task 02 — Web: project issues tab + global cross-project board

<goal>
Issues reachable where the user looks: third project tab, and the global /issues page showing every project's issues.
</goal>

<scope>
- Project detail: third tab «issues» (?tab=issues deep link, existing ProjectTabs pattern) rendering the existing per-project issues surface (features/issues re-use, barrel imports).
- Global /issues (no project param): cross-project list from task 01 — rows with ProjectNameBadge + issue summary + status, deep link into the project's issues tab; open-first order as delivered by the API.
- Web tests per repo patterns; gates green.
</scope>

<anti-scope>
- Kanban (028 decision); read-state UI; notification center.
</anti-scope>

<acceptance-criteria>
- [ ] ?tab=issues deep link renders the tab; per-project list intact.
- [ ] Global /issues renders cross-project rows (project badge, deep link) from the task-01 endpoint.
- [ ] lint/tsc/test/build green.
</acceptance-criteria>

<refs>
- Spec d899f2ac; plan 161608ec; features/issues; ProjectTabs pattern; 101 badge.
</refs>

<log>
- 2026-10-04 [agent] — task cut post-validation; waits on task 01 (API).
</log>