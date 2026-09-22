---
feature: 040
task: 01
slug: artifact-partial-read
status: done
review: light
created: 2026-09-22
brief: Agent-door GET + planning_read_artifact(sections?) with soft missingSections, read cost-tier descriptions, bootstrap read-routing, pinned
---

# 040/01 — planning_read_artifact: agent GET + sections + read-cost texts + pins

<goal>
One artifact readable by id through the agent door — `GET /api/agent/planning/artifacts/{id}` and the MCP tool `planning_read_artifact(artifactId, sections?)` — with optional section narrowing (soft `missingSections`, never a 400), cost-tier descriptions on both read tools, and the read routing line in the session bootstrap.
</goal>

<context>
The human door already serves `GET /planning/artifacts/{id}` (PlanningArtifactsApi.cs:18 — study its loader/mapper and reuse, do not fork it); the agent door has mutations only. `planning_current_task` hands out an artifact id — until now the only read was `planning_read_feature` (whole feature, up to ~150k chars in the field). `PlanningSections.Find/List` (039) extracts sections; the write side already carries cost tiers in descriptions (039 D9) — this task makes the model bidirectional.

Executor reads ONLY this file.
</context>

<scope>
- Host, agent door: `GET /api/agent/planning/artifacts/{id:guid}` in AgentPlanningApi (follow the existing route block, :17-27) — response: metadata (id, kind, status, version, order, updatedAt, brief) + body; `?sections=log,obstacles` narrows the body to those sections' content and adds `missingSections` (requested-but-absent names); 404 through the existing agent-door error mapping.
- Sections semantics (spec D2): comma/space-separated names, matched by the same anchored-pair rules as PlanningSections; absent sections NEVER fail the call.
- Mcp: `planning_read_artifact` in OvermindPlanningTools.cs — ReadOnly = true, Idempotent = true, `artifactId` + optional `sections` string; mirrors the GET including missingSections.
- Cost-tier descriptions (D3), verbatim intent: read_artifact with sections — "cheapest read: only the named sections"; without — "one artifact, full body"; read_feature — "most expensive read: the whole feature with all artifacts; prefer planning_read_artifact for one document" (extend its existing description additively).
- `SessionBootstrapPrompt`: read routing line next to WriteCostRouting, both branches — "one section → planning_read_artifact with sections; one artifact → planning_read_artifact; whole feature → planning_read_feature".
- Tests: Api — GET default full body+metadata; sections exact-subset body; missingSections lists absent names; 404 shape. Mcp — tool mirrors GET (stub), description verbatim pins for both read tools, bootstrap read-line pin in both branches (032 pattern, live ListToolsAsync).
</scope>

<anti-scope>
- No changes to `planning_read_feature`'s response shape or signature (description text only).
- No search work (task 02), no Core/Infrastructure changes, no web.
- No pagination, no version listing (exists), no diff (045).
</anti-scope>

<constraints>
- Reuse the human-door loader/mapper path where practical — shape drift between doors is a review finding.
- No comments; English strings; no new packages; no commits.
- House test gates: dotnet build (0W/0E) + dotnet test — green (baseline 1114).
</constraints>

<acceptance-criteria>
- Agent GET serves metadata+body; sections narrows exactly; missingSections soft-reports.
- planning_read_artifact mirrors it; descriptions and bootstrap lines pinned verbatim.
- All gates green; git status shows Host(AgentPlanningApi only)/Mcp/tests changes.
</acceptance-criteria>

<refs>
- Spec 040 D1-D3, R1-R3, R5; plan risks (door-shape divergence).
- PlanningArtifactsApi.cs:18 (human-door GET), AgentPlanningApi.cs:17-27 (route block), PlanningSections (039), SessionBootstrapPrompt.WriteCostRouting (039/03).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-22 — Execution starts: coder takes the read-artifact contract (agent GET + planning_read_artifact + sections + read-cost texts).
- 2026-09-22 — Light review PASS-WITH-NITS (0 BLOCKER / 0 IMPORTANT / 3 NIT, audits/review-040-01.md): soft-read proven 400-free, Include(Versions) drop judged correct (mapper never reads it), all three cost-text pins verbatim. NIT-1 (tab separator) closed in-tree by a parallel one-line pin; NIT-2/NIT-3 (human-door Include + AsNoTracking pair) → microbatch #40.
- 2026-09-22 — Accepted — read artifact tool complete with the tab pin landed. Feature 040 awaits its final task (search snippets, in execution).
</log>
