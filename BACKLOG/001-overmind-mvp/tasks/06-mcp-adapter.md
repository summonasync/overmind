---
feature: 001
task: 06
slug: mcp-adapter
status: done
review: hard
created: 2026-09-06
---

# 001/06 — stdio MCP adapter

<goal>
`Overmind.Mcp` is a console app that any MCP-compatible agent can spawn via standard MCP config. It serves tools, resources, and a bootstrap prompt over stdio (JSON-RPC), proxying to the Overmind HTTP API. Context is addressable via the `ctx://` URI scheme. Verified by contract tests that speak JSON-RPC to the process over real stdin/stdout.
</goal>

<context>
Spec R2: agents connect through this adapter; it is the product's front door for agents. `ctx://projects/<id>/...` resources are how agents read gates/guards/notes; tools allow CRUD + search. The adapter is stateless and thin — all logic lives behind the HTTP API. MCP clients configure it like: `{"command": "overmind-mcp", "args": []}` (binary name TBD at publish; dev: `dotnet run --project src/Overmind.Mcp`).
</context>

<scope>
`src/Overmind.Mcp/`:
- Use the official `ModelContextProtocol` C# SDK (pinned in task 01), stdio transport.
- Configuration: API base URL resolved in order: env `OVERMIND_API_URL` → `~/.overmind/host.port` file → `http://127.0.0.1:4761`. Startup failure to reach the API → exit code 2 with a clear stderr message (agents surface it).
- Tools (proxied to API, JSON results):
  - `list_projects` → GET /api/projects.
  - `list_entries(projectId, type?)` → GET /api/projects/{id}/entries.
  - `read_entry(entryId)` → GET /api/entries/{id}.
  - `write_entry(projectId, type, title, content, tags?)` → POST /api/entries; `update_entry(entryId, content)` → PUT.
  - `search(query, projectId?, mode?)` → GET /api/search (mode semantic|fulltext, default fulltext).
- Resources: `ctx://projects` (list), `ctx://projects/{id}` (project meta), `ctx://projects/{id}/gates`, `/guards`, `/notes`, `/decisions`, `/skills` (each → entry list of that type, markdown concatenated with title headers), `ctx://entries/{id}` (single entry).
- Prompt: `session_bootstrap` — returns instruction text: "At session start, read ctx://projects/{active}/gates and /guards and follow them." (active project resolved via env `OVERMIND_PROJECT_ID` or the single existing project if exactly one).
- Error mapping: API 404 → MCP resource-not-found; 503 (degraded semantic) → tool error with "vector search unavailable" message; network failure → tool error, never crash the process.

Tests (`tests/Overmind.Mcp.Tests/`):
- Contract tests spawning the adapter process with a stub HTTP server (loopback `HttpListener` returning canned API responses): initialize handshake, tools/list contains exactly the 6 tools above, resources/read for `ctx://projects/{id}/gates` returns stubbed markdown, write_entry produces the expected POST body, API 404 maps to the correct JSON-RPC error.
- No real Overmind.Host or SQLite in these tests.
</scope>

<anti-scope>
- No skill delivery tools (`get_skill`) — P2.
- No resources/templates beyond the listed `ctx://` URIs.
- No streaming/SSE transport — stdio only.
- No auth.
- No changes to Host endpoints; if a needed endpoint is missing, stop and report (do not add API surface from this task).
</anti-scope>

<constraints>
- ModelContextProtocol SDK pinned version from Directory.Packages.props (license MIT — verify via nuget-licensing-guard).
- Adapter must start and handshake in < 2s on a cold machine (agents time out slow servers).
- stderr for diagnostics only; stdout is protocol-clean (any stdout garbage breaks MCP — assert in tests).
- All API calls with 10s timeout.
</constraints>

<acceptance-criteria>
- [ ] Contract tests green: handshake, tools/list (exactly 6 tools), resources/read for all `ctx://` patterns, write/update/search proxying, error mapping (404/503/network).
- [ ] stdout purity test: no non-JSON-RPC bytes on stdout during a full session.
- [ ] Adapter exits code 2 with stderr message when API is unreachable.
- [ ] README of the repo NOT updated (docs polish is a separate step); example MCP config JSON included in this task file's <log> once verified.
</acceptance-criteria>

<refs>
- @../plan.md — task 06
- @../spec.md — R2, decisions/transport
- tasks/03-core-api.md — endpoint surface being proxied (must be done first)
</refs>

<obstacles>
- ModelContextProtocol 2.2.0 SDK has no public server constructor: `McpServer` is DI-only, so the adapter needs the generic host. `Microsoft.Extensions.Hosting` 10.0.10 was added to Directory.Packages.props (first-party Microsoft, MIT — verified; same 10.0.x band the pinned SDK already pulls transitively as Hosting.Abstractions). Alternatives rejected: `FrameworkReference Microsoft.AspNetCore.App` would make a stdio adapter require the ASP.NET Core shared framework on agent machines; hand-rolling an IServiceProvider over SDK internals is fragile.
- The SDK treats `McpProtocolException` thrown from a *tool* as a protocol-level JSON-RPC error (not a CallToolResult), so error mapping is two-layer: the HTTP client throws transport-agnostic domain exceptions (`ApiNotFoundException`, `SemanticSearchUnavailableException`, `ApiUnreachableException`, `ApiStatusException`); tools map them to `McpException` (spec-compliant `isError` tool results), resources map 404 to `McpProtocolException(ResourceNotFound)` (JSON-RPC -32002).
- Host's `UpdateEntryRequest` requires `Title`, so `update_entry(entryId, content)` does GET (read current title) + PUT (title + new content, tags untouched) — two proxied calls, no API surface added.
- `ctx://projects` (no template variable) registers as a static resource and appears in `resources/list`; the other 7 `ctx://` URIs are resource templates in `resources/templates/list`. Both lists asserted in contract tests.
- Host serializes enums camelCase (`JsonStringEnumConverter` in Host DI), so the adapter validates entry types case-insensitively and sends camelCase names ("gate", ...).
- Contract tests spawn the real adapter via `dotnet exec overmind-mcp.dll` (AssemblyName set to `overmind-mcp`) and speak newline-delimited JSON-RPC over real redirected stdin/stdout, with an `HttpListener` stub; no Overmind.Host/SQLite involved.
</obstacles>

<log>
2026-09-06 — writing-plans — task created from approved spec.
2026-09-06 — coder — implemented and verified. `src/Overmind.Mcp` is a stdio MCP adapter on ModelContextProtocol 2.2.0 (Apache-2.0 verified in nuspec) + Microsoft.Extensions.Hosting 10.0.10 (MIT verified; see obstacles). Files: Program.cs (base-URL resolve -> /api/health probe with exit code 2 on failure -> generic host with cleared loggers for stdout purity), ApiBaseUrl.cs (env OVERMIND_API_URL -> ~/.overmind/host.port -> http://127.0.0.1:4761), OvermindApiClient.cs (10s timeout, domain exceptions), OvermindTools.cs (exactly 6 tools), OvermindResources.cs (8 ctx:// URIs), SessionBootstrapPrompt.cs (session_bootstrap, active project via OVERMIND_PROJECT_ID or single-project fallback). Tests in tests/Overmind.Mcp.Tests: StubOvermindApi (HttpListener canned routes + request capture), McpProcessDriver (real process, NDJSON JSON-RPC, stdout capture for purity assertions), 27 contract tests across handshake/tools/resources/prompt/failure-mode.
Verification evidence:
- `dotnet build` (whole solution): Build succeeded, 0 Warning(s), 0 Error(s).
- `dotnet test` (whole solution): Core 30/30, Infrastructure 48/48, Mcp 27/27, Api 44/44 — all green.
- Handshake budget: measured process-spawn -> initialize response at 1050ms cold / ~370ms warm (asserted < 2s in StartupToHandshake_CompletesWithinTwoSeconds).
- stdout purity: AssertStdoutPurity runs after every session (all lines parse as jsonrpc 2.0); unreachable-API test additionally asserts stdout is byte-empty.
- Exit code 2 + stderr message verified both by contract test and manually: exit=2, stdout empty, stderr "FATAL: overmind API at ... is unreachable".
Verified example MCP client config (dev, from repo root):
{
  "mcpServers": {
    "overmind": {
      "command": "dotnet",
      "args": ["run", "--project", "src/Overmind.Mcp"],
      "env": { "OVERMIND_API_URL": "http://127.0.0.1:4761" }
    }
  }
}
(OVERMIND_API_URL optional — adapter falls back to ~/.overmind/host.port, then 127.0.0.1:4761. `env` block can be omitted entirely. Installed form once published: {"command": "overmind-mcp", "args": []}.)
README intentionally NOT touched per anti-scope.
2026-09-06 — fixer — review-gate fixes applied:
1. write_entry/update_entry no longer advertise skill (Host requires skillFiles, so it always 400'd). Writes restricted to note|decision|gate|guard; type=skill (update on a skill entry) returns an isError tool result "Skill entries are managed via the Overmind UI / future MCP delivery...". Tool descriptions updated. New contract tests: skill write rejected with message and NO POST upstream; update on skill entry rejected with NO PUT; theory over the 4 allowed types still proxying.
2. NormalizeType/NormalizeWritableType now throw McpException with a readable message (list of valid types) instead of ArgumentException escaping as an opaque -32603. Contract test: invalid type 'banana' → friendly isError, process alive, no POST.
3. ServerInfo.Version now taken from AssemblyInformationalVersion (Program.cs); handshake contract test asserts it matches the adapter assembly version.
Evidence: `dotnet build` 0 warnings / 0 errors (solution); `dotnet test` all green — Core 30/30, Infrastructure 48/48, Mcp 34/34, Api 44/44.
</log>
