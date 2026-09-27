---
feature: 083
task: 03
slug: feature-body-update
status: done
review: light
created: 2026-09-27
brief: MCP planning_update_feature — mutation tool, verbatim description, logNote required, count pins 24/34
---

# 083/03 — planning_update_feature MCP tool

<goal>
`planning_update_feature(projectId, slug, title?, body?, priority?, logNote)` — the agent-facing partial feature edit, mirroring the new POST /update; the tool that makes feature bodies correctable from any agent runtime (field evidence: Squeezr agent's unfixable typo).
</goal>

<context>
Executor reads ONLY this file. Prerequisite: task 02 (agent route) landed. Pattern: src/Overmind.Mcp/OvermindPlanningTools.cs — [McpServerTool] + [Description] on tool and every parameter, `Guarded(() => _api.PostAsJsonAsync(..., ct))` (mutation — see planning_set_feature_status / upsert patterns in the same file for the POST client method name), 070 slim echo passthrough (the server already returns the compact echo — NO adapter re-shaping, NO body echo).
</context>

<scope>
- Tool (NOT ReadOnly, Idempotent not set/false) next to planning_set_feature_status:
  Name `planning_update_feature`. Description VERBATIM: "Partial edit of a feature's title, body, or priority — durable what/why stays correctable by agents. logNote is mandatory and lands as a dated line in the feature log. A priority change moves the feature to the tail of its priority band. Status is not editable here — use planning_set_feature_status."
- Params: `Guid projectId`, `string slug`, `string? title`, `string? body`, `string? priority`, `string logNote`, `CancellationToken` — every param with [Description] (logNote description states it lands as a dated [agent] line in the feature log).
- Request body: omit null/empty strings entirely (title/body/priority absent when null/whitespace); logNote always sent; priority forwarded as given (server validates).
- Response: passthrough echo unchanged.
- Tests (tests/Overmind.Mcp.Tests/ — PlanningToolContractTests + HandshakeContractTests + Canned + McpSessionTestBase): live ListToolsAsync name pin + description verbatim pin; planning-tools count pins 23→24, total 33→34 (rename existing pins); canned happy path — echo JSON passthrough via JsonNode.DeepEquals, request payload carries logNote + only the set optional fields; missing logNote → the tool call itself fails validation (MCP schema requires logNote) — pin the schema; API 400 (bad priority) and 404 surface as tool errors, process alive.
</scope>

<anti-scope>
- No Host/Infrastructure/Core changes (tasks 01-02 own them), no web, no bootstrap/read-routing text changes (docs task 04 owns CONTEXT/STRUCTURE wording).
</anti-scope>

<constraints>
- No comments; English strings; no new packages; NO git commits.
- `dotnet build Overmind.sln` 0W/0E; full `dotnet test` green (baseline + tasks 01-02 additions).
- Diff confined to src/Overmind.Mcp + tests/Overmind.Mcp.Tests.
</constraints>

<acceptance-criteria>
- [x] planning_update_feature registered (exact name, mutation flags) in live ListToolsAsync; description + param descriptions match verbatim pins.
- [x] logNote is a REQUIRED tool parameter (schema pin); optional fields omitted from the request payload when null/whitespace.
- [x] Happy path passes the compact echo through unchanged (no body echo, no re-shaping).
- [x] API 400/404 surface as tool errors; count pins updated (24 planning / 34 total).
- [x] dotnet build 0W/0E + full suite green; diff confined to Mcp + Mcp tests.
</acceptance-criteria>

<refs>
- src/Overmind.Mcp/OvermindPlanningTools.cs (planning_set_feature_status — mutation POST pattern; list pins)
- POST /api/agent/projects/{projectId}/planning/features/{slug}/update (task 02)
- tests/Overmind.Mcp.Tests/ (032 pin pattern, canned stubs, count pins)
</refs>

<obstacles>
<obstacles>
- Param order: logNote sits before the optional title/body/priority (task listed it last) — C# forbids a required parameter after optional ones, and the MCP schema derives optionality from defaults. Schema requirement holds: logNote is in inputSchema.required.
- SDK 2.2.0 does not emit [McpServerTool] flag hints (annotations) in the wire tools/list payload — verified live: the annotations node is absent. Mutation flags (Destructive=false, Idempotent=false, no ReadOnly) are pinned at the attribute level in OvermindPlanningTools.cs, identical to every other mutation tool; the live pins cover name, description and schema.
</obstacles>
</obstacles>

<log>
- 2026-09-27 — Task created from validated plan 19f231e5; dispatch after task 02 (read-after-write on the route).
- 2026-09-27 [agent] — live ListToolsAsync name pins (24 planning / 34 total) + verbatim description pin green; mutation flags on the attribute, wire annotations absent in SDK 2.2.0 (see obstacles)
- 2026-09-27 [agent] — schema pin: logNote in required, title/body/priority optional; payload tests prove absent keys and blank values are omitted, logNote always sent
- 2026-09-27 [agent] — happy path: Guarded passthrough of the server echo, JsonNode.DeepEquals against the canned PlanningFeatureAgentUpdateResponse — no adapter shaping
- 2026-09-27 [agent] — 400 bad priority and 404 unknown feature surface as tool errors with the process alive; count pins renamed and green (TwentyFourPlanningTools / ThirtyFourTools)
- 2026-09-27 [agent] — dotnet build Overmind.sln 0W/0E; full dotnet test 1561/1561 green (Mcp 378, Api 462, Infra 496, Core 225); git diff confined to src/Overmind.Mcp + tests/Overmind.Mcp.Tests
- 2026-09-27 [agent] — Executed: planning_update_feature added next to planning_set_feature_status in OvermindPlanningTools.cs — verbatim description, [Description] on every param, JsonObject payload (logNote always sent; title/body/priority omitted when null/whitespace), POST to the task-02 /update route, echo passthrough via Guarded. Tests: verbatim description pin, schema pin (logNote required, optionals not), happy path DeepEquals passthrough + payload shape, logNote-only payload, blank-optionals-omitted, 400/404 tool errors with process alive; count pins 23→24 planning, 33→34 total. Quirks in obstacles: required-after-optional C# constraint forced logNote before the optionals; SDK 2.2.0 never emits wire annotations, mutation flags pinned at attribute level. Gates: build 0W/0E, suite 1561/1561, format clean on touched files, diff confined to Mcp + Mcp tests.
- 2026-09-27 [agent] — todo → review: 083/03 executed, all 5 AC ticked after real verification (build 0W/0E, suite 1561/1561, live pins green, diff confined to Mcp + Mcp tests)
- 2026-09-27 [agent] — light review PASS-WITH-NITS (0 BLOCKER / 0 IMPORTANT / 3 NIT microbatch) — audits/review-083-03.md; независимые гейты: build 0W/0E, 1561/1561; description сверён побайтово (task↔source↔pin); оба квирка приняты (C# required-after-optional, SDK 2.2.0 no wire-annotations — дом-консистентно); кросс-чек CONTEXT-рунга с живым description — без противоречий (закрыл отложенную сверку 04); done by orchestrator after review
</log>