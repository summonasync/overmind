---
feature: 001
task: 03
slug: core-api
status: todo
review: light
created: 2026-09-06
---

# 001/03 — Core HTTP API

<goal>
`Overmind.Host` exposes a localhost HTTP API (Minimal APIs) covering project CRUD, entry CRUD with tags and versioning, and FTS search, plus a health endpoint. Verified by WebApplicationFactory integration tests against a real SQLite temp database.
</goal>

<context>
Spec R1/R5: the API is the single source of truth — the web UI (07), the MCP adapter (06), and every future consumer all talk only to it. Binding is localhost-only; no auth in v1.
</context>

<scope>
`src/Overmind.Host/`:
- `Program.cs` — `WebApplication.CreateBuilder`, registers Infrastructure (DbContext with SQLite file at `~/.overmind/overmind.db`, created if missing), Kestrel bound to `127.0.0.1`, default port 4761 with free-port fallback (if taken, bind port 0 and log the actual port; write chosen port to `~/.overmind/host.port`).
- Endpoint groups (one file per group, `Api/` folder):
  - `ProjectsApi` — `GET/POST /api/projects`, `GET/PUT/DELETE /api/projects/{id}`.
  - `EntriesApi` — `GET /api/projects/{projectId}/entries?type=&tag=&q=` (q = FTS), `GET/POST /api/entries`, `GET/PUT/DELETE /api/entries/{id}`, `GET /api/entries/{id}/versions`. Skill entries accept/return `SkillFile[]` payloads inline.
  - `SearchApi` — `GET /api/search?q=&projectId=&limit=` → FTS hits via `FtsSearchService` (from task 02).
  - `HealthApi` — `GET /api/health` → `{ "status": "ok", "vectorSearch": "up|down", "version": "<assembly>" }` (vectorSearch reads `ISemanticSearch.HealthAsync` — wire a `NullSemanticSearch` returning down until task 05 replaces it).
- DTOs per operation in `Api/Contracts/`, manual mapping (no mapper libs), FluentValidation for create/update DTOs (name non-empty, DiskPath absolute, entry Content non-empty, etc.).
- ProblemDetails error responses; 404 for missing ids; 409 on project name conflict (unique index in task 02's migration — if missing, add it here in a new migration).

Tests (`tests/Overmind.Api.Tests/`): WebApplicationFactory with per-test temp SQLite file:
- Full project + entry CRUD lifecycle.
- FTS search returns the right entry after create, respects `projectId` filter and `type` filter.
- Version list grows on PUT.
- Validation failures return 400 with ProblemDetails.
- Port fallback logic: unit-test the port selection helper (not Kestrel itself).
</scope>

<anti-scope>
- No MCP anything (task 06).
- No kit endpoints (task 04), no semantic search (task 05) — `NullSemanticSearch` stub only.
- No static file hosting / SPA (task 07/08 wire that).
- No Avalonia, no tray (task 08). Host runs as plain console web app for now.
- No authentication/authorization middleware.
</anti-scope>

<constraints>
- Minimal APIs only, no controllers.
- FluentValidation (add to Directory.Packages.props — MIT, verify via nuget-licensing-guard).
- Serilog for structured logging (console sink), MIT — verify via guard.
- Async all the way; CancellationToken plumbed to EF calls.
- DTOs never expose entities directly.
</constraints>

<acceptance-criteria>
- [ ] `dotnet test` green, incl. all integration tests above.
- [ ] `dotnet run --project src/Overmind.Host` starts, `curl http://127.0.0.1:4761/api/health` returns JSON with `"status":"ok"`.
- [ ] Port file `~/.overmind/host.port` written on startup (integration test asserts content equals bound port).
- [ ] No entity types appear in any response DTO (checked by review).
</acceptance-criteria>

<refs>
- @../plan.md — task 03
- @../spec.md — R1, R5, R3.2
- tasks/02-domain-and-persistence.md — entities, FtsSearchService signatures (must be done first)
</refs>

<obstacles>
<!-- Filled by executors during work -->
</obstacles>

<log>
2026-09-06 — writing-plans — task created from approved spec.
</log>
