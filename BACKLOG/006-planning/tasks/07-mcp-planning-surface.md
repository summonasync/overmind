---
feature: 006
task: 07
slug: mcp-planning-surface
status: todo
review: light
created: 2026-09-08
---

# 006/07 — MCP planning tools, resources and bootstrap

<goal>
An agent connected through the stdio adapter can run the ceremony: read the feature board, read a full feature, ask for its `current` task (or receive an unmissable STOP), create features, upsert artifacts, move statuses, append log lines — and get refused when it tries to validate something a human must approve.
</goal>

<context>
`src/Overmind.Mcp` references **no other project** and proxies HTTP → JSON-RPC using tolerant `JsonElement` reads. Study the existing trio before writing: `OvermindTools.cs` (`[McpServerToolType]`, `[McpServerTool(Name=…, ReadOnly=…, Idempotent=…)]`, `[Description]` on tool and every parameter, `Guarded(...)` mapping API exceptions to `McpException`), `OvermindResources.cs` (`[McpServerResourceType]`, `[McpServerResource(UriTemplate=…, Name=…, Title=…, MimeType=…)]`, `Guarded` mapping 404 → `McpProtocolException(ResourceNotFound)`), `SessionBootstrapPrompt.cs`, and `Program.cs` for registration.

All routes are the agent door from task 04: `/api/agent/planning/*`. `OvermindApiClient` already covers GET/POST/PUT and maps 404/503/other statuses to typed exceptions — 409 arrives as `ApiStatusException` whose message contains the ProblemDetails `detail`, which is exactly the STOP text we want the agent to see.

Host↔MCP contract is additive-only; this task only adds.
</context>

<scope>
`src/Overmind.Mcp/OvermindPlanningTools.cs` (`[McpServerToolType]`):
- `planning_list_features` (`ReadOnly`, `Idempotent`) — `projectId` → board JSON.
- `planning_read_feature` (`ReadOnly`, `Idempotent`) — `projectId`, `slug` → full feature JSON.
- `planning_current_task` (`ReadOnly`, `Idempotent`) — `projectId`, `slug` → the `current` payload; when `state` is `stop`, the tool returns the payload **and** the reason text is the first thing in it (no extra wrapping).
- `planning_create_feature` — `projectId`, `slug`, `title`, `body?`.
- `planning_upsert_artifact` — `projectId`, `featureSlug`, `kind` (`spec|plan|task|result`), `body`, `order?`, `review?` (`light|hard`), `logNote?`. Unknown kind/review → `McpException` listing the allowed values (mirror `NormalizeType` in `OvermindTools`).
- `planning_set_status` — `artifactId`, `status`, `logNote` (required; empty → `McpException` explaining that status moves are recorded in the artifact log).
- `planning_append_log` — `artifactId`, `note`.
- `planning_validate` — `artifactId`; a 409 from the gate must reach the agent verbatim as an `McpException` message starting with `STOP:`.
Tool descriptions teach the order of the ceremony: read the board → take `current` → work → `set_status` + `append_log`; `validate` is human-only when the gate is on.

`src/Overmind.Mcp/OvermindPlanningResources.cs` (`[McpServerResourceType]`), markdown so agents can read it without JSON parsing:
- `overmind://projects/{id}/planning/features` — board as a markdown list: `- <slug> [<status>] — <title> · tasks <done>/<total> · spec <status> · plan <status>`.
- `overmind://projects/{id}/planning/features/{slug}` — full feature: `# <title>` + feature body, then `## Spec`, `## Plan`, `## Tasks` (each task as `### <NN>. <heading>` with its status line and body), `## Result`.
- `overmind://projects/{id}/planning/features/{slug}/current` — either the task body prefixed by `Current task: <slug> #<order> [<status>]`, or a first-line `STOP: …` / `NOTHING TO DO: …` rendered from `state` + `reason`.

`SessionBootstrapPrompt.cs`: extend the instruction text (both the active-project branch and `FallbackText`) with the planning loop — read `overmind://projects/{id}/planning/features`, take `…/current`, obey a STOP, report progress with `planning_set_status` + `planning_append_log`.

`Program.cs` (Mcp): register the new tool/resource types alongside the existing ones.

Tests — `tests/Overmind.Mcp.Tests`:
- `StubOvermindApi.cs`: serve the agent routes with canned payloads (`Canned.cs`), including a 409 `HumanValidationRequired` ProblemDetails body and the three `current` states.
- `PlanningToolContractTests.cs` — tools/list contains all eight names with the expected descriptions; each tool hits the right path and method (assert the recorded request); invalid kind/status/review produce an MCP error, not an HTTP call; a gated `planning_validate` surfaces an error whose message starts with `STOP:`; `planning_set_status` with an empty note is rejected client-side.
- `PlanningResourceContractTests.cs` — resources/templates list contains the three URIs; board markdown shape; full-feature markdown contains spec/plan/task headings in order; `current` renders `task`, `stop` and `empty` states; 404 → `ResourceNotFound` protocol error.
- `PromptAndFailureModeTests.cs` (extend) — bootstrap text mentions the planning resources and the STOP rule.
</scope>

<anti-scope>
- No Host changes: if a route shape from task 04 is wrong for the adapter, record it in `<obstacles>` and raise it — do not silently add Host routes here.
- No new transport, no protocol-version changes, no sampling/roots capabilities.
- No planning writes outside the agent door (never call `/api/planning/*` from the adapter).
</anti-scope>

<constraints>
- `dotnet build && dotnet test` fully green.
- `Overmind.Mcp` keeps zero project references; no new packages.
- English only; no comments unless non-obvious; file per type.
- Contract tests pin **our** wire behavior (paths, names, error text), not the SDK's.
- No git commits — report the changed-file list.
</constraints>

<acceptance-criteria>
- [ ] Eight planning tools and three planning resources are listed and callable over stdio.
- [ ] A gated validate returns an MCP error whose message starts with `STOP:` and writes nothing.
- [ ] `current` renders task / stop / empty states distinctly, with the STOP text first.
- [ ] Bootstrap prompt teaches the planning loop.
- [ ] The adapter still references no other project and all calls go to `/api/agent/planning/*`.
</acceptance-criteria>

<refs>
- @../spec.md — "MCP surface" (resources, tools, bootstrap), R5
- @../plan.md — Task 07
- @./04-agent-planning-api.md — the routes and DTO shapes being proxied
- src/Overmind.Mcp/{OvermindTools,OvermindResources,SessionBootstrapPrompt,OvermindApiClient,Program}.cs
- tests/Overmind.Mcp.Tests/{StubOvermindApi,Canned,McpSessionTestBase}.cs
- TECHNICAL-SOLUTIONS.md — "Host↔MCP contract is unversioned, additive-only"
</refs>

<obstacles>
</obstacles>

<log>
2026-09-08 — task created from the validated plan.
</log>
