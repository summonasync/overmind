---
feature: 099
task: 03
slug: agent-claims
status: done
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
- [x] `dotnet build` + full `dotnet test` green
- [x] planning_set_current gone from the MCP tool surface
- [x] claim → foreign-session claim → 409 naming holder → unclaim, exercised via MCP-level tests
- [x] task_queue / list_features / read_feature outputs carry claim alias, vendor, claimedAt, derived state
- [x] Onboarding payload contains the naming-rules block (project value or generic hint)
</acceptance-criteria>

<refs>
- Spec: 0e774399 (MCP agent door + Read-side surface).
- Task 02 (order 2) — lock + session plumbing this task exposes.
- Pre-wave snapshot map (commit 275c905; lines shift while task 01 is in flight — re-grep before editing):
  - REMOVE pin surface: OvermindPlanningTools.cs:428-438 (planning_set_current tool); :581 and :609 dependency-tool descriptions mention planning_set_current — rewrite to claim wording; :508 task_queue description lists isCurrent in entries — swap to claim fields. AgentPlanningApi.cs:44 route + :365-391 agent handler; PlanningFeaturesApi.cs:22 route + :229-239 handler + SetCurrentAsync call. PlanningContracts.cs:17,35 IsCurrent fields + :51 SetPlanningFeatureCurrentRequest. PlanningMapper.cs:27,52 IsCurrent mapping. OvermindApiClient.cs:82-85 SetPlanningFeatureCurrent client method.
  - ADD: planning_claim_feature(projectId, slug, alias, vendor) / planning_unclaim_feature(projectId, slug) tools in the neighbor style ([McpServerTool] + [Description] carrying 409 semantics + recovery pointers); claim/unclaim HTTP endpoints for the UI door; Contracts/Mapper claim fields (alias, vendor, claimedAt, alive|stale|ended); OnboardApi.cs:143-149 currentSlug (from IsCurrent) → claim block + naming-rules block (project AgentNamingRules, else generic hint).
  - READ-SIDE note: MCP TaskQueue/ListFeatures/ReadFeature are JsonElement proxies (OvermindPlanningTools.cs:507+) — payloads come from HTTP; only descriptions change in MCP. DO NOT touch overmind://.../features/{slug}/current resource (OvermindPlanningResources.cs:36) — it is the current-TASK-of-feature read by slug, pin-free; planning_current_task likewise untouched. SessionBootstrapPrompt.cs:34,77 reference that resource legitimately.
</refs>

<obstacles>
- Human-door claim without a session: the data model requires ClaimSessionId, but the web door has no adapter session. Decision: FeatureClaimService.ClaimAsync takes Guid? sessionId — null means the human door; the service mints a fresh AgentSession row (StartedAt=LastSeenAt=now, so the claim goes stale like any other when the human walks away) and logs with the Ui source; UnclaimAsync(null) releases any claim without the holder check ("claim released via the human door", Ui). Agent door passes the X-Overmind-Session id and keeps the Agent source + holder semantics.
- The claim/unclaim agent routes deliberately carry NO ClaimLockFilter — they ARE the lock entry point; a foreign session must reach FeatureClaimService so stale take-over and auto-adopt can succeed instead of being generic-409'd. Missing session header on those two routes → 400 (a claim cannot bind without a session); unclaim via the human-door route stays session-free.
- Identity cap (review 099/01 nit): the MCP tools pre-validate alias/vendor ≤32 chars and throw a client-side McpException naming the cap before any HTTP call; the HTTP handlers additionally map the entity's ArgumentException to 400, so a 33-char identity never 500s on any door.
- Read-side liveness needs a session lookup the feature rows do not carry: new FeatureClaimService.ClaimLivenessAsync(projectId, features) returns featureId → alive|stale|ended using the project's stale threshold; threaded through Board, QueueRead, QueueMove, GetFeature (roster + full), human UpdateFeature, agent SetFeaturePriority. CreateFeature responses stay claim-free (a fresh feature cannot be claimed).
- Onboarding identity block: new OnboardAgentIdentityBlock(alias, vendor, namingRules) appended after the semantic block — alias/vendor defaults ("agent"/"unknown") live in Host-side AgentIdentityDefaults, namingRules = project.AgentNamingRules ?? generic "invent an alias" hint. The MCP adapter's own OVERMIND_ALIAS/OVERMIND_VENDOR defaults are a separate copy because Overmind.Mcp does not reference Core/Host (deliberate standalone adapter).
- Persistence bug caught by the human-door test: ClaimAsync added the minted session to the context BEFORE PersistAsync, whose FirstOrDefaultAsync queries the DATABASE (row not saved yet) and then added a SECOND instance with the same key → IdentityMap conflict. Fix: no eager Add; PersistAsync's existing Restore path creates the row on SaveChanges.
- Payload contract tests (PlanningApiTests full-contract field lists) extended with the four claim fields; MCP read_feature description pin relaxed from exact-equality to key phrases since the description now also names the claim fields.
- Full-suite runs under parallel load flaked twice during the round (timing-sensitive); isolated reruns green, control full run green.
</obstacles>

<log>
- 2026-10-01 [agent] — review nit from 099/01 (audits/review-099-01.md): identity cap (>32 chars) currently surfaces as ArgumentException → 500. Task 03 must pre-validate alias/vendor length in the MCP tool layer (client-side McpException naming the 32-char cap) or map the ArgumentException to a 400 — otherwise a 33-char alias becomes a server error.
- 2026-10-01 [agent] — orchestrator hold: user ordered a wave pause after task 02 closes (machine reboot planned). Do NOT dispatch task 03 until the user resumes the wave in-session. Resume order: task 02 review → done → then this task.
- 2026-10-02 [agent] — dispatched to dotnet-coder (same session — owns the lock surface from tasks 01/02), hold lifted by user after reboot, task 02 done
- 2026-10-02 [agent] — Executed by dotnet-coder. MCP: planning_claim_feature + planning_unclaim_feature (409 semantics + recovery pointers in descriptions, 32-char identity cap as client-side McpException), set_current already gone (verified), task_queue/list_features/read_feature descriptions name the claim fields. Host: agent-door POST claim/unclaim routes WITHOUT the lock filter (lock entry point — service matrix decides take-over/adopt), human-door claim/unclaim in PlanningFeaturesApi (session-free, Ui-sourced), FeatureClaimService Guid?-session overloads + ClaimLivenessAsync, claim fields (claimedByAlias/Vendor, claimedAt, claimState) threaded into Board/Queue/GetFeature(roster+full)/UpdateFeature/SetFeaturePriority payloads via PlanningContracts+PlanningMapper, OnboardAgentIdentityBlock (alias/vendor defaults + project naming rules or generic hint). Tests: Mcp claim/unclaim contract (identity posting+trim, 409 names holder, over-cap rejected without HTTP, unclaim echo nulls), tool-list + handshake lists updated (no set_current), Api doors (agent claim happy/400-no-header/400-over-cap/409-live/stale-take-over, unclaim holder/foreign, human door session-free), payload claim-field asserts (board/queue/read), onboard naming block (generic hint vs project value). Gates: dotnet build --no-incremental 0/0; dotnet test 1791 passed / 0 failed (Core 251, Infra 552, Api 569, Mcp 419); format clean outside Migrations.
- 2026-10-02 [agent] — task 03 executed to review: build 0/0, full suite 1791 passed / 0 failed, all 5 ACs ticked; claim/unclaim MCP tools + both doors, claim state in board/queue/read payloads, onboarding naming block, identity cap enforced client-side; ready for dotnet-code-reviewer (light review per plan)
- 2026-10-02 [agent] — 2026-10-02 [agent] — reviewed by dotnet-code-reviewer: FAIL — 1 BLOCKER (human-door claim 409s on a live claim; spec says take-over any feature, no session restrictions; human test covers free path only), 3 nits (dead claims param in Current, ClaimEchoAsync agent-only, human-held claim indistinguishable in payload → task 04); tools/caps/payloads/onboard clean, build 0/0, 1791/0 re-verified; report audits/review-099-03.md. Fix loop dispatched to the coder.
- 2026-10-02 [agent] — Fix round for review 099-03: (1) BLOCKER — FeatureClaimService.ClaimAsync now branches isHuman (sessionId null) before the holder gate: the human door skips the live-holder 409 and proceeds as take-over with the Ui-source log line; done→409 and identity caps stay for humans; implementation uses an effectiveSessionId local (also silences the CS8629 warnings the naive flag introduced). New test HumanDoor_Claim_OnLiveAgentClaim_TakesOver pins the hole: live agent claim → human claim 200, claim moves to the human identity, feature log carries "took over alive claim of Dan by qwen"; (2) nit — dead FeatureClaimService parameter dropped from AgentPlanningApi.Current; (3) nit — ClaimEchoAsync moved out of the agent door into shared Host/Api/FeatureClaimEcho.cs, both doors use it. Gates: dotnet build --no-incremental 0 errors / 0 warnings; full dotnet test 1793 passed / 0 failed (Core 251, Infra 552, Api 570, Mcp 419); format clean outside Migrations.
- 2026-10-02 [agent] — 2026-10-02 [agent] — re-reviewed fix round 1 (task 03) by dotnet-code-reviewer: PASS — human-door live-claim take-over at the exact gate, agent matrix untouched, regression test fails on old code, both nits applied; build 0/0, lock suite 59/59; report audits/review-099-03.md.
- 2026-10-02 [agent] — delta review PASS (audits/review-099-03.md, fix round 1): human-door bypass at the exact gate + regression test + both nits; AC 5/5; 1793/1793. Closing as done.
</log>