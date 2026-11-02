# STRUCTURE — Project Layout & Conventions

How the repo is organized and the rules for writing code in it. System design lives in `CONTEXT.md`, decision rationale in `TECHNICAL-SOLUTIONS.md`, work tracking on the Overmind board + the materialized `BACKLOG/` tree.

## Repository layout

```
├── CONTEXT.md / STRUCTURE.md / TECHNICAL-SOLUTIONS.md
├── BACKLOG/                  # feature folders (nnn-slug/): feature.md, spec.md, plan.md, tasks/
├── ENTRIES/                  # entry projection (051, SIBLING of BACKLOG/ — the importer scans BACKLOG
│                             #   feature folders only): {type}/{kebab-title}-{id8}.md, one file per entry,
│                             #   tracked rows like the planning tree — hand edits refuse (033), force warns
├── audits/                   # skill-produced reports, named <skill>-*.md
├── FEATURELIST/              # field feedback from agents on other projects, materialized from Overmind notes;
│                              #   raw input for the board, not a decision record (README.md is the index)
├── build/                    # packaging: pack.sh/install.sh/run.sh (portable, unix), install.ps1/run.cmd (portable, win),
│                              #   dist-layout.env (shared crew-kit + folder-name contract), windows/ (Inno Setup channel:
│                              #   overmind.iss, pack-win.ps1, make-icon.ps1 + overmind.ico, smoke-installer.ps1)
├── src/
│   ├── Overmind.Core/        # domain: Entities/, Enums/, Abstractions/ — zero dependencies
│   ├── Overmind.Infrastructure/  # Persistence/, Search/, VectorSearch/, Embeddings/, Materialization/, Planning/, Wave/ (wave chat store: WaveChatService rooms/unread/watermarks + WaveChatRetentionSweeper), Configuration/, Agent/ (embedded agent: AgentLoop, AgentToolRegistry + Tools/ spec families, AgentPromptComposer snapshot, DelegatedApprovalGate, ConversationStore/ConversationMessageStore, EmbeddedAgentSessionRegistrar, AgentLoopbackClient), Llm/ (LlmChatClientFactory + cache, OpenAI-compatible), Grimoire/ (detection + integration for the external Grimoire companion, plus the agent-side GrimoireMemoryClient + GrimoireCompactionRunner), Migrations/
│   ├── Overmind.Host/        # composition root: Program.cs, DependencyInjection.cs, Api/ (slices incl. Planning*Api.cs + AgentPlanningApi.cs + GrimoireApi.cs + WaveChatApi.cs + AgentChatApi.cs (SSE chat) + ApprovalSettingsApi.cs), Loopback/ (LoopbackOrigin + EmbeddedAgentSessionHandler — the embedded agent's loopback HttpClient), AgentLoopHostedService.cs (embedded session register/end), GrimoireCompactionSweepService.cs (hourly memory self-compaction, Testing-skipped), WaveChatRetentionSweepService.cs (background purge, Wave:ChatRetentionDays), BacklogImportCli.cs (run-and-exit --import-backlog) + BacklogImportCommand.cs (boot-time import), tray+lifecycle at root
│   └── Overmind.Mcp/         # stdio MCP adapter, no project references — planning tools under Planning/:
│                             #   PlanningFeatureTools / PlanningArtifactTools / PlanningMaterializationTools
│                             #   (tool classes, one per domain), PlanningArtifactAliasResolver (alias→artifactId),
│                             #   PlanningToolResponses (shared response shaping, normalization, readbacks);
│                             #   Registration/ — the `install` CLI: managed mcpServers keys (overmind + grimoire),
│                             #   Json/TomlConfigUpsertEngine, InstallCommand/InstallOptions, onboard blocks
├── tests/                    # mirrors src: Overmind.Core.Tests, .Infrastructure.Tests, .Api.Tests (Host), .Mcp.Tests
└── web/                      # Next.js 16 static-export SPA (FSD)
```

Central package management: `Directory.Packages.props`; shared build props: `Directory.Build.props`. Pin new package versions centrally. Solution: `Overmind.sln`.

## Backend architecture rules

1. **Dependency direction is one-way:** `Core ← Infrastructure ← Host`. `Overmind.Mcp` references nothing and proxies over HTTP. Never add a reverse or skip-level reference (e.g. Core → Infrastructure, Infrastructure → Host). Test projects reference only their target project.
2. **Core stays pure.** Entities, enums, abstractions, domain DTOs. No `using Microsoft.*`, no EF annotations, no serialization attributes. Domain behavior goes on entities (factory methods, invariant guards) where practical. A cross-entity domain helper lives at the Core root only when a model invariant depends on it (`PlanningLog` was the first, `PlanningSections` the second — the patch/tick invariants parse and rewrite through it); parsing no invariant depends on stays in Infrastructure.
3. **Persistence entities live in `Core/Entities`.** (`MaterializedFile` currently sits in `Infrastructure/Materialization/` — recorded debt from the 2026-09-07 architect review, pending move or an explicit exception rule. Its planning twin `PlanningMaterializedFile` went to `Core/Entities` correctly, so the two are split across layers until the move happens.)
4. **API style: vertical-slice minimal APIs with EF Core in handlers** — one static class per bounded context under `Host/Api/`, `Map*Api()` extension registers the routes, handlers inject `OvermindDbContext` and services directly. Sanctioned for v1 by ADR; **extraction trigger:** a second write path for the same aggregate (e.g. P2 MCP tools) or a module growing past ~300 lines → extract application services. **The trigger has fired for planning** (three writers by design: human door, agent door, CLI importer) — ceremony mutations live in `Infrastructure/Planning/` services and handlers stay thin (ADR "Planning ceremony mutations live in `Infrastructure/Planning` services"). Every other context is still handler-direct.
5. **Per-request wiring is explicit:** contracts in `Api/Contracts/`, FluentValidation validators in `Api/Validation/`, entity→DTO mapping in `Api/Mapping/`, error shaping via `HttpErrors` (ProblemDetails). All DI registration goes through `Host/DependencyInjection.cs` (`AddOvermind`).
6. **Domain invariants with an ADR must hold regardless of entry point.** Known gap: "single active kit per project" is currently enforced only in `KitsApi.Activate` (recorded in the 2026-09-07 review) — any new write path must re-enforce it or move the guard into the domain first.
7. **Disk is a managed cache.** The DB is the source of truth; materialized files on disk are derived state tracked by `MaterializedFile` rows + SHA-256 hashes. Never treat files on disk as authoritative; reconcile via drift detection.
8. **Loopback is the security boundary.** Kestrel binds `127.0.0.1` only. Localhost-only endpoints (e.g. `system/browse-folder`) must never do anything a local user shouldn't be able to do.
9. **Host↔MCP contract is additive-only** (ADR). Renames/removals in Host DTOs silently break the adapter's tolerant `JsonElement` reads — additive changes only, or update both sides in the same change.
10. **Caller identity a gate depends on lives in the route prefix, never in a parameter.** When one surface must treat a caller differently (agent vs human), split the route prefix and map it onto the same `internal static` handlers: `/api/planning/*` (human door) and `/api/agent/planning/*` (agent door, the only MCP-called surface; gated `validate` + `current` are agent-specific). An actor flag in the body/query or a `X-Overmind-Actor` header would make the gate a parameter any caller can omit (ADR). New planning routes must be mapped on both doors deliberately — forgetting the agent mapping narrows the agent surface without a compile error.
11. **Canonical version numbering:** a version row labelled N holds the body that was live at version N, and a fresh aggregate is version 1. The planning entities follow it; `ContextEntry`/`EntryVersion` predate it and are off by one — tracked as hardening on the board, not to be "fixed" inside an unrelated task.
12. **Enum posture:** enums carry explicit values, and status vocabulary is enforced in the domain (`PlanningArtifact.AllowedStatuses(kind)` throws on violations) — no DB CHECK constraints. Trusted local SQLite file; revisit if the store ever becomes shared.
13. **The embedded agent dispatches over HTTP, never over services.** Every `Agent/Tools/*AgentTools` spec goes through `AgentLoopbackClient` → the same `/api/agent/*` doors the MCP adapter uses (`Host/Loopback/EmbeddedAgentSessionHandler` stamps `X-Overmind-Session`), so claim locks, gates and materialization hold by construction — a new tool that calls an `Infrastructure` service directly is wrong. Tool families register in `AgentToolRegistry` (duplicate names are a construction error); a human-gated tool declares `AgentToolCategory.HumanGated` and carries a `DelegatedInvoke` closure that threads `X-Overmind-Delegated-By` — the header is validated server-side against the conversation store (embedded role + caller session + user-role message in the project-scoped conversation), never trusted as sent. Wave tools take `projectId` per call — cross-project traffic needs no context pinning. Background services (`AgentLoopHostedService`, `GrimoireCompactionSweepService`) return immediately under the Testing environment.

## Planning ceremony (standing rules)

Feature 006 landed the planning bounded context whole (domain, both doors, workflow settings, materializer, importer, MCP surface, web UI). The rules below govern it; extend this shape, don't invent a parallel one. The generalizable conventions graduated into the architecture rules above (rule 10 = two doors, rule 11 = canonical version numbering, rule 12 = enum posture).

- **Model** (`Core/Entities`): `PlanningFeature` (slug unique per project, `Body`, `Status`, `Version`), `PlanningArtifact` (`Kind` ∈ spec/plan/task/result, `Status`, `Review`, `Order`, `Body`, `FrozenAt`, `Version`), `PlanningArtifactVersion` (unique `(ArtifactId, Version)`), `PlanningMaterializedFile` (nullable `FeatureId`, unique `(ProjectId, RelativePath)`). `Project` carries `PlanningMaterialization` + `RequireHumanValidation` and `SetPlanningWorkflow`.
- **One status enum, per-kind vocabulary:** `PlanningArtifact.AllowedStatuses(kind)` — spec/plan/result take `draft|validated`, tasks take `todo|in_progress|review|done|blocked`. Violations throw from the entity; no caller re-checks.
- **Frozen semantics live in the entity:** editing a `validated` artifact without a change note throws; with one it appends a dated `<log>` line, snapshots the old body into `PlanningArtifactVersion`, resets to `draft`, clears `FrozenAt`. Every body mutation snapshots and bumps `Version` (039 D4) — `UpdateBody`, `PatchSection`, `TickAc` and `AppendLog` alike; a tick no-op on an already-ticked item is the one exception (no snapshot, no bump).
- **Task `blockedBy` rides the artifact upsert (081):** the optional `blockedBy: [order, ...]` field on `planning_upsert_artifact` (task) / `planning_upsert_tasks` items declaratively replaces the task's edge set (empty array clears; unknown/self/duplicate order → 400, cycle → 400 naming the chain) — no add/remove dependency tools at this level, and an edge closes when the blocker task reaches `done|review`. Edge edits are metadata: they stage outside the body path, so a `blockedBy` re-upsert on a `validated` (frozen) task needs no change note — a deliberate exception to the frozen rule above (microbatch guard candidate if product ever wants notes on frozen edge changes).
- **`<log>` grammar is owned by Core** (`PlanningLog.Append`/`Parse` over `PlanningSections`) *only* because a domain invariant depends on it; every other markdown/frontmatter parse stays in Infrastructure. The API projects the parsed lines (`PlanningArtifactResponse.Log`) so no client re-implements the grammar.
- **Bodies enter canonical (`PlanningSectionCanonicalizer`, 105):** one-line `<tag>content</tag>` sections are expanded to the anchored multi-line form at every write entrance (`PlanningArtifactService.UpsertAsync`/`UpsertTasksAsync`, `PlanningFeatureService.ImportAsync`); the anchored-only parser contract (039) stays frozen. The invariant is convention, not structure — any NEW write path (a fourth door, a direct `PlanningArtifact.Create`/`UpdateBody` call) must canonicalize at the entrance or it stores inline sections again; the `PatchSectionNotFound` hint makes a miss self-diagnosing (one wasted round-trip, then heal). Feature bodies are excluded by design — they carry no section contract.
- **Artifact writes are priced (039 D9) — pick by cost, cheapest that fits:** one log line → `planning_append_log` (body not resent); correcting the newest own-door log line's text → `planning_edit_log` (same cost tier as an append, body not resent); one XML section or one AC item → `planning_patch_artifact` (`replace_section`/`tick_ac`, rest of the body byte-untouched); whole body → `planning_upsert_artifact`. The tier is stated verbatim in each tool description and the bootstrap prompt carries the routing line; a new write tool fits this ladder or it does not ship. Reads run the mirrored ladder (040 D3): `planning_list_artifacts` → cheapest read (metadata-only roster of one feature's artifacts, no bodies); `planning_read_artifact` with `sections` → cheapest read (named sections only, absent ones soft-reported in `missingSections`); plain `planning_read_artifact` → one artifact, full body; `planning_read_feature` → most expensive read, the whole feature with all artifacts.
- **All planning writes go through the service seam:** `Infrastructure/Planning/PlanningFeatureService` (`CreateAsync`/`UpdateAsync`/`DeleteAsync`/`ImportAsync`) and `PlanningArtifactService` (`UpsertAsync`/`SetStatusAsync`/`PatchAsync`/`ValidateAsync`/`AppendLogAsync`/`EditNewestLogAsync`/`DeleteAsync`). Feature bodies are editable but their `<log>` is append-only on both doors (guard on `PlanningFeature.Update`, 083 — closes the 078 gap for features); artifact log lines gained the narrow exception on 106 — the newest own-door line's text is correctable in place (`EditNewestLogAsync`), date and marker preserved, original snapshotted. A service owns the whole unit: load → domain method → `SaveChangesAsync` → materializer sync → failure translation. Handlers hold no EF writes and no invariant checks. Shared persistence guards (`IsUniqueViolation`, swallow-and-log sync/clear wrappers) live in `PlanningServiceGuard`; the `BacklogImporter` parses only and writes exclusively through `ImportAsync`.
- **Disk can never fail a DB write:** `SyncAsync`/`ClearAsync` are invoked through swallow-and-log wrappers; un-writable or un-removable files surface via `PlanningMaterializationReport.Warnings`. `IPlanningMaterializer` is `PlanningMaterializer` (scoped; the archive data directory is injected from Host, Infrastructure holds no `~/.overmind` literal).
- **Failure vocabulary:** services throw `PlanningException` (`Core/Abstractions`) carrying `Kind` (→ status: `NotFound` 404, `Gone` 410, `Duplicate`/`RuleViolation` 409, `InvalidStatus`/`InvalidInput` 400) and `Code` (`PlanningFailureCode`, whose `ToString()` **is** the ProblemDetails `title` on every branch — the `NotFound` branch passes it as `title:` to `HttpErrors.NotFound`, whose `"Not Found"` default stays for non-planning 404s). `Host/Api/PlanningErrors.Map` is the only translator. Never derive a title from message text; never let a raw `DbUpdateException` or domain exception reach a caller. A new failure is a `PlanningFailureCode` member + a factory, never a hand-written `title:` string.

## Backend code style

- C# 14+ / .NET 10: records for DTOs, pattern matching, `required`/init where it fits, file-scoped namespaces.
- **File per type**, filename matches the type. No `region` directives, no Hungarian notation.
- **No comments by default** — comments exist only for non-obvious decisions. Never restate the code.
- All user-facing messages, identifiers, and docs in English.
- No `async void`. Cancellation tokens flow through async APIs.
- No god-services: if a class accumulates >5 public methods, split it. Current ceiling is 9 (`PlanningArtifactService` — `PatchAsync` pushed it past the 039 guidance, later additions kept it there and `EditNewestLogAsync` (106) made the breach recorded fact; a patch-path extraction is the recorded split candidate, not to be done opportunistically); `PlanningFeatureService` is at 6, `SkillMaterializer` at 3.
- FluentValidation for request validation; Shouldly + NSubstitute + xUnit for tests. Manual mapping or mappers — no AutoMapper.
- EF Core migrations in `Overmind.Infrastructure/Migrations/`, generated, never hand-edited (the FTS5 trigger SQL in the initial migration is the documented exception).

## Test conventions

- Test project per source project, same root namespace + `.Tests`. Test file per subject type (`ProjectApiTests`, `MaterializationTests`).
- API integration via WebApplicationFactory with the `Testing` environment (skips single-instance and tray). Infrastructure tests use temp SQLite files / temp dirs — no shared state between tests.
- MCP contract tests spawn the real adapter process against a stub HTTP API and pin **our** wire behavior, not the SDK's.

## Frontend structure (`web/src`, FSD)

Layers and allowed import direction — **never import upward**:

```
app        → pages, widgets, features, shared   (Next routes, providers, globals)
pages      → widgets, features, shared          (page compositions, one per route)
widgets    → features, shared                   (app-shell, kit-workspace, degraded-banner, grimoire-banner)
features   → shared                             (domain slices: entries, kits, projects, search, settings, health, planning, grimoire, wave-chat, agent-chat)
shared     → (nothing above)                    (api client, ui primitives, lib, assets)
```

- **Features must not import other features.** (The 2026-09-07 violation — `features/projects` importing `browseFolder` from `features/system` — is fixed: the call lives in `shared/api/system.ts` and `features/system/` no longer exists. Stateless cross-cutting calls belong in `shared/api`, not behind a feature edge.)
- **Public APIs:** every feature and page slice has an `index.ts` barrel; import from the slice root (`@/features/kits`), not from its internals. `widgets/app-shell` got its `index.ts` barrel on 2026-09-19 (task 011/03 — info-panel family + provider); `degraded-banner` got its barrel on 2026-09-20 (task 025/02 — honest diagnostics); `kit-workspace` is still consumed via deep imports — recorded debt; when touching a widget, add its barrel and route imports through it.
- **Data access:** every `fetch` goes through `shared/api/client.ts` to same-origin `/api/*`. No exceptions — no direct fetch/axios anywhere else.
- **React Query pattern:** query-key factories + `queryOptions` live in `features/<domain>/queries.ts` (`entryKeys`, `kitKeys`, …). Hooks in `features/<domain>/lib/` return `{ data, isPending, error, refetch }`; mutations use `useMutation` calling the feature's `api.ts`, then `queryClient.invalidateQueries` with the factory keys. **No `useEffect` for data fetching** (browser APIs only).
- **No Server Actions, no Next server.** Static export (`output: "export"`) means the production app is fully static — see ADR "static-export SPA data access".
- **Routing/MPA quirk:** `pageExtensions` is `["page.tsx", ...]`, so route files are named `page.page.tsx`. Navigation uses **plain `<a href>`** (sidebar, mobile nav, links) — the Next 16 static-export client router is broken upstream; full-page loads between routes are the accepted behavior. Do not introduce `next/link`/`useRouter` without verifying upstream is fixed. Exception by design: board filters keep state in React and write the URL via `window.history.replaceState` + a `popstate` listener (`features/planning/lib/use-board-filters.ts`), so shareable filter URLs work without the client router.
- `'use client'` only where interactivity/hooks require it; RSC by default. TypeScript strict, no `any`.
- Brand/design tokens: `web/brand-book.md` is the source of truth for palette, typography, and assets (`shared/assets/brand/`).

## AI agent guidelines

- Read `CONTEXT.md` (this system's design), the planning board (the live index — web UI or MCP), and the feature folder before changing anything; check `TECHNICAL-SOLUTIONS.md` before proposing an alternative to an existing pattern. Accepted ADRs are changed only by a new superseding ADR — never a silent edit.
- Complex work follows `BACKLOG/nnn-slug/` task files; executing agents read only their `tasks/NN-foo.md`; accepted spec/plan are read-only snapshots.
- No auto-commits. Report changed files; the user commits.
- New third-party packages need license vetting (OSI-approved) and central version pinning before adding.
- Structural nits discovered but not fixed get recorded (BACKLOG hardening / audit), not silently dropped or silently "fixed" mid-task.

## Do / Don't

**Do**
- Keep Core dependency-free; push framework concerns to Infrastructure/Host.
- Add new endpoints as vertical slices in the matching `Host/Api/*Api.cs` module with contract + validator + mapper.
- Return ProblemDetails via `HttpErrors`; use 409 for domain conflicts with an explanatory `detail`.
- Track every materialized file in `MaterializedFile` with a normalized-content SHA-256.
- Centralize query keys in `features/*/queries.ts`; reuse `queryOptions` across widgets.
- Update `CONTEXT.md`/`STRUCTURE.md` in the same change that alters what they describe.

**Don't**
- Don't add project references that violate `Core ← Infrastructure ← Host` or make Mcp reference anything.
- Don't put EF/serialization attributes or `using Microsoft.*` in Core.
- Don't bypass `IMaterializer` to write into project skill directories.
- Don't write planning rows outside the `Infrastructure/Planning` services, and don't translate a planning failure anywhere but `PlanningErrors.Map`.
- Don't bind Kestrel beyond loopback, and don't remove or rename Host DTO fields consumed by the MCP adapter (additive-only contract).
- Don't fetch outside `shared/api/client.ts`, don't use `useEffect` for data, don't import across or upward between FSD slices.
- Don't add `next/link`/`useRouter` navigation while the static-export router is broken upstream.
- Don't write Server Actions or anything that assumes a Next.js server exists in production.
