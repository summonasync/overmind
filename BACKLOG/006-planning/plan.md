---
feature: 006
slug: planning
status: validated
created: 2026-09-08
validated: 2026-09-17
---

## Shape

Ten sequential tasks, backend-first: domain → human API → settings → agent API → materialization → importer → MCP → web (read) → web (write) → closeout. Each task ends green on its own verification command; the UI is coded against real contracts that already exist. No new NuGet or npm packages anywhere (frontmatter parsing is hand-rolled, markdown rendering reuses `shared/lib/markdown`).

Two spec amendments were approved by the user on 2026-09-08 during plan drafting and are already written into `spec.md`: `PlanningFeature.Body`, `PlanningArtifact.Review`, and the split agent surface `/api/agent/planning/*`.

## Amendments (2026-09-08, after the hard review of task 01)

Source: `audits/architect-review-2026-09-08-006-01.md` (+ a parallel line-level code review). The sections below stay verbatim as validated; where an amendment conflicts with them, **the amendment wins**. Task files 02/04/05/06/09/10 were amended to match.

**A1 — ceremony mutations move into an application service (user-approved; supersedes the "Two doors, one handler set" bullet, the Task 02/04/05/06 descriptions and the "Module size" risk).**
Planning has three writers by design — the human door, the agent door and the CLI importer. That is literally the extraction trigger in the `vertical-slice minimal APIs` ADR ("a second write path for the same aggregate appears"), so the service layer is created now rather than deferred:
- `Infrastructure/Planning/PlanningFeatureService` — `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `ImportAsync` (4 public methods).
- `Infrastructure/Planning/PlanningArtifactService` — `UpsertAsync`, `SetStatusAsync`, `ValidateAsync`, `AppendLogAsync` (4 public methods).
- Both own the full mutation unit: load → domain method → `SaveChangesAsync` → `IPlanningMaterializer.SyncAsync` (swallow-and-log, so disk state can never fail a DB write) → translate failures. `SyncAsync` therefore has exactly two call sites in the codebase instead of one per handler, and no handler can forget it.
- Failure translation is centralized: a new `PlanningException` + `PlanningFailureKind` in `Core/Abstractions` (house pattern = `MaterializationException`), produced by the services from domain exceptions and from unique-index violations; the Host maps kinds → 404/409/400 in one helper, the importer maps them to log lines. No raw `DbUpdateException` escapes to any caller.
- API modules stay vertical slices: handlers resolve the project/slug/artifact, call a service, map the result. `AgentPlanningApi` still maps onto the same `internal static` handler delegates; the agent door keeps its two unique behaviors (gated `validate`, `current`).
- Because task 05 implements `IPlanningMaterializer`, task 02 registers a `NullPlanningMaterializer` placeholder that task 05 replaces and deletes.
- The ADR recording this (two doors + importer → service extraction) is written in task 10.

**A2 — the `<log>` timeline is projected by the API (supersedes the Task 02 route list and the Task 09 timeline description).** `PlanningArtifactResponse` gains `log: [{ date, text }]`, produced by `PlanningLog.Parse` in the mapper. Additive, so the Host↔MCP contract ADR holds. Task 09 consumes it — no TypeScript re-implementation of the grammar (two parsers for one grammar would drift).

**A3 — shared materialization primitives are extracted before task 05 (extends Task 05, narrows Task 06).** `Infrastructure/Materialization/` gains internal `ContentHash` (CRLF/CR→LF + UTF-8-no-BOM + SHA-256), `Slug` (one slugify) and `DirectoryPruner` (prune + try-delete); `SkillMaterializer` is refactored onto them behavior-identically (its tests are the safety net). The `feature:`/`slug:` frontmatter split rule lives in exactly one place (`BacklogNaming.SplitSlug`) consumed by both the materializer and the importer, because task 05's acceptance criterion is that the written layout round-trips through the importer byte-for-byte.

**A4 — the importer restores through the service, not around it (extends Task 06).** `BacklogImporter` parses markdown and calls `PlanningFeatureService.ImportAsync`, which uses the `Restore` factories added in task 01b (`Version`, `CreatedAt`, `UpdatedAt`, `FrozenAt` carried from frontmatter). No second slugify, no direct `DbSet` writes, no duplicate upsert logic. Task 06 consequently depends on task 02.

**A5 — version numbering: the planning convention is canonical, Entries stay as they are (records a MINOR finding; no code in 006).** A planning version row labelled N holds the body that was live at version N, and a fresh artifact is version 1. `ContextEntry.UpdateContent`/`CurrentVersion` are off by one against that convention (a fresh entry is version 0) — retrofitting them touches `EntryApiTests`, `EntryMapper` and the web badge, so it goes to `BACKLOG.md` hardening instead, and the convention itself is written into `STRUCTURE.md`/ADR in task 10.

**A6 — task 01 is followed by a fix task `01b-domain-schema-hardening` (adds a task, changes no other task's content).** Four BLOCKERs and five IMPORTANTs from the review are closed there, and the unshipped `Planning` migration is regenerated once with all schema deltas (nullable `PlanningMaterializedFile.FeatureId` for project-scoped rows, unique `(ArtifactId, Version)`, unique `(ProjectId, RelativePath)`, explicit values on the six planning enums) — verified safe because the migration was never applied to a real database.

## Cross-cutting decisions (apply to every task)

**Final data model** (one migration, task 01):

- `PlanningFeature { Id, ProjectId, Slug, Title, Body, Status, Version, CreatedAt, UpdatedAt }` — `Slug` is the folder name (`006-planning`), unique per project.
- `PlanningArtifact { Id, FeatureId, Kind, Status, Review, Body, Order, FrozenAt?, Version, CreatedAt, UpdatedAt }`.
- `PlanningArtifactVersion { Id, ArtifactId, Version, Body, CreatedAt }` — mirrors `EntryVersion` (internal factory, `ValueGeneratedNever`, unique `(ArtifactId, Version)`).
- `PlanningMaterializedFile { Id, ProjectId, FeatureId, RelativePath, Sha256, MaterializedAt }` — lives in `Core/Entities` per STRUCTURE rule 3 (do **not** repeat the `MaterializedFile`-in-Infrastructure debt). Used by both `on` and `archive` strategies.
- `Project` gains `PlanningMaterialization PlanningMaterialization` (default `inherit`) and `HumanValidationOverride RequireHumanValidation` (default `unset`) + a `SetPlanningWorkflow(materialization, validation)` method. `ProjectResponse` gains both fields — additive, so the Host↔MCP contract ADR holds.
- Enums (`Core/Enums`): `PlanningFeatureStatus { Draft, Ready, InProgress, Done }`, `PlanningArtifactKind { Spec, Plan, Task, Result }`, `PlanningArtifactStatus { Draft, Validated, Todo, InProgress, Review, Done, Blocked }`, `PlanningReviewLevel { Light, Hard }`, `PlanningMaterialization { Inherit, Off, On, Archive }`, `HumanValidationOverride { Unset, On, Off }`.
- One status enum with a per-kind allowed subset (`PlanningArtifact.AllowedStatuses(kind)`): Spec/Plan/Result → `draft|validated`; Task → `todo|in_progress|review|done|blocked`. Vocabulary violations throw from the entity, handlers map to 400/409.
- **Deliberate extra guard (not a domain invariant):** at most one Spec, one Plan and one Result per feature, and unique `(FeatureId, Kind, Order)` enforced by a DB index (doc kinds always `Order = 0`, so the index gives the singleton rule for free). This is an API/usability guard so the UI tabs stay unambiguous — the spec's "exactly two model-level invariants" is about the domain, and reviewers should read it that way.

**Domain behavior lives on entities** (`Core`), so both HTTP doors share one code path:

- `PlanningFeature.Create/Update(title, body)/SetStatus` — `Update` bumps `Version` when the current status is not `draft`.
- `PlanningArtifact.Create/UpdateBody(body, logNote)/SetStatus(status, logNote)/Validate/AppendLog(note)`; `IsFrozen => Status == Validated`.
- Editing a frozen artifact without a `logNote` throws; with one it appends a dated line to `<log>`, snapshots the old body into `PlanningArtifactVersion`, resets `Status = Draft` and clears `FrozenAt`.
- `AppendLog` mutates `<log>` only and does **not** bump `Version` (log lines are metadata, content versions are for bodies); it does touch `UpdatedAt`.
- `PlanningLog` (Core static): `Append(body, note)` inserts `- yyyy-MM-dd — note` before `</log>`, creating the section when absent; `Parse(body)` returns the lines for the UI timeline. Core owns `<log>` parsing *only* because a model invariant depends on it — all other markdown/frontmatter parsing stays in Infrastructure.

**Two doors, one handler set.** `Host/Api/PlanningFeaturesApi.cs`, `PlanningArtifactsApi.cs`, `PlanningWorkflowApi.cs` serve `/api/planning/*` and the project-scoped reads; `Host/Api/AgentPlanningApi.cs` maps `/api/agent/planning/*` onto the same `internal static` handler delegates, adding only what is agent-specific: `current` and the gated `validate`. Slice modules are split up-front to stay under the ~300-line ADR trigger.

**Effective settings** resolve in `Infrastructure/Planning/PlanningPolicy.cs` (2 public methods: `ResolveValidationGateAsync`, `ResolveMaterializationAsync`), reading `AppSettings` keys `planning.requireHumanValidationDefault` (`off` default) and `planning.materializationDefault` (`off` default).

**Archive root convention** stays in Host: `DependencyInjection` constructs `PlanningMaterializer` with `OvermindPaths.DataDirectory` passed in, so Infrastructure never duplicates the `~/.overmind` convention (recorded debt stays un-worsened).

**Frontmatter split rule** for `feature: 006` / `slug: planning`: if the part before the first `-` is all digits, it becomes `feature:` and the rest becomes `slug:`; otherwise both fields get the full slug. The importer applies the reverse.

## Task 01 — domain & persistence (`tasks/01-domain-persistence.md`) — review: hard

`Core/Entities`: the four entities above + the `Project` additions. `Core/Enums`: the six enums. `Core/PlanningLog.cs`. `Core/Abstractions`: `IPlanningMaterializer` (`SyncAsync`, `DetectDriftAsync`, `ClearAsync`), `PlanningDriftItem(ProjectId, FeatureSlug, RelativePath, DriftKind)` reusing the existing `DriftKind`, `PlanningMaterializationReport` + `PlanningMaterializedFileInfo`.
`Infrastructure/Persistence/OvermindDbContext`: DbSets, cascade `Project → PlanningFeature → PlanningArtifact → PlanningArtifactVersion`, unique `(ProjectId, Slug)`, unique `(FeatureId, Kind, Order)`, index on `PlanningMaterializedFile.ProjectId`.
Migration `Planning` via `dotnet ef migrations add` (generated, never hand-edited).
Tests: `Core.Tests/Entities/PlanningFeatureTests.cs`, `PlanningArtifactTests.cs`, `PlanningLogTests.cs` (status vocabulary per kind, frozen-edit reset + change-note requirement, version snapshotting, log append without version bump); `Infrastructure.Tests/CrudRoundtripTests.cs` extended with a planning roundtrip incl. cascade delete.

Verification: `dotnet build && dotnet test` green.

## Task 02 — ceremony API, human door (`tasks/02-planning-api.md`) — review: hard

`Host/Api/Contracts/PlanningContracts.cs`, `Validation/PlanningValidators.cs`, `Mapping/PlanningMapper.cs`, `PlanningFeaturesApi.cs`, `PlanningArtifactsApi.cs`; registered in `Program.cs` and DI (validators).

Routes:
- `GET /api/projects/{projectId}/planning/features` — board rows `{ slug, title, status, version, specStatus, planStatus, taskTotal, taskDone, updatedAt }`.
- `POST /api/projects/{projectId}/planning/features` `{ slug, title, body? }` → 201; duplicate slug → 409; slug shape → 400.
- `GET|PUT|DELETE /api/projects/{projectId}/planning/features/{slug}` — PUT takes `{ title?, body?, status? }` and applies the version-bump rule; DELETE cascades artifacts and versions.
- `POST /api/projects/{projectId}/planning/features/{slug}/artifacts` `{ kind, body, order?, review? }` → 201; second Spec/Plan/Result or a duplicate `(kind, order)` → 409.
- `GET /api/planning/artifacts/{id}`, `GET /api/planning/artifacts/{id}/versions`, `PUT /api/planning/artifacts/{id}` `{ body, logNote? }`, `DELETE /api/planning/artifacts/{id}`.
- `POST /api/planning/artifacts/{id}/status` `{ status, logNote }` (logNote required), `POST /api/planning/artifacts/{id}/log` `{ note }`, `POST /api/planning/artifacts/{id}/validate` (human door: always allowed; 409 for `kind = task`).
- PUT on a frozen artifact without `logNote` → 409 with an explanatory `detail`; response shows `status: draft` and cleared `frozenAt`.

Tests: `Api.Tests/PlanningApiTests.cs` — CRUD, slug conflict, version history, frozen-edit reset, missing change note, status vocabulary rejection, validate-a-task 409, board counters.

Verification: `dotnet build && dotnet test` green.

## Task 03 — workflow settings & global defaults (`tasks/03-planning-workflow.md`) — review: light

`Host/Api/PlanningWorkflowApi.cs`: `GET|PUT /api/projects/{projectId}/planning/workflow` → `{ materialization, requireHumanValidation, effectiveMaterialization, effectiveRequireHumanValidation }` (PUT accepts `inherit|off|on|archive` and `unset|on|off`, rejects anything else with a ValidationProblem).
`Infrastructure/Planning/PlanningPolicy.cs`; `SettingKeys` + `SettingDefaults` gain the two `planning.*` keys.
`SettingsApi`: `SettingsResponse` gains `planningMaterializationDefault` and `planningRequireHumanValidationDefault`; `UpdateSettingsRequest` accepts both (additive only).
Tests: extend `Api.Tests/SettingsApiTests.cs` (new defaults present, writable, validated) and add `PlanningWorkflowApiTests` covering inheritance: project `unset` follows the global, `on`/`off` override it, `inherit` materialization follows the global default.

Verification: `dotnet build && dotnet test` green.

## Task 04 — agent door (`tasks/04-agent-planning-api.md`) — review: light

`Host/Api/AgentPlanningApi.cs` under `/api/agent/planning`:
- reads: `GET .../projects/{projectId}/planning/features`, `GET .../features/{slug}`, `GET .../features/{slug}/current`.
- writes: `POST .../projects/{projectId}/planning/features`, `POST .../features/{slug}/artifacts` (**upsert** by `(kind, order)` — existing artifact gets a new body version, missing one is created), `POST .../artifacts/{id}/status`, `POST .../artifacts/{id}/log`, `POST .../artifacts/{id}/validate`.
- `validate` resolves the effective gate first: gate on → 409 `title: "HumanValidationRequired"`, `detail: "STOP: <kind> awaits human validation — ask the user to approve it in the Overmind UI."`; gate off → the shared validate handler.
- `current` returns `{ state: "task" | "stop" | "empty", feature: { slug, title, status }, artifact: {…} | null, reason: string | null }`. STOP when the gate is on and the spec is missing/draft, or the spec is validated but the plan is missing/draft. `empty` when there are no tasks, or all tasks are done; otherwise the first non-done task by `Order`.
- Handlers are shared with task 02 by making them `internal static` — no duplicated logic, no second write path.

Tests: `Api.Tests/AgentPlanningApiTests.cs` — upsert create-then-version, gated validate 409 (global on, project `unset`), ungated validate (project `off` beats global `on`), all three `current` states, board parity with the human door.

Verification: `dotnet build && dotnet test` green.

## Task 05 — materialization (`tasks/05-planning-materialization.md`) — review: hard

`Infrastructure/Planning/PlanningMaterializer.cs` (`IPlanningMaterializer`), `ManagedBlock.cs` (BACKLOG.md block read/write), plus DI registration with the Host-provided data directory.

- Strategy resolution per project via `PlanningPolicy`; `off` → `ClearAsync` (delete tracked files, prune empty dirs, drop rows).
- `on` → `<DiskPath>/BACKLOG/<feature-slug>/{feature,spec,plan}.md` + `tasks/NN-<slug>.md` (NN = zero-padded `Order`, slug from the first `# ` heading of the body, fallback `task-NN`), plus the managed block in `<DiskPath>/BACKLOG.md` between `<!-- overmind:planning:start -->` / `:end -->`. Files that exist but are untracked → `MaterializationException.Overwrite` (409), exactly like skills. Tracked paths are rewritten freely.
- The tracked row for `BACKLOG.md` stores the SHA-256 of **the managed block only**, so human edits outside the markers never show as drift. Missing markers → append the block; missing file → create it with a `# BACKLOG` heading.
- `archive` → `<dataDir>/projects/<project-slug>/backlog/<feature-slug>/…`, same layout, free rewrite, rows tracked, no drift guards.
- Drift detection runs for `on` only → `PlanningDriftItem` list (Missing/Modified).
- `GET /api/projects/{projectId}/planning/drift`, `POST /api/projects/{projectId}/planning/materialize` `{ force }` — `force: false` 409s on untracked foreign files, `force: true` adopts them.
- Wire `SyncAsync` into every planning write handler from tasks 02/04 (single call site per handler, after `SaveChangesAsync`) and into feature delete (`ClearAsync` for that feature).
- `ProjectsApi.Update`: extend the DiskPath-immutability guard to also count `PlanningMaterializedFile` rows (same hazard as the existing ADR — relative paths resolve under DiskPath).

Tests: `Infrastructure.Tests/PlanningMaterializerTests.cs` (temp dirs: on/archive/off roundtrip, layout + NN naming, managed block insert/append/preserve-foreign-content, block-only hashing, overwrite-blocked, drift Missing/Modified, clear prunes empty dirs); `Api.Tests/PlanningMaterializationTests.cs` (drift + materialize endpoints, DiskPath 409 while planning files are tracked).

Verification: `dotnet build && dotnet test` green.

## Task 06 — backlog importer (`tasks/06-backlog-importer.md`) — review: light

`Infrastructure/Planning/BacklogImporter.cs` (`ImportAsync`) + `BacklogMarkdown.cs` (frontmatter and XML-section parsing, hand-rolled, no YAML package).
Host `Program.cs`: `--import-backlog <path>` (+ optional `--project <id|name>`), executed after migrations and before `StartAsync`; the app still boots on import failure (logged, never fatal).
Behavior: `<path>` is a repo root containing `BACKLOG/` (or the `BACKLOG/` folder itself); the target project is resolved by normalized `DiskPath` equality, else by `--project`, else startup logs a clear error naming the candidates. Per feature folder: `feature.md` frontmatter → `Slug`/`Title`(from the H1)/`Status`/`Version`/`CreatedAt`, body → `Body`; `spec.md`/`plan.md` → artifacts (`validated:` frontmatter date → `FrozenAt`, status `validated`); `tasks/NN-*.md` → `Order = NN`, `Review` from frontmatter, status, `<log>` preserved verbatim inside the body. Idempotent by `(ProjectId, Slug)`: a second run updates in place and creates no duplicates.
Known accepted losses (documented in the task file and CONTEXT): `requirements.md` is ignored, BACKLOG.md index statuses are not read back into features.

Tests: `Infrastructure.Tests/BacklogImporterTests.cs` against a temp fixture tree — statuses, `FrozenAt`, task order from NN prefixes, log lines, review level, idempotent re-run, missing spec/plan tolerated.

Verification: `dotnet build && dotnet test` green.

## Task 07 — MCP surface (`tasks/07-mcp-planning-surface.md`) — review: light

`Mcp/OvermindPlanningTools.cs`: `planning_create_feature`, `planning_upsert_artifact`, `planning_set_status`, `planning_validate`, `planning_append_log` — all against `/api/agent/planning/*`, with `[Description]` text that teaches the ceremony order.
`Mcp/OvermindPlanningResources.cs`: `overmind://projects/{id}/planning/features` (compact markdown board), `…/features/{slug}` (full feature as markdown), `…/features/{slug}/current` (task body or the STOP text, rendered so an agent cannot miss it).
`SessionBootstrapPrompt`: extend the instruction text with the planning loop (read `planning/features` → take `current` → work → `set_status` + `append_log`).
Registration in `Mcp/Program.cs`; `OvermindApiClient` unchanged (existing GET/POST/PUT cover it).

Tests: `Mcp.Tests/StubOvermindApi.cs` gains the agent routes; new `PlanningToolContractTests.cs` + `PlanningResourceContractTests.cs` pinning our wire behavior, incl. the 409 → `McpException` STOP message and the `current` STOP rendering.

Verification: `dotnet build && dotnet test` green.

## Task 08 — web: planning slice + board (`tasks/08-web-planning-board.md`) — review: light

`web/src/features/planning/`: `types.ts`, `api.ts` (through `shared/api/client.ts` only), `queries.ts` (`planningKeys` factory + `queryOptions`), `lib/use-planning.ts` (`{ data, isPending, error, refetch }` hooks + mutations), `components/feature-board.tsx`, `components/feature-form.tsx`, `index.ts` barrel.
`pages/planning-page/`: `ui/planning-page.tsx` + `ui/planning-info-panel.tsx` + `index.ts`; route `app/planning/page.page.tsx`.
Routing follows the static-export quirk: `/planning?project=<id>` for the board, `&feature=<slug>` for the workspace, plain `<a href>`, `useSearchParams` inside `Suspense` (same as `entries-page`).
Sidebar (`widgets/app-shell/ui/app-sidebar.tsx`) and `mobile-nav.tsx` gain a Planning item that carries the last active project id like Entries does.
Board card: status chip, task progress `n/m done`, spec/plan validated chips; empty state points at "New feature".

Verification: `npm run lint && npx tsc --noEmit && npm run build` clean.

## Task 09 — web: workspace, editors, settings (`tasks/09-web-planning-workspace.md`) — review: light

`features/planning/components/`: `feature-workspace.tsx` (tabs Spec / Plan / Tasks / Result), `artifact-editor.tsx` (xl `Modal`, Write/Preview from `entry-editor`, Approve button, change-note prompt when the artifact is frozen), `task-board.tsx` (five status columns, cards, status-move buttons — no drag), `task-editor.tsx` (large body editor + `<log>` timeline rendered from `PlanningLog.Parse` output via the API), `workflow-settings.tsx`, `drift-panel.tsx`.
`pages/projects-page/ui/projects-page.tsx` renders `<PlanningWorkflowSettings projectId=… />` beside `ProjectDetail` — the page composes two features, so `features/projects` never imports `features/planning` (FSD rule).
`pages/settings-page`: the two global defaults in `settings-form.tsx` + `features/settings/{types,api}.ts`.
Drift panel: list of `PlanningDriftItem` + a Re-materialize action (`force` after an explicit confirm modal).

Verification: `npm run lint && npx tsc --noEmit && npm run build` clean; manual pass — create a feature, write a spec, Approve it, move a task through statuses, watch the log timeline grow.

## Task 10 — closeout: e2e, docs, review (`tasks/10-closeout-e2e-docs.md`) — review: hard

- e2e smoke against a **temp** database (`Overmind:DatabasePath`), never the user's `~/.overmind`: `--import-backlog` on this repo's own BACKLOG → verify R7 spot-checks (feature statuses, `FrozenAt` from `validated:`, task order from NN prefixes, `<log>` lines, `review:` levels) → flip materialization to `on` in a temp project copy → diff the written files against the originals → drive one task through the MCP adapter (`current` → `set_status` → `append_log`) → flip the gate on and confirm the 409 STOP.
- Docs in the same change: `CONTEXT.md` (planning section: model, two doors, materialization strategies, importer), `STRUCTURE.md` (new folders/files, module split rationale), `TECHNICAL-SOLUTIONS.md` (ADR: two HTTP doors for one aggregate — agent surface under `/api/agent/*`; note it satisfies the ADR extraction trigger), `BACKLOG.md` (006 → `[x]`, plus a Deferred line for FTS over planning bodies).
- Full verification: `dotnet build && dotnet test`, `npm run lint && npx tsc --noEmit && npm run build`, then `architect-review` (hard).

## Risks

- **Module size.** Planning touches a lot of routes; the four-module split plus shared `internal static` handlers is what keeps every file under the ~300-line ADR trigger. If `PlanningArtifactsApi` still grows past it, extract an application service and record it in the ADR rather than letting the slice bloat.
- **Managed block in a hand-written `BACKLOG.md`.** Hashing the block only (not the file) is what makes human edits outside the markers invisible to drift. Any bug here edits a user's real repo file — the importer/materializer tests must cover "foreign content preserved byte-for-byte".
- **Materialization writes into real repos.** `on` is opt-in per project and defaults to `off` globally; untracked existing files always 409 unless the user forces it. PathGuard is reused for every resolved path.
- **DiskPath retargeting.** Planning files create the same hazard as skill files, so the existing immutability guard is extended in task 05 — forgetting it would let a dematerialize/clear delete foreign files.
- **Mixed status enum.** One enum with a per-kind allowed subset keeps a single `Status` column as specced; the subset map is the thing reviewers should check first.
- **Static-export routing.** Query-param navigation only (`?project=&feature=`), no `next/link`, no dynamic segments — the upstream router is still broken.
- **Importer fidelity.** `feature:`/`slug:` frontmatter splitting and `validated:` date parsing are the fragile spots; the repo's own BACKLOG (001–006) is the corpus, and losses (`requirements.md`) are declared, not discovered later.

<log>
- 2026-09-17 — re-imported from BACKLOG/006-planning/plan.md
</log>
