# CONTEXT — Overmind System Design

Living description of the system **as built**. Update it when structure, roles, or communication rules change; per-decision rationale lives in `TECHNICAL-SOLUTIONS.md` (ADRs), per-feature history in `BACKLOG/`.

## What Overmind is

A win/mac/linux desktop app (.NET 10) that gives AI coding agents (Claude Code, Kimi Code, Codex, Gemini CLI, Antigravity) a shared, persistent context store. One local app holds projects, notes/decisions, gates, guards, and skills as the single source of truth; agents connect through a thin stdio MCP adapter over a loopback HTTP API; skill-kits are materialized into project folders so agents pick them up natively. Setup is download-and-run; the only optional external dependency is a local Qdrant (Docker) for semantic search.

## Process topology

```
AI agent (Claude/Kimi/...)                Browser
      | stdio JSON-RPC                         |
      v                                        v
Overmind.Mcp (console adapter)  ----HTTP---->  Overmind.Host (single process)
                                               ├─ Kestrel minimal API, 127.0.0.1 only
                                               ├─ static SPA hosting (web/out)
                                               └─ Avalonia TrayIcon (no native windows)
                                                       |
                                                       v
                                          Overmind.Infrastructure
                                          ├─ SQLite (WAL) + FTS5  — always on
                                          └─ Qdrant (Docker)      — optional, vector search
```

- **One process owns everything.** The Host is the single source of truth; UI, MCP adapter, and future integrations all talk to its HTTP API. No direct DB access from any other process.
- **Single instance.** `SingleInstance` takes a machine-wide lock at startup; a second launch reads the port file and opens the running UI in the default browser instead of starting.
- **Tray-only UX.** The Host has no native windows — an Avalonia `TrayIcon` (FluentTheme) on a dedicated UI thread (STA on Windows; StatusNotifierItem over D-Bus on Linux) offers Open/Quit. The browser is the app window. Quit stops Kestrel cleanly; `--no-tray` runs headless. The folder picker behind `system/browse-folder` shells out per OS: PowerShell FolderBrowserDialog (Windows), `osascript` (macOS), `zenity`/`kdialog` (Linux).

## Projects and roles

### `src/Overmind.Core` — domain, zero dependencies

Entities, enums, and abstractions only. No framework usings, no EF, no serialization attributes. Everything else points at it.

- Entities: `Project`, `ContextEntry`, `SkillFile`, `Tag`, `EntryTag`, `EntryVersion`, `SkillKit`, `SkillKitEntry`, `ProjectKit`, `AppSetting`, and the planning domain: `PlanningFeature`, `PlanningArtifact`, `PlanningArtifactVersion`, `PlanningMaterializedFile`.
- Enums: `EntryType` (note, decision, gate, guard, skill), `AgentPlatform` (claude, kimi-code, codex, gemini, antigravity, qwen), planning: `PlanningFeatureStatus` (draft, ready, in-progress, done), `PlanningArtifactKind` (spec, plan, task, result), `PlanningArtifactStatus` (draft, validated, todo, in-progress, review, done, blocked), `PlanningReviewLevel` (light, hard), `PlanningMaterialization` (inherit, off, on, archive), `HumanValidationOverride` (unset, on, off).
- Abstractions (`Abstractions/`): `IEmbeddingProvider`, `IMaterializer`, `ISemanticSearch`, `IPlanningMaterializer`, plus their DTOs (`MaterializationReport`, `DriftItem`, `EntrySearchHit`, `PlanningMaterializationReport`, `PlanningDriftItem`) and exceptions (`MaterializationException`, `PlanningException` carrying `PlanningFailureKind`/`PlanningFailureCode`).
- Domain behavior lives on entities where practical: factory methods (`ContextEntry.Create/CreateSkill`, `ProjectKit.Create`), invariants (`SkillKit.AddEntry` duplicate guard), versioning (`ContextEntry.UpdateContent` snapshots an `EntryVersion`). `PlanningLog` appends dated lines to a planning artifact's `<log>` section.

### `src/Overmind.Infrastructure` — persistence, search, materialization

References Core only.

- `Persistence/` — `OvermindDbContext` (EF Core Sqlite), migrations, `WalPragmaInterceptor` (WAL mode on every connection), design-time factory.
- `Search/` — `FtsSearchService`: FTS5 over all textual entry content, kept in sync via hand-written SQL triggers in the initial migration. Always available, zero dependencies.
- `VectorSearch/` — `QdrantSemanticSearch` (implements `ISemanticSearch`), `QdrantCollection` (collection provisioning/config), `EntryIndexingService` (called by the API on entry create/update/delete), `ReindexService` (full rebuild after provider change).
- `Embeddings/` — `IEmbeddingProvider` implementations: `OnnxEmbeddingProvider` (default, bundled pipeline via `OnnxEmbeddingPipeline`; model files downloaded on first run by `ModelDownloader` into `~/.overmind/models/`), `OllamaEmbeddingProvider`, `OpenAiCompatibleEmbeddingProvider`. `EmbeddingProviderFactory` picks the provider from stored settings.
- `Materialization/` — `SkillMaterializer` (implements `IMaterializer`), `PlatformDirectoryMap` (native skill-dir mapping loaded from embedded `platforms.json`, fixable without recompile), `PathGuard` (path-traversal defense, root-relative resolution), shared internal primitives (`ContentHash`, `Slug`, `DirectoryPruner`) reused by the planning materializer, and the `MaterializedFile` entity (materialization state: project, kit, platform, relative path, SHA-256).
- `Planning/` — application services for the planning APIs, extracted per the vertical-slice ADR trigger (006/A1): `PlanningFeatureService`, `PlanningArtifactService`, `PlanningPolicy`, and `PlanningMaterializer` (real `IPlanningMaterializer`, scoped; writes `BACKLOG/<slug>/` + the managed `BACKLOG.md` block per project strategy off/on/archive, archive root injected from Host — replaces the `NullPlanningMaterializer` placeholder from tasks 02–04). Plus the `BacklogImporter` (idempotent file→DB import behind the Host's `--import-backlog` CLI flag; parsing only, writes go through `PlanningFeatureService.ImportAsync`) and the shared `PlanningServiceGuard` (unique-violation detection + swallow-and-log materialization wrappers used by both services).
- `Configuration/` — `AppSettingsService` over the `AppSetting` table, with `SettingKeys`/`SettingDefaults`.

### `src/Overmind.Host` — composition root, HTTP API, tray

References Core + Infrastructure. ASP.NET Core (Kestrel) + Avalonia in one process.

- `Program.cs` — bootstrap: data-dir creation, single instance, Serilog, port resolution, SPA static hosting, migrations on startup, `--import-backlog` flag (`BacklogImportCommand`, runs after migrations, never blocks boot), port file, tray thread.
- `DependencyInjection.cs` — `AddOvermind()` registers everything (scoped DbContext + services, singleton `PlatformDirectoryMap`, FluentValidation validators, camelCase string-enum JSON).
- `Api/` — vertical-slice minimal APIs (one static class per bounded context, `Map*Api` extension): `ProjectsApi`, `EntriesApi`, `SearchApi`, `KitsApi` (incl. activate/deactivate/drift), `SettingsApi`, `HealthApi`, `SystemApi` (localhost-only `browse-folder` for the UI folder picker, directory listing for the projects tree), `SystemRootsApi` (tree-root CRUD; removing a root that still contains projects returns 409), `PlanningFeaturesApi` and `PlanningArtifactsApi` (planning CRUD over the `Infrastructure/Planning` services), `PlanningWorkflowApi` (per-project planning-workflow settings resolved through `PlanningPolicy`), `PlanningMaterializationApi` (materialize + drift), and `AgentPlanningApi` (the agent door at `/api/agent/planning/*` — gated `validate` and `current` over the same human-door handlers). Handlers inject `OvermindDbContext` directly — sanctioned for v1, see ADR "vertical-slice minimal APIs". Contracts (`Api/Contracts/`), validators (`Api/Validation/`), entity→DTO mappers (`Api/Mapping/`), `HttpErrors` (ProblemDetails helpers), `PlanningErrors` (PlanningException → ProblemDetails).
- Tray/lifecycle at project root: `TrayApplication`/`TrayApp` (Avalonia STA loop), `SingleInstance`, `PortFile`, `OvermindPaths` (`~/.overmind`: `overmind.db`, `host.port`), `OvermindPorts` (default 4761, free-port fallback), `BrowserUri` (rewrites the bound loopback URL to `overmind.localhost` when opening the browser).

### `src/Overmind.Mcp` — stdio MCP adapter (standalone)

References **no other project** — a deliberate decoupling: the adapter is a thin HTTP→JSON-RPC proxy that deserializes Host DTOs via tolerant `JsonElement` reads. Contract is additive-only (see ADR). Console exe, assembly name `overmind-mcp`, built on the `ModelContextProtocol` C# SDK. Spawned by agents via their standard MCP config; exits with code 2 and a stderr hint if the Host API is unreachable at startup.

### `web/` — Next.js 16 static-export SPA

App Router, React 19, TypeScript strict, Tailwind 4, React Query 5, FSD layering. Built with `output: "export"` into `web/out/`, served by the Host from the same origin — there is **no Next.js server in production**, so all data access is client-side React Query over same-origin `/api` (see ADR "static-export SPA data access"). Brand/design reference: `web/brand-book.md`. Slices: entries, kits, projects, search, settings, health, planning (board + feature workspace).

## Communication rules

1. **Loopback only.** Kestrel binds `127.0.0.1` — default port 4761 with free-port fallback; override via `Overmind:BindUrl` config. No auth in v1; loopback binding is the security boundary (token auth is a P2+ candidate). Never bind a public interface without a new ADR.
2. **Port discovery via port file.** On startup the Host writes the bound port to `~/.overmind/host.port`. Consumers resolve the base URL in order: `OVERMIND_API_URL` env var → port file → default 4761. Single-instance relaunch uses the same file to find the running UI.
3. **The HTTP API is the only contract.** UI and MCP adapter never touch the DB, filesystem materialization, or Qdrant directly. Everything goes through `/api/*`. A distinction the domain gates on (agent vs human) is carried by the route prefix — `/api/agent/*` — never by a header or body parameter a caller could omit; see ADR "Two HTTP doors for one aggregate".
4. **API shape.** JSON, camelCase, enums as camelCase strings. Errors as RFC 7807 ProblemDetails (`HttpErrors`); validation failures as 400 `ValidationProblem`. Domain conflicts (overwrite-blocked materialization, second kit activation, DiskPath change while active) are 409 with an explanatory `detail`.
5. **Host↔MCP contract is additive-only.** New fields allowed; renames/removals forbidden (known hazard tolerated while single-repo — a Host DTO rename silently breaks the adapter).
6. **SPA routing.** Static export = one `.html` per route. The Host rewrites extension-less non-`/api` GET paths to `<path>.html` before static files, then falls back to `index.html`. Web root: `OVERMIND_WEB_ROOT` env var, else `../../web/out` relative to the content root.
7. **The MCP adapter bypasses the system proxy.** `OvermindApiClient` builds its `HttpClient` with `SocketsHttpHandler { UseProxy = false }` (`src/Overmind.Mcp/OvermindApiClient.cs:57`) — loopback is the security boundary, and a system proxy would carry Host traffic off-machine (see ADR, from `audits/audit-2026-09-10.md` BLOCKER-1). Infrastructure embedding providers deliberately keep proxy support: their base URLs (Ollama, OpenAI-compatible) may be remote.

## MCP server design (adapter surface)

Transport: stdio JSON-RPC via the ModelContextProtocol SDK. Server name `overmind`; server instructions tell agents to read `overmind://` resources first.

- **Tools** (`OvermindTools`): `list_projects`, `list_entries` (optional type filter), `read_entry`, `write_entry`, `update_entry` (content-only, title/tags preserved), `search` (`mode=fulltext|semantic`, semantic returns "vector search unavailable" when Qdrant is down). **Write scope is restricted to note/decision/gate/guard** — skills carry files and are managed via the UI until P2 runtime delivery; attempts to write a skill get an explanatory MCP error.
- **Planning tools** (`OvermindPlanningTools`): `planning_list_features`, `planning_read_feature`, `planning_current_task` (read-only), `planning_create_feature`, `planning_upsert_artifact`, `planning_set_status` (empty log note rejected client-side — status moves are recorded in the artifact log), `planning_append_log`, `planning_validate` (gated: a 409 from the human-validation gate reaches the agent verbatim as an error starting with `STOP:`). All planning calls go to the agent door `/api/agent/planning/*` only.
- **Resources** (`OvermindResources`), custom scheme `overmind://` (see ADR — renamed from `ctx://`):
  - `overmind://projects`, `overmind://projects/{id}` — JSON.
  - `overmind://projects/{id}/{gates|guards|notes|decisions|skills}` — concatenated markdown (`# Title` + body per entry).
  - `overmind://entries/{id}` — single entry as markdown.
- **Planning resources** (`OvermindPlanningResources`): `overmind://projects/{id}/planning/features` (board as markdown), `…/features/{slug}` (full feature: body + `## Spec`/`## Plan`/`## Tasks`/`## Result`), `…/features/{slug}/current` (current task, or a first-line `STOP:` / `NOTHING TO DO:`).
- **Prompts** (`SessionBootstrapPrompt`): session-bootstrap prompt teaching the agent to read gates/guards at session start and to run the planning loop (board → `current` → obey a STOP → report via `planning_set_status`/`planning_append_log`).
- **Error mapping:** 404 → `ResourceNotFound` protocol error; unreachable API / 503 semantic-down / other statuses → `McpException` with a human-readable message.

## Skill-kits and materialization (v1 mechanics)

Source of truth is the DB; **disk is a managed cache**. Agents load skills only from the filesystem, so activation writes native skill directories into the project folder.

- A `SkillKit` is a named, versioned set of entries (skills, gates, guards), linkable to projects many-to-many via `ProjectKit`.
- **Exactly one kit active per project** (ADR): activating a second kit returns 409 instructing to deactivate first; the UI offers a switch flow.
- Activate = `SkillMaterializer.MaterializeAsync`: for each platform found in the kit's skill files, writes `<native-skills-dir>/<skill-slug>/<file-relative-path>` plus a generated `overmind-bootstrap/SKILL.md` (points agents at `overmind://projects/<id>/gates` and `/guards`, inlines current gates/guards). Every written file gets a `MaterializedFile` row with a SHA-256 of normalized (LF, UTF-8 no BOM) content.
- **Overwrite protection:** a target path that exists on disk but is not tracked for this kit → 409 `OverwriteBlocked`. Writes run in a DB transaction; on failure, already-written files are best-effort deleted.
- Deactivate removes tracked files, prunes now-empty directories up to the project root, and deletes the rows. `DetectDrift` compares on-disk hashes against rows (Missing/Modified).
- **Project `DiskPath` is immutable while any kit is active or any `MaterializedFile` rows exist** (ADR) — dematerialize resolves relative paths under the current DiskPath, so retargeting mid-flight would delete foreign files. `PUT /api/projects/{id}` returns 409.
- Known debt (BACKLOG hardening): materialized skills land double-nested (`<kit-slug>/<skill-slug>/SKILL.md`); single-level layout decision pending — changing it means adjusting materializer + drift hashes.

## Planning (the ceremony as an entity)

Features, specs, plans, tasks and results live in the database as versioned artifacts with markdown+XML bodies; agents and the human UI drive the same aggregate. The database is the source of truth — the `BACKLOG/` folder on disk is a per-project materialized view (or an internal archive), never an input the app trusts.

- **Model** (`Core`): `PlanningFeature` (slug unique per project, body, status draft/ready/in-progress/done, version) and `PlanningArtifact` (kind spec/plan/task/result, one status enum with a per-kind vocabulary, task review level light/hard, `Order`, body, `FrozenAt`, body history in `PlanningArtifactVersion`). Editing a validated artifact requires a dated change note, snapshots the old body, and resets the artifact to draft — the frozen lifecycle is enforced on the entity, not in handlers.
- **Two doors, one aggregate.** The human/UI door is `/api/projects/{id}/planning/*` + `/api/planning/*`; the agent door is `/api/agent/planning/*`, the only surface the MCP adapter calls. Both map onto the same `internal static` handlers and the same `Infrastructure/Planning` services, so "one service path" holds by construction. The agent door exists for the validation gate: when the effective gate is on, the agent's `validate` returns 409 `HumanValidationRequired` with a `STOP:` detail and `current` reports a stop state, while the UI's validate always works (ADR "Two HTTP doors for one aggregate").
- **Gate resolution** (`Infrastructure/Planning/PlanningPolicy`): global settings `planning.requireHumanValidationDefault` and `planning.materializationDefault` (both default `off`) with per-project overrides (`unset/on/off`, `inherit/off/on/archive`); effective values are exposed and set via `GET|PUT /api/projects/{id}/planning/workflow` and mirrored in Settings + project pages in the UI.
- **Materialization.** `on` writes `BACKLOG/<feature-slug>/{feature,spec,plan,result}.md` + `tasks/NN-<slug>.md` into the project repo and maintains a managed block in the repo's `BACKLOG.md` between `<!-- overmind:planning:start/end -->` markers: hand-written content outside the markers is preserved byte-for-byte, and the tracked hash covers the block content only, so human index edits never read as drift. A target file that exists but is untracked → 409 unless `force` adopts it; tracked files are rewritten freely (a hand edit of a tracked file is transient — the next ceremony mutation rewrites it from DB truth; `force` gates only the adoption of foreign untracked files), and drift (Missing/Modified) is reported, never auto-repaired. `archive` writes the same layout under `<data dir>/projects/<project>/backlog/` with free rewrites; `off` deletes what was tracked. Every delete path is hash-guarded: a file is removed (or its managed block stripped) only when its normalized SHA-256 — block-only for `BACKLOG.md` — matches the tracked row, so foreign content sitting at a tracked path in either root is kept and surfaced as a report warning. A failed or refused sync never rolls back the DB write — problems surface as report warnings.
- **Importer.** Host CLI `--import-backlog <repoRoot|BACKLOG>` (optional `--project <id|name>`, else the single project whose `DiskPath` matches) parses frontmatter + XML bodies into features and artifacts; idempotent by `(ProjectId, Slug)` / `(Kind, Order)`, one transaction per feature, never blocks boot. Declared losses: `requirements.md` and the `BACKLOG.md` index are not read back, `amended:` frontmatter lines and the text after a `validated:` date are dropped (the date itself survives), and task files without a pure `NN-` digit prefix are skipped; unknown or legacy `status` values (e.g. `approved`) import as the kind's default rather than their historical meaning.
- **MCP planning surface**: eight tools (`planning_list_features`, `planning_read_feature`, `planning_current_task`, `planning_create_feature`, `planning_upsert_artifact`, `planning_set_status`, `planning_append_log`, `planning_validate`) and three markdown resources (`overmind://projects/{id}/planning/features`, `…/features/{slug}`, `…/features/{slug}/current`); the bootstrap prompt teaches the loop board → current → obey STOP → report.

## Storage, search, embeddings

- **SQLite (WAL)** at `~/.overmind/overmind.db` is the only metadata/document store. Migrations in Infrastructure, applied at Host startup. Entities: projects, entries, skill files, tags, entry versions, kits, kit links, project-kit links, materialized files, app settings, and the planning tables (`PlanningFeatures`, `PlanningArtifacts`, `PlanningArtifactVersions`, `PlanningMaterializedFiles` — migration `20260908173101_Planning`). `PlanningMaterializedFiles` deliberately has multiple cascade paths (`Project` direct and `Project → PlanningFeature → row`); SQLite accepts that, but a move to SQL Server would require `Restrict` on one path (comment in `OvermindDbContext`).
- **FTS5 always on.** Full-text search over all entry content, maintained by SQL triggers. No degradation path — it is the baseline.
- **Qdrant is the only vector backend, and it is optional.** Local Docker, explicit health check. Down → **honest degraded mode**: semantic search returns 503, UI shows a degraded banner, MCP `search` tells the agent to use `mode=fulltext`, the app never crashes.
- **Embedding provider is pluggable and selected in Settings** (`IEmbeddingProvider`): ONNX local (default; all-MiniLM-class model downloaded on first run into `~/.overmind/models/` with progress — keeps the install small, offline after first run), Ollama (URL-configurable), OpenAI-compatible (URL + key). Changing provider triggers reindexing (`ReindexService`).
- Entry indexing is write-through: the API calls `EntryIndexingService` on create/update/delete; indexing failures must not break CRUD.

## Development phases

- **001-overmind-mvp — shipped.** Tasks 01–08 + 075: scaffold → domain+persistence → core API → kits/materialization → embeddings/Qdrant → MCP adapter → web UI → tray host e2e; `ctx://`→`overmind://` rename. Hard architect review: `audits/architect-review-2026-09-07.md`.
- **004-projects-tree-view — shipped.** Sidebar tree of real disk folders (localhost-only `SystemApi` browse/list endpoints), inactive non-project nodes, register-in-place sync flow.
- **005-tree-workspace — shipped.** User-defined tree roots (`SystemRootsApi` add/remove with a project guard) + active-project context in the web UI (tree click → Entries of that project, session-persistent expansion).
- **006-planning — shipped.** The ceremony as a first-class entity: planning domain + `Planning` migration, `Infrastructure/Planning` services (incl. the off/on/archive materializer with drift guards), the human door, workflow settings, the agent door, the materialization endpoints, the idempotent `--import-backlog` CLI importer, the MCP planning surface (8 tools + 3 markdown resources + bootstrap loop), and the full web UI (`/planning` board + feature workspace with artifact/task editors, task board, log timeline, per-project workflow settings, drift panel; global defaults on the Settings page). Closeout ran the e2e dogfood on this repo's own BACKLOG against a temp database and a temp repo copy — import (R7 spot-checks), materialize on/force-adopt/drift, agent-door task cycle, human-validation gate on both doors, archive and off cleanup, and the real MCP adapter. 11/11 tasks; hard reviews on 01, 02, 05 and the closeout, light on 03/04/06–09. Spec/plan and task contracts in `BACKLOG/006-planning/`.
- **002-kit-library — planned.** Import kits from a local folder or git repo (incl. private repos with stored credentials); imported kits are first-class.
- **003-bundled-installer — blocked by 002.** Win/mac installer with curated pre-bundled kits riding the 002 import pipeline. Revisit the additive-only-contract ADR when separate distribution becomes real.
- **Deferred:** P1 external-folder materialization + agent launcher (`claude --add-dir`, `kimi --skills-dir`, spike for the rest); P2 runtime skill/gate/guard delivery via MCP (`get_skill` tool, `overmind://skills/` resources, universal bootstrap skill); P2/P3 agent onboarding flows. Hardening nits from reviews live in `BACKLOG.md` Deferred.

## Testing strategy

xUnit + Shouldly + NSubstitute; test projects mirror src and each references only its target. Current suite: 403 tests green (Core 85, Infrastructure 125, Api 134, Mcp 59).

- `Overmind.Core.Tests` — entity factories, domain behavior.
- `Overmind.Infrastructure.Tests` — EF roundtrips on temp SQLite DBs, FTS trigger sync, materialization into temp dirs, embedding providers, Qdrant config (live tests gated on availability).
- `Overmind.Api.Tests` — WebApplicationFactory integration (`Testing` environment skips single-instance/tray), incl. degraded-mode semantics, port resolution, single-instance behavior.
- `Overmind.Mcp.Tests` — spawns the real adapter process against a stub HTTP API; contract tests pin our JSON-RPC behavior (tools, resources, prompts, failure modes), not the SDK's.
