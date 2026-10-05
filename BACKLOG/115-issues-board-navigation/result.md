---
feature: 115
slug: issues-board-navigation
status: draft
created: 2026-10-05
---

# Result 115 — Issues Board: third project tab + cross-project sidebar view (new/unread first)

## What shipped

Issues are reachable where the user looks:

- **Cross-project endpoint (T1, a555f1d):** read-only `GET /api/issues` outside the per-project and agent namespaces — one SQL roundtrip (Issues×Projects join, project name resolved server-side, append-count as a correlated scalar subquery; interceptor test pins `Queries == 1`), status filter, open-first sort (enum-order group, newest-first within), rows carry `projectId`+`projectName`. Inner join safe by the cascade/restrict model — no orphan rows.
- **Project tab (T2):** third tab «Issues» in the project detail header via the existing ProjectTabs pattern (pathname routes — the repo canon; the spec's `?tab=issues` wording was approximate, deviation recorded in the task log). Deep link `/issues?project=X&issue=Y` opens the detail with the tab active; per-project surface re-used from features/issues through the barrel.
- **Global board (T2):** `/issues` without a project renders the cross-project list — ProjectNameBadge + issue summary + age + appends + status chip, each row deep-linking into the project's issues tab. Server-delivered open-first order rendered as-is. `issueKeys.crossProject()` nests under `lists()` so triage/close/promote mutations invalidate both lists; live-sync on one interval per page; hooks order verified (all unconditional before the early return).

## Review rounds

- T1 light review PASS-WITH-NITS 0 blockers (audits/review-115-01.md): single-query verified honestly, namespace clean vs 028 routes, sort pinned on both layers.
- T2 light review PASS-WITH-NITS 0 blockers: pathname-vs-?tab= recorded as a spec-deviation note (mechanic matches intent), React Query invalidation and hook order verified, FSD clean.

## Gates (re-run by orchestrator)

dotnet: build 0w/0e, Infra 689 (+5), Api 658 (+2). web: lint 0 errors (6 pre-existing), tsc clean, 207/207 node --test (+7), build ok.

## Microbatch

#240 unbounded cross-project list (consistent with 028, paginate when the board grows), #241 status-parse block ×3, #242 CrossProjectIssueRow.projectName unused client-side.

## Post-release click-check

1. Project detail → third tab «Issues» active on /issues?project=X; per-project list behaves as before (filter/triage/promote/close).
2. Open an issue detail, copy the URL → pasting it reopens the same detail with the Issues tab active.
3. Global /issues (no project): rows from every project, open-first (a closed-but-new issue sits at the bottom), each row carries the project badge.
4. Click a global row → lands in that project's issues tab with the detail open.
5. Triage an issue from the global list's project page → both the project list and the global list refresh (invalidation covers both).

<log>
- 2026-10-05 [agent] — T1 coded + reviewed (PASS-WITH-NITS), endpoint live in the tree.
- 2026-10-06 [agent] — closeout: T2 review PASS-WITH-NITS 0 blockers, spec-deviation note recorded (pathname canon), gates green both stacks (dotnet Infra 689/Api 658; web 207/207). Waiting on the human tap.
</log>