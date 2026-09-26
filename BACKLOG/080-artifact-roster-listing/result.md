---
feature: 080
slug: artifact-roster-listing
status: draft
created: 2026-09-26
---

# Result 080 — Artifact roster: planning_list_artifacts — metadata-only listing per feature (order/kind/status/review/AC/brief)

## Outcome
The 2-tier read cliff is closed: `planning_list_artifacts(projectId, slug, kind?)` serves a metadata-only roster of one feature's artifacts — documents (spec → plan → result) first, then tasks by order asc; task items additionally carry `review`, `acTicked`/`acTotal`, `brief`; documents carry none of the four. Bodies never leave the server.

Shipped in three tasks:
- **01 — agent-door GET** `/api/agent/projects/{projectId:guid}/planning/features/{slug}/artifacts?kind=` (PlanningFeaturesApi.ListArtifacts + AgentPlanningApi route + ToRosterItem mapper + PlanningArtifactRosterResponse contract, 4 Api tests). Review **PASS-WITH-NITS** (audits/review-080-01.md): 4 NIT + 1 PROCESS, all no-action; shape matched the pre-committed 070 MCP canned contract field-for-field.
- **02 — MCP tool** `planning_list_artifacts` (ReadOnly+Idempotent, pure passthrough — deliberately NOT wrapped in KeepReviewOnlyOnTaskArtifacts since the server already omits doc fields; verbatim roster description; kind forwarded when set, omitted when null/whitespace; 4 Mcp tests, 23-planning/33-total pins updated). Review **PASS** (audits/review-080-02.md): 2 NIT, no-action.
- **03 — docs anchors**: CONTEXT.md MCP surface «twenty-three tools» with the roster rung; STRUCTURE.md read ladder gained the roster as the cheapest read. Verified by grep + diff (2 lines in 2 files); orchestrator-reviewed (docs-only).

## Gates
- `dotnet build Overmind.sln` — 0 warnings / 0 errors (verified independently by both reviewers).
- `dotnet test Overmind.sln` — **1533/1533 green** (1344 baseline → +84 wave 070/086/088 → +4 roster Api +4 roster Mcp; Core 215 / Infra 496 / Api 451 / Mcp 371). One unrelated Mcp.Tests startup flake observed once across three full runs — known, outside the diff.
- Diff confinement: Host Api + Api tests (01), Mcp + Mcp tests (02), CONTEXT/STRUCTURE (03). No Core/Infrastructure changes (plan held), no new packages, no commits (user-gated).

## Deviations
- None against the validated plan. The task-01 scope referenced an "existing acTicked/acTotal mapping used by planning_read_feature" — no such mapping existed; executor used the single shared parser `PlanningSections.ParseAcceptanceCriteria` (same instance as UntickedAcWarning), reviewer confirmed correctness (documented in task obstacles).
- Accepted NITs: enum-numeric docs ordering (matches existing pattern, test-pinned); Versions join wasted on roster (verbatim-reuse per scope); `?kind=0` numeric tolerance (pre-existing shared helper); whitespace-only kind omitted client-side (server validates); double «cheapest read» wording in STRUCTURE ladder (semantically accurate).

## Deferred (honest gaps)
- Live MCP probe against a deployed host was not performed — the code is not deployed; the tool reaches agents after the next bundle deploy + adapter/session restart (house pattern). Coverage until then: ApiFactory integration tests exercise the real endpoint end-to-end; live ListToolsAsync pins the tool surface. Post-deploy smoke: call `planning_list_artifacts` on feature 080 (expect 3 tasks, no bodies) and `?kind=task`.

## Follow-ups
- User gates: commit wave (Host + Mcp + docs + tests + audits + BACKLOG mirror), bundle deploy, result validation, feature done.
- Microbatch candidates from reviews: tighten numeric-kind tolerance in the shared TryParseEnum helper (dedicated task), slimmer roster loader without the Versions join (touch with 045 diff work).

<log>
- 2026-09-27 [agent] — result drafted after all three tasks done (reviews: PASS-WITH-NITS / PASS / orchestrator docs check); 1533/1533 green; live MCP probe deferred to post-deploy.
</log>