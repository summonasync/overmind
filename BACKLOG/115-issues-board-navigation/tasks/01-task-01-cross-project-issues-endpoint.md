---
feature: 115
task: 01
slug: issues-board-navigation
status: todo
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
- [ ] Cross-project endpoint live: aggregation, filter, open-first sort pinned by tests; no N+1 (single query with join).
</acceptance-criteria>

<refs>
- Spec d899f2ac; plan 161608ec; IssuesApi.cs; IssuesService (028); 101 ProjectNameBadge resolver.
</refs>

<log>
- 2026-10-04 [agent] — task cut post-validation (human proof 807f7db6).
</log>