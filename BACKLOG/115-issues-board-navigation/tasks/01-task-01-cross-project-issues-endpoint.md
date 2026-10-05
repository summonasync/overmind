---
feature: 115
task: 01
slug: issues-board-navigation
status: inprogress
review: light
created: 2026-10-04
brief: API: cross-project issues list (aggregate, filter, open-first sort)
---

# Task 01 — Cross-project issues endpoint

<goal>
One read-only endpoint returns every issue from every project, ready for the global board.
</goal>

<scope>
- IssuesService: aggregate query with project join (no N+1), status filter, open-first sort (open → triaged → promoted → closed), newest-first within group by creation.
- Host API: GET route (cross-project namespace, NOT under /api/projects/{id}) returning rows with issue fields + projectId + projectName (101 resolver data).
- Tests: aggregation across projects, filter behavior, sort order pinned, empty state.
</scope>

<anti-scope>
- MCP surface (non-goal v1); mutations; read-state model (challengeable v2).
</anti-scope>

<acceptance-criteria>
- [x] Cross-project endpoint live: aggregation, filter, open-first sort pinned by tests; no N+1 (single query with join).
</acceptance-criteria>

<refs>
- Spec d899f2ac; plan 161608ec; IssuesApi.cs; IssuesService (028); 101 ProjectNameBadge resolver.
</refs>

<obstacles>
- Sort relies on the IssueStatus enum numeric order (open=0 → closed=3) matching the board order; a mid-enum insert would silently reorder the board. Decision noted in a comment on ListAllAsync.
- The issue-status parse-and-reject block now exists in three list endpoints (IssuesApi, AgentIssuesApi, CrossProjectIssuesApi) — pre-existing duplication extended, left as-is to match 028 style; microbatch extraction candidate.
- EF SQLite fires only ReaderExecutingAsync on an interceptor, not the sync overload — the query-count test pins the async path.
</obstacles>

<log>
- 2026-10-04 [agent] — task cut post-validation (human proof 807f7db6).
- 2026-10-05 [agent] — taken by orchestrator — dispatching dotnet-coder (sonnet) for cross-project issues endpoint
- 2026-10-05 [agent] — dotnet-coder: cross-project endpoint landed — GET /api/issues (IssueService.ListAllAsync, single SQL query: join Issues×Projects + status filter + enum-order open-first sort + correlated append-count subquery, query count pinned by interceptor test). Gates: dotnet build 0w/0e; IssueServiceTests 21/21; CrossProjectIssuesApiTests+IssueDoorApiTests 13/13; dotnet format clean on 4 touched projects.
- 2026-10-05 [agent] — obstacles filled: enum-order sort coupling, tripled status-parse block (microbatch candidate), SQLite async interceptor quirk.
</log>