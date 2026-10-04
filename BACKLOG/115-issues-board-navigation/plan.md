---
feature: 115
slug: issues-board-navigation
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 115 — Issues Board: third project tab + cross-project sidebar view (new/unread first)

<goal>
Backend aggregate first, both web surfaces second.
</goal>

<sequence>
- 01 (dotnet coder): cross-project issues endpoint — IssuesService aggregate query (project join, no N+1), API route, status filter, open-first/newest sort, tests.
- 02 (web coder): project issues tab (?tab=issues deep link, re-uses features/issues) + global /issues cross-project list (project badge rows, deep links). Waits on 01.
- Reviews: one light review per task, batched when both land (01 backend, 02 web — may run as one pass if timing allows). Result after.
</sequence>

<verification>
- API tests: aggregation includes multiple projects, filter behavior, sort order pinned.
- Web gates: lint/tsc/test/build.
- Deep-link check: ?tab=issues renders the tab directly.
</verification>

<refs>
- Spec d899f2ac; 028 issues domain; IssuesApi.cs; app-sidebar.tsx.
</refs>

<log>
- 2026-10-04 [agent] — plan draft.
- 2026-10-04 [agent] — validated with human proof: notification 807f7db6-d5b2-4288-8f00-291986dfab83 replied 2026-10-04 18:51:13 UTC
</log>