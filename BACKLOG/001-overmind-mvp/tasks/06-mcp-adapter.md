---
feature: 001
task: 06
slug: mcp-adapter
status: todo
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
<!-- Filled by executors during work -->
</obstacles>

<log>
2026-09-06 — writing-plans — task created from approved spec.
</log>
