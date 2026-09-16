---
feature: 006
task: 07
slug: planning
status: done
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
- [x] Eight planning tools and three planning resources are listed and callable over stdio.
- [x] A gated validate returns an MCP error whose message starts with `STOP:` and writes nothing.
- [x] `current` renders task / stop / empty states distinctly, with the STOP text first.
- [x] Bootstrap prompt teaches the planning loop.
- [x] The adapter still references no other project and all calls go to `/api/agent/planning/*`.
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
- **SDK prefixes exception-based tool errors** (ModelContextProtocol 2.2.0): an `McpException` thrown from a tool method surfaces to the client as `An error occurred invoking 'planning_validate': <message>` — the STOP text was present but not first, failing the "message starts with STOP:" wire requirement. Fixed inside the adapter: `planning_validate` returns `CallToolResult { IsError = true, Content = [TextContentBlock(…)] }` with the gate's STOP detail verbatim, which the SDK passes through unprefixed. All other failures (404, non-gate 409s, invalid arguments) still use `McpException` and keep the informative prefixed rendering. No Host changes; task-04 route/DTO shapes were correct as-is.
- **`OvermindApiClient` kept unchanged** (pinned by plan Task 07): the 409 ProblemDetails detail is extracted from the `ApiStatusException` message at the `"): "` boundary — a format owned by the same assembly, with safe degradation (non-STOP details keep the full prefixed message). The plan's alternative (a `Detail` property on the exception) would have been cleaner but required touching the client.
- **Consequential test edit outside the scope list**: `HandshakeContractTests.ToolsList` pinned the exact tool set (six) — updated to fourteen plus the three planning resource templates, otherwise the suite breaks by construction when the surface grows.
</obstacles>

<log>
2026-09-08 — task created from the validated plan.

2026-09-13 — executed. Changes by file:

**src/Overmind.Mcp/OvermindPlanningTools.cs** — new, eight tools: `planning_list_features` / `planning_read_feature` / `planning_current_task` (ReadOnly+Idempotent, GET agent routes), `planning_create_feature`, `planning_upsert_artifact` (kind/review normalized client-side against spec|plan|task|result and light|hard), `planning_set_status` (status normalized against the seven-value wire vocabulary, empty logNote rejected client-side with the artifact-log explanation), `planning_append_log`, `planning_validate`. `current` in state `stop` reorders the payload so `reason` (the STOP text) is the first property. Gate refusals on validate surface as `CallToolResult { IsError = true }` with the STOP detail verbatim (see obstacles).

**src/Overmind.Mcp/OvermindPlanningResources.cs** — new, three markdown resources: board as `- <slug> [<status>] — <title> · tasks <done>/<total> · spec <status> · plan <status>` (null statuses render `none`), full feature as `# <title>` + body + `## Spec`/`## Plan`/`## Tasks` (`### <NN>. <heading>` from the task's first H1, `Status: … · Review: …` line, zero-padded order) + `## Result`; `current` renders `Current task: <slug> #<NN> [<status>]` + body, a first-line `STOP: …` (reason verbatim), or `NOTHING TO DO: …`. 404 → `ResourceNotFound` protocol error.

**src/Overmind.Mcp/SessionBootstrapPrompt.cs** — both branches (active-project and FallbackText) teach the planning loop: read `…/planning/features`, take `…/features/<slug>/current`, obey any STOP line (gated specs/plans are approved by the user, never by the agent), report progress via `planning_set_status` + `planning_append_log`.

**src/Overmind.Mcp/Program.cs** — registered `OvermindPlanningTools` / `OvermindPlanningResources` (DI + `WithTools`/`WithResources`).

**tests/Overmind.Mcp.Tests/Canned.cs** — agent payloads: board (2 features, one without spec/plan), full feature (spec validated+frozen, plan draft, tasks 01 done / 02 inProgress hard), standalone task artifact, `current` in all three states, 409 `HumanValidationRequired` ProblemDetails with the exact STOP detail.

**tests/Overmind.Mcp.Tests/McpSessionTestBase.cs** — `RegisterPlanningRoutes`: board/feature/current (generic task state + stop/empty slugs), create/upsert/status/log/validate POSTs; the three no-content routes return empty 200 bodies like the real Host.

**tests/Overmind.Mcp.Tests/PlanningToolContractTests.cs** — new, 15 tests (16 cases; the unknown-kind rejection is a theory over two inputs): eight-name tools/list with description pins, per-tool path/method/body assertions, stop-state payload leads with `reason`, gated validate → error text starts with `STOP:` and exactly one agent request is made, ungated validate confirms, unknown kind/status/review and empty logNote rejected client-side without any HTTP call.

**tests/Overmind.Mcp.Tests/PlanningResourceContractTests.cs** — new, 7 tests: three URI templates listed, board line shape, feature section ordering (Spec → Plan → Tasks → tasks by order, no Result section when absent), `current` task/stop/empty renderings, unknown feature → `-32002` ResourceNotFound.

**tests/Overmind.Mcp.Tests/PromptAndFailureModeTests.cs** — extended with two bootstrap tests (single-project and fallback branches both mention the planning resources, STOP rule, `planning_set_status`/`planning_append_log`).

**tests/Overmind.Mcp.Tests/HandshakeContractTests.cs** — tools pin 6 → 14 names; three planning resource templates added to the templates assertion (consequential, see obstacles).

**Verification:** `dotnet build Overmind.sln` → 0 warnings / 0 errors. `dotnet test Overmind.sln --no-build` → 400 passed, 0 failed (Core 85, Infrastructure 122, Api 134, Mcp 59; baseline 375, +25 in Mcp). `dotnet format --verify-no-changes` → clean except 6 pre-existing IDE0161 in generated EF migrations (untouched). `grep ProjectReference src/Overmind.Mcp/Overmind.Mcp.csproj` → 0; every planning call in the adapter targets `/api/agent/planning/*` or `/api/agent/projects/{id}/planning/*`.
</log>
