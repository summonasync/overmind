---
feature: 001
slug: overmind-mvp
status: validated
created: 2026-09-06
validated: 2026-09-17
---

# Spec 001 — Overmind MVP

<decisions>

## Product
- **Name: Overmind.** One mind, swarm of agents. Repo/folder slug: `overmind`.
- Inspired by OpenViking (Volcengine context DB for agents, `viking://` URI model) but targeting radically simpler setup: download-and-run, no mandatory external services.

## Transport & process topology
- **Single .NET 10 host process**: tray application (Avalonia, tray-icon only, no native windows) + Kestrel HTTP API on localhost + serves the web UI (SPA) from the same origin.
- **Thin stdio MCP adapter** (separate small console app): agents spawn it via their standard MCP config; it proxies JSON-RPC to the local HTTP API. Chosen over pure stdio (avoids multi-process DB contention, UI and agents share one backend) and over HTTP-only MCP (stdio remains the lowest-friction client integration).
- **Custom `overmind://` URI scheme** lives inside MCP Resources (MCP natively supports arbitrary URI schemes in `resources/read`) — we get an OpenViking-style "protocol" without inventing a network transport. Example: `overmind://projects/<id>/gates`. <changed 2026-09-07 — scheme renamed ctx:// → overmind://, see TECHNICAL-SOLUTIONS.md ADR>

## Storage & search
- **SQLite (WAL) is the only metadata/document store.** Embedded, cross-platform, single-file backup. No external DB for core data.
- **FTS5 full-text search always available**, zero dependencies, offline.
- **Qdrant is the ONLY vector backend** (user decision, hard dependency on local Docker for semantic search). No Docker → honest degraded mode: FTS works, UI shows vector search as off.
- **Embedding provider is pluggable and selected in UI**: local ONNX (zero-config default) / Ollama / OpenAI-compatible endpoint. `IEmbeddingProvider` abstraction.
- User rejected: sqlite-vec as built-in vector store (chose Qdrant-only), Qdrant as optional (chose it as the single backend).

## UI
- **Tray app + web UI** (user decision over Avalonia/MAUI native windows). Browser is the app window; zero native window plumbing.
- Frontend per global conventions: Next.js (App Router, RSC, React Query, FSD, Tailwind), served/exported and hosted by the .NET app. BFF rule applies: UI talks only to the local API.

## Data model
- **Project is the top-level entity** (multi-project from day one): name, disk path, linked skill-kits. Context is isolated per project; retrofitting multi-project later would be painful.
- **Content types in v1**: notes/decisions, gates (checklists before actions), guards (constraint rules), skills (full skill packages: SKILL.md + files, with per-platform variants for `.claude` / `.kimi-code` / `.codex` / `.gemini` / `.antigravity`).
- All entries: tags, versioning, FTS5 index. Documents/snippets as full stored content, indexed for search.

## Skill engine (hybrid, split by priority)
Agents load skills **only from the filesystem** — there is no "push skill over the wire" API on any platform. Source of truth is our DB; disk is a managed cache.
- **v1 — materialize into project**: the app writes/cleans native skill directories inside the project folder (`.claude/skills/`, `.kimi-code/skills/`, …). Universal, works on all five platforms because these are their native discovery paths. Switching a kit in UI re-materializes files.
- **P1 — external-folder materialization + launcher** (deferred; verified feasible but not guaranteed across platforms): central kit store under `~/.overmind/kits/<kit-id>/`, agent launched with flags — `claude --add-dir <kit>` (documented), `kimi --skills-dir <kit>` (documented); Codex/Gemini/Antigravity need a spike; fallback is v1 in-project materialization.
- **P2 — runtime delivery via MCP**: `get_skill` tool, `overmind://skills/` resources, one universal bootstrap skill on disk teaching agents to pull skills on demand (the OpenViking pattern).

## Session bootstrap
On agent start, bootstrap instruction tells the agent to read `overmind://projects/<id>/gates` and `guards` first. In v1 this instruction is part of materialized content; P2 makes it a first-class MCP flow.

## Testing
xUnit + Shouldly + NSubstitute: unit tests for domain and skill engine; API integration tests via WebApplicationFactory; MCP adapter contract tests (JSON-RPC); FTS and degraded-mode behavior covered.

</decisions>

<open-questions>
- None. Spec may proceed to approval.
</open-questions>

<requirements>

Refined during brainstorming; baseline outcomes live in feature.md.

### R1. Host & API
- R1.1 Single .NET 10 process hosts: tray icon (Avalonia), Kestrel HTTP API bound to localhost (fixed default port with free-port fallback), static SPA hosting.
- R1.2 API is the single source of truth for UI, MCP adapter, and future integrations.
- R1.3 No auth beyond localhost binding for v1 (token auth is a P2+ candidate).

### R2. stdio MCP adapter
- R2.1 Console app, spawnable by any MCP client config; proxies tools/resources/prompts to the HTTP API.
- R2.2 Exposes `overmind://` resources for project context (projects, notes/decisions, gates, guards, skills). <changed 2026-09-07 — scheme renamed ctx:// → overmind://, see TECHNICAL-SOLUTIONS.md ADR>
- R2.3 Exposes tools for context CRUD and search (FTS always; semantic when Qdrant is up).

### R3. Storage
- R3.1 SQLite (WAL) holds projects, entries, tags, versions, kit definitions, materialization state.
- R3.2 FTS5 index over all textual content.
- R3.3 Qdrant client with explicit health check; app reports degraded mode (FTS-only) when unreachable.

### R4. Embeddings
- R4.1 `IEmbeddingProvider` with three implementations: ONNX local (bundled, default), Ollama (URL-configurable), OpenAI-compatible (URL + key configurable).
- R4.2 Provider selected in UI settings; re-indexing triggered on provider change.

### R5. Projects & content
- R5.1 Project CRUD: name, disk path, description, linked kits.
- R5.2 Entry CRUD per type: note/decision, gate, guard, skill (with per-platform variant content).
- R5.3 Tags + versioning on entries; FTS over everything.

### R6. Skill-kits & materialization (v1 mechanics)
- R6.1 Kit = named, versioned set of skills/gates/guards; kits are linkable to projects (many-to-many).
- R6.2 Activation materializes the kit into the project's native skill dirs for the chosen platforms; deactivation/switch cleans them.
- R6.3 Materialization state is tracked in DB; app can reconcile drift (files changed/deleted externally).
- R6.4 Bootstrap content materialized alongside: instruction to read gates/guards at session start via MCP.

### R7. Web UI
- R7.1 Next.js SPA served by the host; sections: Projects, Entries (per type), Kits, Search, Settings (embedding provider, Qdrant endpoint, ports).
- R7.2 Degraded-mode banner when Qdrant/Docker is unavailable.

### R8. Testing
- R8.1 Unit: domain rules, kit materialization engine, FTS queries.
- R8.2 Integration: API via WebApplicationFactory (incl. degraded mode).
- R8.3 Contract: MCP adapter JSON-RPC surface.

</requirements>

<!--
LIFECYCLE: после status: approved файл ЗАМОРОЖЕН.
Изменение = ре-валидация с пользователем + bump version. Молчаливые правки запрещены.
Читатели: оркестратор, ревьюер. Исполняющий субагент spec НЕ читает —
его контракт — tasks/NN-foo.md.
-->

<log>
- 2026-09-17 — re-imported from BACKLOG/001-overmind-mvp/spec.md
- 2026-09-17 — re-imported from BACKLOG/001-overmind-mvp/spec.md
</log>
