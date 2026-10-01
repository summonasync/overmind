---
feature: 099
task: 03
slug: agent-claims
status: todo
review: light
created: 2026-09-30
depends_on: 02
brief: claim/unclaim MCP tools, set_current deleted, claim state in queue/list/read/board payloads, naming rules in onboarding
---

# Task 03 — MCP tools + HTTP API surface

<goal>
The agent-facing surface: claim/unclaim tools, set_current removed, claim state visible in every read the board and queue already make, naming rules in onboarding.
</goal>

<scope>
- src/Overmind.Mcp/OvermindPlanningTools.cs: planning_claim_feature(projectId, slug, alias, vendor), planning_unclaim_feature(projectId, slug) with descriptions carrying the 409 semantics; remove planning_set_current registration entirely.
- Claim fields + derived state (alias, vendor, claimedAt, alive|stale|ended) into planning_task_queue entries, planning_list_features, planning_read_feature outputs.
- HTTP: claim/unclaim endpoints for the web human door (PlanningFeaturesApi.cs / AgentPlanningApi.cs per repo split); board payload gains claim fields (PlanningContracts.cs, PlanningMapper.cs); set-current endpoint removed.
- OnboardApi.cs onboarding payload: agent naming rules block — project AgentNamingRules when set, generic hint otherwise.
- Contract/integration tests per repo conventions: claim/unclaim roundtrip, 409 texts name the holder, no set_current on the tool list, queue/list payloads carry claim state.
</scope>

<anti-scope>
No web UI code (task 04), no docs text updates (task 05). Adapter session plumbing already done in task 02.
</anti-scope>

<constraints>
English-only, no comments by default, no auto-commits. Tool descriptions follow the existing style (semantics + recovery pointers).
</constraints>

<acceptance-criteria>
- [ ] `dotnet build` + full `dotnet test` green
- [ ] planning_set_current gone from the MCP tool surface
- [ ] claim → foreign-session claim → 409 naming holder → unclaim, exercised via MCP-level tests
- [ ] task_queue / list_features / read_feature outputs carry claim alias, vendor, claimedAt, derived state
- [ ] Onboarding payload contains the naming-rules block (project value or generic hint)
</acceptance-criteria>

<refs>
- Spec: 0e774399 (MCP agent door + Read-side surface).
- Task 02 (order 2) — lock + session plumbing this task exposes.
- Pre-wave snapshot map (commit 275c905; lines shift while task 01 is in flight — re-grep before editing):
  - REMOVE pin surface: OvermindPlanningTools.cs:428-438 (planning_set_current tool); :581 and :609 dependency-tool descriptions mention planning_set_current — rewrite to claim wording; :508 task_queue description lists isCurrent in entries — swap to claim fields. AgentPlanningApi.cs:44 route + :365-391 agent handler; PlanningFeaturesApi.cs:22 route + :229-239 handler + SetCurrentAsync call. PlanningContracts.cs:17,35 IsCurrent fields + :51 SetPlanningFeatureCurrentRequest. PlanningMapper.cs:27,52 IsCurrent mapping. OvermindApiClient.cs:82-85 SetPlanningFeatureCurrent client method.
  - ADD: planning_claim_feature(projectId, slug, alias, vendor) / planning_unclaim_feature(projectId, slug) tools in the neighbor style ([McpServerTool] + [Description] carrying 409 semantics + recovery pointers); claim/unclaim HTTP endpoints for the UI door; Contracts/Mapper claim fields (alias, vendor, claimedAt, alive|stale|ended); OnboardApi.cs:143-149 currentSlug (from IsCurrent) → claim block + naming-rules block (project AgentNamingRules, else generic hint).
  - READ-SIDE note: MCP TaskQueue/ListFeatures/ReadFeature are JsonElement proxies (OvermindPlanningTools.cs:507+) — payloads come from HTTP; only descriptions change in MCP. DO NOT touch overmind://.../features/{slug}/current resource (OvermindPlanningResources.cs:36) — it is the current-TASK-of-feature read by slug, pin-free; planning_current_task likewise untouched. SessionBootstrapPrompt.cs:34,77 reference that resource legitimately.
</refs>

<obstacles></obstacles>

<log></log>

<log>
- 2026-10-01 [agent] — review nit from 099/01 (audits/review-099-01.md): identity cap (>32 chars) currently surfaces as ArgumentException → 500. Task 03 must pre-validate alias/vendor length in the MCP tool layer (client-side McpException naming the 32-char cap) or map the ArgumentException to a 400 — otherwise a 33-char alias becomes a server error.
- 2026-10-01 [agent] — orchestrator hold: user ordered a wave pause after task 02 closes (machine reboot planned). Do NOT dispatch task 03 until the user resumes the wave in-session. Resume order: task 02 review → done → then this task.
</log>
