---
feature: 028
slug: cross-project-bug-intake
status: validated
created: 2026-10-03
validated: 2026-10-03
---

# Result 028 — Cross-project bug intake: чужой код → чужая борда (bug-сущность + onboarding правило)

## Outcome

All five tasks landed. The cross-project protocol is real end-to-end: a dedicated Issue domain with an append-only trail and a forward status machine (open → triaged → promoted → closed, closed reachable from any non-closed, no reopen), HTTP doors for both agent and human over the same service, six issues_* MCP tools, a foreign-create notification with an Issues-Board deep link, and a dedicated Issues Board tab in the web UI with the full human door (triage/close/promote into a feature picker). Promotion is lock-aware (028/05 architect W1): a claimed target feature blocks promotion from any session that does not hold the claim.

Per task:

- **01 Core + Infrastructure: Issue domain** — Issue/IssueAppend entities (append-only factory, no update/remove paths), IssueStatus enum, IssueService: Create validates board + author projects (404), Append lands provenance from any project, SetStatus enforces the machine (frozen transition map; source marker per the 043 pattern stored on the issue), Promote composes the task artifact through PlanningArtifactService.UpsertAsync inside an explicit transaction (goal = title+body, scope = ## Repro/Expected/Actual sections extracted with "(not provided)" markers, obstacles = the append trail, refs naming the issue) and links both ways. Migration AddIssues — CREATE-only, FTS-safe by construction.
- **02 Host API + foreign-create notification** — agent door (/api/agent/projects/{projectId}/issues: create/list/get/append/status/promote) + human door (/api/projects/{projectId}/issues, Ui-sourced), metadata-only list rows with AppendCount, full card with the chronological trail, foreign-create notification (Kind=Info, Tab="issues", FeatureSlug=issueId, text naming the author project) — own-project create posts nothing. Provenance 404 flows through both doors.
- **03 MCP tools** — IssuesTools.cs (new file, OvermindPlanningTools untouched): issues_create/list/get/append/set_status/promote with protocol-first descriptions, client-side pre-validation (blank title/body/text → McpException naming the field, zero HTTP), status/source normalization (invalid → client-side McpException listing allowed values), issues_list passing raw filter strings so the server owns the 400 text.
- **04 Web Issues Board** — dedicated tab from the left menu, list (title, status chip, author-project, age, append count) with status/author filters, detail with the append trail and human-door actions (triage, close with reason, promote via feature picker), sequential append-then-close semantics, React Query invalidation per the house idiom.
- **05 Closeout** — quickstart foreign-project protocol paragraph, architect light (APPROVED-WITH-WARNINGS → W1/W2 closed, N5 to follow-ups), result artifact, final gates.

## Authorized deviations / decisions

- **Promotion is lock-aware** (028/05 architect W1, user-approved spec log on 1fd1454c): PromoteAsync calls FeatureClaimService.EnsureHolderAsync for the TARGET feature before any writes — a claim held by any other session, in any liveness state (alive/stale/ended), 409s FeatureClaimHeld naming the holder (promote is not take-over: the owner team resolves the claim situation first); own-session or claim-free promotes proceed. sessionId flows from the doors: agent door reads X-Overmind-Session, human door passes null (human door promotes anything), MCP tools inherit via the stamped adapter session on the agent routes.
- **issues_set_status logNote accepted but not forwarded** (task 03, documented in the description): the Issue domain has no log — the source marker (agent/ui) is the provenance. The parameter exists for protocol symmetry with planning_set_status.
- **projectId-less agent namespace /api/agent/issues** (task 03 fix round, reviewer-recommended): the spec's id-tools are projectId-less while the task-02 doors were projectId-scoped; AgentIssuesApi now serves both — project-scoped routes for the human door and list/create, id-keyed routes (/api/agent/issues/{id}[...]) as thin ById handlers over the same IssueService for the MCP tools. Reads open by id; no cross-board guard (provenance model).
- **Issue list query params parsed in-handler** (string status / string authorProjectId → TryParse/Guid.TryParse → 400 with explicit text): minimal API returns body-less framework 400s on nullable enum/Guid query binding failures.
- **Web close-with-reason is sequential append-then-status** (task 04 fix round): the close POST fires only after the reason append succeeds, so a failed append can never close the issue with the reason lost.

## Verification evidence

Per-task review cycles: 01 — FAIL (append authorProjectId unvalidated) → fix + nit (Create source parameter) → delta PASS; 02 — FAIL (list parse defaults leaked into nullable filters: unfiltered board always empty; hidden by a test that never read those responses) → fix (true nullables + strengthened test asserting non-empty on no-query and status-only) + board guards on append/status/promote + cross-board pinning test → PASS; 03 — FAIL (four id-tools called the phantom /api/agent/planning/issues namespace that existed nowhere in the host; contract tests pinned tool↔stub fiction) → fix (projectId-less /api/agent/issues host routes + tools re-pointed + stubs re-pinned to real host paths + host-side integration test) → PASS; 04 web — FAIL (close-with-reason not sequential) → fix → PASS-WITH-NITS (microbatch); 05 closeout — architect light APPROVED-WITH-WARNINGS (W1 promote lock-awareness — closed this round; W2 unknown-id 500s — closed this round; N5 human-claim aging → follow-ups).

Final tree: dotnet build 0 warnings 0 errors; full suite **1857 passed / 0 failed** (Core 274, Infra 569, Api 582, Mcp 435) — sequential per-project run (a parallel sln run flaked with TimeoutException on random Api tests under 4-host load, machine-dependent; isolated runs stable). Web (task 04): lint 0 errors / tsc clean / build clean (with /issues route) / tests 52-52.

## Post-release batch dogfood checklist

Run after release against a board with a second project (the 090 isolated-host pattern: scratch HOME, own port, kill by PID):

1. **Cross-project create via real MCP** — from the second project's agent: issues_create(projectId=this board, authorProjectId=theirs, title/body with Repro/Expected/Actual) → compact echo, issue visible on this board with author provenance.
2. **Notification lands with deep link** — owner project's notification center has the Info entry (text names the author project); clicking the deep link opens /issues?project={board}&issue={id} → the detail view of the filed issue.
3. **Append from another project** — issues_append from the foreign project → entry lands in the trail with authorProjectId + text + createdAt; visible in the detail view.
4. **Owner promotes** — issues_promote(issueId, featureSlug) → task artifact composed on the target feature (goal/scope/obstacles/refs per the mapping), issue renders promoted with the artifact link; the task is visible on the target feature. Lock check: a foreign claim on the TARGET FEATURE 409s the promote.
5. **Issues Board click-check** — list filters (status, author-project) work in the browser; detail shows the trail; triage/close/promote actions work through the human door; close-with-reason appends the reason before closing.

## Follow-ups (microbatch queue)

- Guarded/EchoResult extraction: now 8 copies across the adapter (IssuesTools copy already diverged — lost StopDetailOf STOP-prefixed error handling); promote to a shared internal helper.
- Web: status chips on the Issues Board are hand-rolled tones instead of the shared Badge tone map — unify.
- Web: Issues Board filters not in URL — port the planning board's URL + session-storage filter sync.
- NormalizeIdentity duplication: carried from 028/01 review (issue identity guards vs the shared normalizer idea from 099).
- Allow-list filter-marker test: carried from 099/02 review (every non-GET route under /api/agent/planning carries the lock filter).
- Web notification-href: tab=="issues" routing depends on the Issues Board route landing with task 04 (shipped) — keep href and route in sync if the route moves.

## Status

Feature ready for user validation; result stays draft until user validation. Architect light APPROVED-WITH-WARNINGS with W1/W2 closed (this round). No live dogfood in this wave — the post-release batch checklist above is the contract for the follow-up session.

<log>
- 2026-10-03 [agent] — drafted by dotnet-coder at the wave close: outcome per task, authorized deviations/decisions, verification evidence (per-task review cycles + final suite 1855/0), post-release batch-dogfood checklist (5 scenarios), microbatch follow-ups. Draft pending architect-light + user validation.
- 2026-10-03 [agent] — architect 028/05 micro round (W1 lock-aware promotion + W2 unknown-id 404s + N5 follow-up): decision recorded in Authorized deviations; suite 1857/0 (Core 274, Infra 569, Api 582, Mcp 435).
- 2026-10-03 [agent] — Architect 028/05 micro round: W1 — promotion is now lock-aware (EnsureHolderAsync before any writes; foreign claim in any liveness → 409 naming holder, own-session/claim-free proceed; sessionId flows from agent door header / human null / MCP tools inherit via the stamped session); W2 — unknown issue id returns 404 on both doors (both Gets wrapped, pinned by UnknownIssueId_Returns404OnBothDoors); N5 human-claim aging appended to follow-ups. Result Status paragraph records APPROVED-WITH-WARNINGS with W1/W2 closed.
</log>