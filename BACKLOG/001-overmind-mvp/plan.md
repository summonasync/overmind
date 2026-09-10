---
feature: 001
slug: overmind-mvp
status: approved
version: 1
spec: spec.md
created: 2026-09-06
---

# Plan 001 — Overmind MVP

<approach>
Greenfield, four .NET projects + one Next.js app, built bottom-up so every layer is testable before the next exists:

- `Overmind.Core` — domain entities and interfaces only, zero dependencies. Everything else points at it.
- `Overmind.Infrastructure` — EF Core (SQLite + FTS5), Qdrant client, embedding providers, kit materialization engine.
- `Overmind.Host` — single process: ASP.NET Core (Kestrel, Minimal APIs, static SPA hosting) + Avalonia tray icon.
- `Overmind.Mcp` — stdio MCP adapter console app, thin proxy over the HTTP API.
- `web/` — Next.js 16 (App Router, FSD, React Query, Tailwind), statically exported and served by the Host.

Order: scaffold (01) → domain+persistence (02) → HTTP API (03). Then two parallel tracks: skill-kits/materialization (04) and embeddings/Qdrant (05), both depending only on 02. Then MCP adapter (06, needs the API), web UI (07, needs the API), tray host polish + e2e (08).

Rationale: the API is the single source of truth (spec R1.2), so it lands before every consumer. Materialization and embeddings are independent of each other and of the API surface, so they can be built and reviewed in parallel. Tray polish is last because it needs the host's API and the SPA to already exist.
</approach>

<tasks>
- [x] 01-solution-scaffold — solution + 4 projects + test projects + central package management; verify: `dotnet build` green → tasks/01-solution-scaffold.md
- [x] 02-domain-and-persistence — Core entities/interfaces + EF Core SQLite with FTS5; verify: `dotnet test` (domain + EF integration) → tasks/02-domain-and-persistence.md
- [x] 03-core-api — Minimal APIs: projects/entries CRUD, FTS search, health; verify: WebApplicationFactory integration tests → tasks/03-core-api.md
- [x] 04-skill-kits-materialization — kit CRUD + project linking + materializer into native skill dirs + drift detection + bootstrap content; verify: unit tests with temp dirs → tasks/04-skill-kits-materialization.md
- [x] 05-embeddings-qdrant — IEmbeddingProvider (ONNX/Ollama/OpenAI-compatible) + Qdrant client + semantic search + degraded mode; verify: unit tests + degraded-mode integration test → tasks/05-embeddings-qdrant.md
- [x] 06-mcp-adapter — stdio JSON-RPC adapter: tools (CRUD/search) + `overmind://` resources + bootstrap prompt; verify: contract tests → tasks/06-mcp-adapter.md
- [x] 07-web-ui — Next.js UI (Projects/Entries/Kits/Search/Settings), served by Host, degraded banner; verify: `npm run lint && npx tsc --noEmit && npm run build` → tasks/07-web-ui.md
- [x] 08-tray-host-e2e — Avalonia tray (open UI, quit), single instance, port fallback, full manual e2e scenario; verify: manual checklist in task → tasks/08-tray-host-e2e.md
</tasks>

<risks>
- **ModelContextProtocol C# SDK maturity** — API surface may shift between versions; pin version in Directory.Packages.props, isolate SDK usage behind adapter internals (mitigation: contract tests in 06 pin our behavior, not the SDK's).
- **Avalonia tray on macOS** — TrayIcon outside a signed .app bundle is unreliable; v1 accepts "icon shows when run via dotnet run on Windows, macOS may need .app packaging" — packaging is out of MVP scope, noted as known limitation.
- **ONNX embedding model size** — all-MiniLM-class model is ~90 MB; decision: model is downloaded on first run into `~/.overmind/models/` (with progress), not bundled. Keeps installer small; offline-after-first-run preserved.
- **Qdrant Docker dependency** — by design (spec). Mitigation: health check + degraded mode + UI banner; app never crashes without Docker.
- **Platform skill-dir mapping** — exact discovery paths for Codex/Gemini/Antigravity are less documented than Claude/Kimi. Mitigation: mapping lives in a JSON config (`platforms.json`) loaded at runtime, fixable without recompile; Claude/Kimi hardcoded-verified in tests.
- **FTS5 triggers** — hand-written SQL for FTS sync is error-prone. Mitigation: single migration file, covered by integration tests (02).
</risks>

<verification>
After all tasks done:
1. `dotnet build` — clean.
2. `dotnet test` — all green.
3. `cd web && npm run lint && npx tsc --noEmit && npm run build` — clean.
4. Manual e2e (checklist in tasks/08-tray-host-e2e.md): start host → open UI in browser → create project with disk path → add a gate and a note → create kit with one skill, link to project → activate kit → verify `.claude/skills/` files materialized on disk → configure Claude Code MCP config to spawn `Overmind.Mcp` → agent reads `overmind://projects/<id>/gates` and finds the gate → FTS search returns the note → stop Docker → UI shows degraded banner, FTS still works.
</verification>

<!--
LIFECYCLE: после status: approved файл ЗАМОРОЖЕН (как spec.md).
Чекбоксы в <tasks> — единственное, что обновляется по ходу работы.
-->
