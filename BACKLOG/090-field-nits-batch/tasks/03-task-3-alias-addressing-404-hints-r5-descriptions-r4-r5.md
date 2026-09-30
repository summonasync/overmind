---
feature: 090
task: 03
slug: field-nits-batch
status: done
review: light
created: 2026-09-29
depends_on: 02
brief: MCP alias resolver ({slug}/T3|S|P|R) + optional projectId on 4 tools + 404 hint texts (exception + 3 HTTP sites) + no-plan sentences in upsert_artifact/current_task descriptions + tests
---

# Task 3 — alias addressing + 404 hints + R5 descriptions (R4, R5)

<goal>
A typoed artifactId must not dead-end the agent: artifacts become addressable by slug aliases resolved in the MCP adapter, every artifact-not-found text teaches the format, and the no-plan-no-auto-review rule reaches the two tool descriptions that gate the affected moments.
</goal>

<scope>
- src/Overmind.Mcp/OvermindPlanningTools.cs:
  - Optional projectId + alias on planning_read_artifact, planning_patch_artifact, planning_set_status, planning_append_log.
  - Alias grammar: {slug}/T{order} for tasks, {slug}/S | {slug}/P | {slug}/R for the single spec/plan/result.
  - Resolution via the existing feature roster read path; alias + artifactId together, and alias without projectId, are client-side McpExceptions.
  - Unknown alias or kind/order mismatch → error listing the feature's actual kind/order pairs.
  - R5: planning_upsert_artifact and planning_current_task descriptions gain one sentence: without a validated plan the feature never auto-promotes to review — flip manually via planning_set_feature_status.
- src/Overmind.Core/Abstractions/PlanningException.cs — MissingArtifact message gains the alias-format hint + planning_list_artifacts recovery pointer.
- src/Overmind.Host/Api/PlanningArtifactsApi.cs — the three bare HttpErrors.NotFound artifact sites (GetArtifact, GetVersions, UpdateArtifact) carry the same hint.
- tests/Overmind.Mcp.Tests — alias parse/resolve/diagnostics + client-side rejections.
- tests/Overmind.Api.Tests — 404 detail contains the alias hint; string assertions on the two descriptions.
</scope>

<anti-scope>
- HTTP-level alias routing (aliases are MCP-adapter-only; the backend keeps GUIDs).
- Feature-number aliasing, 089 territory.
</anti-scope>

<constraints>
- Backend planning endpoints unchanged (resolution happens adapter-side).
- Extra roster read per aliased call is accepted; do not optimize.
</constraints>

<acceptance-criteria>
- [x] All four tools resolve 090/T3-style aliases and S/P/R document aliases against real artifacts.
- [x] Unknown alias errors list the feature's actual kind/order pairs.
- [x] alias+artifactId and alias-without-projectId are rejected client-side.
- [x] Unknown-GUID 404s (exception + three HTTP sites) carry the alias hint and recovery pointer.
- [x] planning_upsert_artifact and planning_current_task descriptions contain the no-plan rule.
- [x] dotnet build + dotnet test full suite green.
</acceptance-criteria>

<refs>
- Spec 62aac67e (R4, R5); plan b78738e8 (Task 3).
- OnboardQuickstart.Text already carries the no-plan line — do not duplicate logic, descriptions only.
</refs>

<obstacles>
<obstacles>
- Scope named three bare artifact-404 sites (PlanningArtifactsApi: GetArtifact/GetVersions/UpdateArtifact), but the agent door (AgentPlanningApi.cs) carried two more bare "Artifact '...' was not found." texts — agent GetArtifact and agent Validate. The failing agent-door 404 pin surfaced them; both now carry the same hint via PlanningException.ArtifactNotFoundHint, per the goal (every artifact-not-found text teaches the format).
- Briefing placed the no-plan description assertions in tests/Overmind.Api.Tests — impossible there: tool descriptions live in Overmind.Mcp and are not visible from the Api test project. They live in tests/Overmind.Mcp.Tests/PlanningToolContractTests.cs (UpsertArtifactAndCurrentTaskDescriptions_CarryTheNoPlanReviewRule) instead.
- Making artifactId optional on planning_set_status/planning_append_log forces status/logNote/note to the first parameter position (C# requires defaults after the first optional). MCP calls are JSON named arguments, so wire behavior is unchanged; all existing tool pins pass untouched.
- Canned fixture PlanningArtifactNotFoundJson updated to the new backend text so the adapter-relay test reflects the real response shape.
</obstacles>
</obstacles>

<log>
- 2026-09-29 [agent] — task cut from validated plan.
- 2026-09-30 [agent] — [agent] taken into work; blocker (task 2) closed done; dispatching dotnet-coder (sonnet).
- 2026-09-30 [agent] — coder started: reading current file state (MCP tools, Core exception, Host artifacts API, contract test pins) before alias resolver implementation
- 2026-09-30 [agent] — coder: recorded 4 quirks — 2 extra agent-door 404 sites fixed, no-plan assertions relocated to Mcp.Tests, parameter reorder on set_status/append_log, canned 404 fixture refreshed
- 2026-09-30 [agent] — coder done → review: alias resolver on 4 MCP tools (T{order}/S/P/R, roster-resolved, client-side rejections), R5 no-plan sentences in upsert_artifact/current_task descriptions, alias hint on MissingArtifact + all 5 artifact-404 HTTP sites; build green, Mcp 409/409, Api 519/519, Core 225/225, Infrastructure 535/535; 6/6 AC ticked, quirks in obstacles
- 2026-09-30 [agent] — [agent] done: light review PASS-WITH-NITS (audits/review-090-03.md), 3 NIT + 1 observation → microbatch, 0 blockers; all 4 coder obstacles confirmed via git history; reviewer rerun Mcp 409/409 + Api 519/519 + Core 225 + Infra 535.
</log>