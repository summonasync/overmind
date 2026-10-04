---
feature: 094
task: 02
slug: artifact-deletion
status: done
review: light
created: 2026-10-04
depends_on: 01
brief: Both doors: MCP planning_delete_artifact (draft-only, alias, logNote) + HTTP DELETE (human, unrestricted) + door tests
---

# Task 02 — Doors: MCP tool + HTTP DELETE

<goal>
Expose guarded deletion through both doors: the agent MCP tool (draft-only) and the human HTTP DELETE (unrestricted).
</goal>

<scope>
- planning_delete_artifact(projectId, artifactId | alias, source, logNote): draft-only guards on the agent door, description documents the guard matrix (agents cannot delete validated/done; humans can via the UI), alias per 090.
- HTTP DELETE on the human door: unrestricted per matrix; 409s for edge-blocked tasks.
- Tests both doors: guard 409s, alias resolution, audit line in the feature log, human-deletes-validated path, unknown artifact 404 with the alias hint.
</scope>

<anti-scope>
- Web UI (task 03), restore, force flag.
</anti-scope>

<constraints>
- Description = documentation for agents: state the matrix verbatim, do not make agents discover it by 409s.
</constraints>

<acceptance-criteria>
- [x] MCP tool with contract tests: draft delete OK; validated/done 409 (agent); alias resolution; audit line lands in the feature log.
- [x] HTTP DELETE: human deletes a validated artifact; auto-transition fallout pinned; blockedBy task 409.
- [x] Tool description documents the guard matrix verbatim (pinned).
- [x] 404 carries the 090 alias hint.
</acceptance-criteria>

<refs>
- Spec 1966d6c8 (surfaces); plan 0b80c38а (task 2); alias machinery 090; description-as-doc convention 037.
</refs>

<obstacles>
- Minimal APIs do not infer body parameters on DELETE: the new request record on both delete handlers needed an explicit [FromBody], otherwise route building throws "Body was inferred but the method does not allow inferred body parameters" and every Api test fails at startup.
- DELETE with a JSON body needed a new OvermindApiClient overload (HttpRequestMessage with DELETE + JsonContent) — plain HttpClient.DeleteAsync sends no body.
- The tool's source param carries the door-source marker into the feature-log audit line ([agent]/[ui] prefix); the deleted-by marker stays derived from byHuman, so an agent can never fabricate "deleted by human".
- HandshakeContractTests pins the exact tool roster (was forty) — renamed to forty-one with planning_delete_artifact added.

</obstacles>

<log>
- 2026-10-04 [agent] — task cut from validated plan.
- 2026-10-04 [agent] — taken into work
- 2026-10-04 [agent] — implemented both doors: MCP planning_delete_artifact (artifactId | alias per 090 roster resolution, source, logNote; Destructive=true; description pins the guard matrix verbatim — "agents can delete draft artifacts only; validated/done deletion is the human's door in the UI") over the new agent-door HTTP DELETE /api/agent/planning/artifacts/{id} (LockToClaim, byHuman: false) echoing {id, kind, featureLogLine}; human-door DELETE /api/planning/artifacts/{id} now binds [FromBody] DeletePlanningArtifactRequest (logNote/source) and stays 204 NoContent; PlanningArtifactService.DeleteAsync gained an optional audit source; new OvermindApiClient DELETE-with-body overload. Tests: MCP — draft delete + request-shape pin, alias resolution, validated 409, dependents 409, 404 alias hint, alias+artifactId conflict, description pins, roster count 41; Api — agent door audit line in the feature log, validated-spec 409, done-task 409, human deletes validated spec pulling the feature out of review (demotion line pinned), dependents 409 naming task 02, unknown-id 404 with alias hint. Gates: dotnet build 0/0; Infrastructure 611/0, Api 591/0, Mcp 441/0; dotnet format clean on touched files. No commits made.
- 2026-10-04 [agent] — to review — task 02 complete. Both doors over the task-01 seam: MCP planning_delete_artifact (draft-only guards, alias per 090, matrix documented verbatim in the description) → new agent-door DELETE /api/agent/planning/artifacts/{id} (LockToClaim, byHuman: false, echo {id, kind, featureLogLine}); human-door DELETE extended with [FromBody] logNote/source, still 204, unrestricted per matrix. 4/4 AC ticked. Files: src/Overmind.Mcp/OvermindPlanningTools.cs + OvermindApiClient.cs, src/Overmind.Host/Api/AgentPlanningApi.cs + PlanningArtifactsApi.cs + Contracts/PlanningContracts.cs, src/Overmind.Infrastructure/Planning/PlanningArtifactService.cs; tests: PlanningToolContractTests + Canned + McpSessionTestBase + HandshakeContractTests (Mcp), AgentPlanningApiTests (Api). Gates: build 0/0; Infrastructure 611/0, Api 591/0, Mcp 441/0; dotnet format clean. No commits.
- 2026-10-04 [agent] — review returned PASS-WITH-NITS (audits/review-094-02.md): 0 blockers; WARNING (delete_artifact missing from the lock matrix) fixed — mutation row + switch arm added, lock theory re-run green; NIT source-passthrough pinned (trim + door-source fallback); NIT bodyless-204 was already pinned by the pre-existing 006-era test, the with-body form pinned additionally. Stab-vs-host parity verified by the reviewer (same route path). Fix round by the orchestrator, gates green.
- 2026-10-04 [agent] — review PASS-WITH-NITS closed: lock matrix row added (WARNING), source passthrough pinned, with-body 204 pinned; all findings green — doors complete over the task-01 seam
</log>