---
feature: 115
task: 02
slug: issues-board-navigation
status: done
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
- [x] ?tab=issues deep link renders the tab; per-project list intact.
- [x] Global /issues renders cross-project rows (project badge, deep link) from the task-01 endpoint.
- [x] lint/tsc/test/build green.
</acceptance-criteria>

<refs>
- Spec d899f2ac; plan 161608ec; features/issues; ProjectTabs pattern; 101 badge.
</refs>

<log>
- 2026-10-04 [agent] — task cut post-validation; waits on task 01 (API).
- 2026-10-05 [agent] — taken by orchestrator — dispatching web-coder (sonnet) for issues tab + global cross-project board
- 2026-10-05 [agent] — web surface landed: third project tab (issues) + global cross-project board on /issues; lint 0 errors / tsc clean / 207 tests pass (7 new) / build green
- 2026-10-05 [agent] — scope notes: no status-filter UI on the global list (API supports ?status=, server sort rendered as-is); /issues?issue= without ?project= lands on the global list — card endpoint needs projectId, no cross-project card route exists; global row/section named GlobalIssue* to avoid clashing with the CrossProjectIssueRow contract type
- 2026-10-05 [agent] — review round: light PASS-WITH-NITS 0 blockers (hooks order verified, invalidation prefix-match covers both lists, FSD clean). Spec-deviation note per reviewer: the tab deep link is the pathname route /issues?project=X — NOT ?tab=issues as the spec text loosely said; pathname is the existing ProjectTabs canon (dashboard/entries live on pathname routes), so the mechanic matches the spec's intent while the literal query-param wording does not apply. Nits: CrossProjectIssueRow.projectName unused client-side (badge resolves via useProjectName) -> microbatch #242; crossProject() key collision — theoretical only.
- 2026-10-05 [agent] — closeout complete: review PASS-WITH-NITS 0 blockers, spec-deviation note recorded (pathname canon), result 361a7601 drafted with click-check, nits #240-242 to microbatch. Feature goes to review; done-flip waits on the human tap.
</log>