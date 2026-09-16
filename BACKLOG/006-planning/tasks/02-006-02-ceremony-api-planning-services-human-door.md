---
feature: 006
task: 02
slug: planning
status: done
review: hard
created: 2026-09-08
---

# 006/02 — Ceremony API + planning services (human door)

<goal>
The whole ceremony is drivable over HTTP for a human/UI caller, and every planning mutation funnels through one application-service seam that the agent door (task 04) and the CLI importer (task 06) reuse: feature CRUD with a board projection, artifact upsert with body version history, status moves with a mandatory log note, log appends, and validation (Approve) — with the frozen-artifact semantics and all failure translation enforced below the handlers.
</goal>

<context>
Tasks 01 and 01b landed the domain: `PlanningFeature` (`Create`, `Update(title?, body?)` with null = untouched, `SetStatus`, `Restore`), `PlanningArtifact` (`Create`, `UpdateBody(body, logNote?)`, `SetStatus(status, logNote?)`, `Validate`, `AppendLog`, `Restore`, `AllowedStatuses`, `IsFrozen`), `PlanningArtifactVersion`, `PlanningMaterializedFile` (nullable `FeatureId`), the six enums with explicit values, and `PlanningLog` with line-anchored `<log>` detection. Read `BACKLOG/006-planning/tasks/01b-domain-schema-hardening.md` `<log>`/`<obstacles>` for the final signatures before coding against them.

Plan amendment **A1** (approved by the user 2026-09-08) puts ceremony mutations in `Infrastructure/Planning` services, because planning has three writers — human door, agent door, CLI importer — which is the extraction trigger of the "vertical-slice minimal APIs" ADR. API modules stay thin vertical slices: resolve → call a service → map. Amendment **A2** adds a `log` projection to the artifact response so the web UI never re-parses the `<log>` grammar.

House patterns to follow: `EntriesApi.cs` / `KitsApi.cs` for slice shape and error mapping (`KitsApi.MaterializationError` maps a `MaterializationException.Kind` to a status code — do the same for `PlanningException`), `HttpErrors` for ProblemDetails, `DependencyInjection.AddOvermind` for registration. Infrastructure services already take `ILogger<T>` (`ReindexService`, `EntryIndexingService`) — do the same here.

Materialization is implemented in task 05, so this task registers a `NullPlanningMaterializer` placeholder (writes nothing, returns empty reports) that task 05 replaces and deletes.
</context>

<scope>
**Core — one error vocabulary** (`src/Overmind.Core/Abstractions/`):
- `PlanningFailureKind { NotFound, Duplicate, RuleViolation, InvalidStatus }`.
- `PlanningException` shaped like the existing `MaterializationException` (`Kind` + `Message` + private ctor + static factories): `MissingProject`, `MissingFeature`, `MissingArtifact`, `DuplicateSlug`, `DuplicateArtifact`, `InvalidSlug`, `ChangeNoteRequired`, `CannotValidateTask`, `InvalidStatusForKind`.

**Infrastructure — the service seam** (`src/Overmind.Infrastructure/Planning/`):
- `PlanningFeatureService(OvermindDbContext db, IPlanningMaterializer materializer, ILogger<PlanningFeatureService> logger)` — three public methods in this task (task 06 adds `ImportAsync` as the fourth):
  - `CreateAsync(Guid projectId, string slug, string title, string body, CancellationToken ct)` → `PlanningFeature`; validates slug shape (`^[a-z0-9]+(-[a-z0-9]+)*$`, ≤ 64) → `InvalidSlug`; duplicate `(ProjectId, Slug)` → `DuplicateSlug`.
  - `UpdateAsync(Guid projectId, string slug, string? title, string? body, PlanningFeatureStatus? status, CancellationToken ct)` → `PlanningFeature`; null fields are untouched.
  - `DeleteAsync(Guid projectId, string slug, CancellationToken ct)` → calls `materializer.ClearAsync(projectId, featureId, ct)` **before** removing rows (after the cascade the tracked rows are gone and the files would be uncleanable orphans), then removes + saves + syncs.
  - Missing project → `MissingProject`; missing feature → `MissingFeature`.
- `PlanningArtifactService(OvermindDbContext db, IPlanningMaterializer materializer, ILogger<PlanningArtifactService> logger)` — five public methods (the house rule splits at more than five; this is at the line, do not add a sixth here):
  - `UpsertAsync(Guid featureId, PlanningArtifactKind kind, string body, int? order, PlanningReviewLevel? review, string? logNote, CancellationToken ct)` → `PlanningArtifact`. Upsert key is `(kind, order)`: document kinds (`Spec`/`Plan`/`Result`) always use order 0, so a second one is `DuplicateArtifact`; tasks default to `max(existing task orders) + 1` when `order` is null. Existing artifact → `UpdateBody(body, logNote)` (new version, frozen rules apply); missing → `PlanningArtifact.Create`.
  - `SetStatusAsync(Guid artifactId, PlanningArtifactStatus status, string logNote, CancellationToken ct)` — `logNote` required (empty → `ChangeNoteRequired`).
  - `ValidateAsync(Guid artifactId, CancellationToken ct)` — `CannotValidateTask` for a task.
  - `AppendLogAsync(Guid artifactId, string note, CancellationToken ct)`.
  - `DeleteAsync(Guid artifactId, CancellationToken ct)`.
- Both services own the full mutation unit: load → domain method → `SaveChangesAsync` → `materializer.SyncAsync(projectId, force: false, ct)` wrapped so a `MaterializationException` is logged as a warning and never fails the DB write (its message goes into the report/log, not into the HTTP response) → translate failures: domain `InvalidOperationException` → the matching `PlanningException` kind, `ArgumentOutOfRangeException` from `SetStatus` → `InvalidStatusForKind`, `DbUpdateException` on a unique index → `Duplicate*`. No raw EF or domain exception may escape a service method.
- `NullPlanningMaterializer` (internal sealed, `IPlanningMaterializer`) — no-op placeholder until task 05.

**Host — contracts, validation, mapping, routes:**
- `Api/Contracts/PlanningContracts.cs`:
  - `PlanningFeatureBoardItem(string Slug, string Title, PlanningFeatureStatus Status, int Version, PlanningArtifactStatus? SpecStatus, PlanningArtifactStatus? PlanStatus, int TaskTotal, int TaskDone, DateTime UpdatedAt)`
  - `CreatePlanningFeatureRequest(string Slug, string Title, string? Body)`
  - `UpdatePlanningFeatureRequest(string? Title, string? Body, PlanningFeatureStatus? Status)`
  - `PlanningFeatureResponse(string Slug, string Title, string Body, PlanningFeatureStatus Status, int Version, DateTime CreatedAt, DateTime UpdatedAt, PlanningArtifactResponse[] Artifacts)`
  - `CreatePlanningArtifactRequest(PlanningArtifactKind Kind, string Body, int? Order, PlanningReviewLevel? Review, string? LogNote)`
  - `UpdatePlanningArtifactRequest(string Body, string? LogNote)`
  - `SetPlanningArtifactStatusRequest(PlanningArtifactStatus Status, string LogNote)`
  - `AppendPlanningLogRequest(string Note)`
  - `PlanningArtifactResponse(Guid Id, PlanningArtifactKind Kind, PlanningArtifactStatus Status, PlanningReviewLevel Review, int Order, string Body, DateTime? FrozenAt, int Version, DateTime CreatedAt, DateTime UpdatedAt, PlanningLogLineResponse[] Log)` + `PlanningLogLineResponse(string? Date, string Text)` — **A2**: the mapper fills `Log` from `PlanningLog.Parse(body)`, `Date` as `yyyy-MM-dd` or null.
  - `PlanningArtifactVersionResponse(int Version, string Body, DateTime CreatedAt)`
- `Api/Validation/PlanningValidators.cs` — request-level pre-checks only (title required ≤ 200, body non-empty, `Order` ≥ 0, `LogNote`/`Note` non-whitespace). Slug shape and all invariants live in the services, not here.
- `Api/Mapping/PlanningMapper.cs` — entity → response, artifacts ordered by `Kind` then `Order`, `Log` from `PlanningLog.Parse`.
- `Api/PlanningErrors.cs` — one static `Map(PlanningException)` → `HttpErrors` result: `NotFound` → 404, `Duplicate`/`RuleViolation` → 409 (with a `title` per kind: `ChangeNoteRequired`, `CannotValidateTask`, `DuplicateSlug`, `DuplicateArtifact`), `InvalidStatus`/`InvalidSlug` → 400.
- `Api/PlanningFeaturesApi.cs` (`MapPlanningFeaturesApi`), handlers `internal static` so task 04 can map them again:
  - `GET /api/projects/{projectId:guid}/planning/features` → board (`TaskDone` counts tasks with status `Done`).
  - `POST /api/projects/{projectId:guid}/planning/features` → 201 `PlanningFeatureResponse`.
  - `GET|PUT|DELETE /api/projects/{projectId:guid}/planning/features/{slug}`.
  - `GET /api/planning/features/{featureId:guid}/artifacts` is NOT needed — the full feature response carries artifacts.
- `Api/PlanningArtifactsApi.cs` (`MapPlanningArtifactsApi`):
  - `POST /api/projects/{projectId:guid}/planning/features/{slug}/artifacts` → upsert through `PlanningArtifactService.UpsertAsync` → 201 when created, 200 with the new version when updated (the response body says which via `version`).
  - `GET /api/planning/artifacts/{id:guid}`, `GET /api/planning/artifacts/{id:guid}/versions`, `PUT /api/planning/artifacts/{id:guid}`, `DELETE /api/planning/artifacts/{id:guid}`.
  - `POST /api/planning/artifacts/{id:guid}/status`, `.../log`, `.../validate` (human Approve: no gate check here — the gate is the agent door's job in task 04).
- Wiring: register both `Map*Api` calls in `Program.cs`, the validators, `PlanningFeatureService`, `PlanningArtifactService` and `IPlanningMaterializer` → `NullPlanningMaterializer` in `DependencyInjection.AddOvermind`.

**Tests**
- `tests/Overmind.Infrastructure.Tests/PlanningServiceTests.cs` — the seam itself, on a temp DB with a stub materializer (NSubstitute): create/duplicate slug, update with null fields untouched, delete clears materialization **before** the rows go (assert `ClearAsync` was called with the feature id), upsert create-then-version, task order auto-increment, second spec → `DuplicateArtifact`, frozen `UpdateBody` without a note → `ChangeNoteRequired`, `ValidateAsync` on a task → `CannotValidateTask`, invalid status → `InvalidStatusForKind`, and `SyncAsync` throwing → the mutation still succeeds and is saved.
- `tests/Overmind.Api.Tests/PlanningApiTests.cs` (WebApplicationFactory via `ApiFactory`; create a project with a temp `DiskPath` first) — board shape and counters; create feature 201 / duplicate slug 409 / bad slug 400; full feature with artifacts ordered; unknown project/slug/artifact → 404; feature update version bump only after leaving `draft`; artifact upsert created-vs-versioned; `PUT` body → `version` increments and `GET .../versions` returns the previous body; `log` projection present with parsed dates; validate spec → `validated` + `frozenAt`; validate task → 409; frozen edit without `logNote` → 409 `ChangeNoteRequired`, with it → 200 `draft` + `frozenAt` null + note in `<log>`; status move with note persisted; spec → `done` → 400; append log → body updated, `version` unchanged; deletes → 204 then 404.
</scope>

<anti-scope>
- No agent door (`/api/agent/planning/*`) — task 04.
- No workflow/materialization settings endpoints and no `PlanningPolicy` — task 03.
- No real materializer: `NullPlanningMaterializer` only, and no disk writes anywhere in this task (task 05).
- No `ImportAsync` on the feature service and no importer (task 06).
- No MCP, no web.
- Do not grow either service past five public methods; if a sixth is genuinely needed, stop and record it in `<obstacles>` instead of splitting the service mid-task.
</anti-scope>

<constraints>
- `dotnet build && dotnet test` fully green (task 01b's suite included).
- No new NuGet packages. English only. No comments unless non-obvious. File per type.
- Handlers contain no EF writes and no invariant checks: everything mutating goes through a service, everything translating goes through `PlanningErrors.Map`.
- Host↔MCP contract stays additive-only: do not rename or remove any existing DTO field.
- Each API module stays under ~300 lines; each service at or below five public methods.
- No git commits — report the changed-file list.
</constraints>

<acceptance-criteria>
- [x] Both services exist, are registered, and are the only place planning rows are written.
- [x] `SyncAsync` is called from the services (two call sites total) and a materializer failure never fails the write.
- [x] Feature delete clears materialization before the rows cascade away.
- [x] All failures reach the client as ProblemDetails via `PlanningErrors.Map` — no raw `DbUpdateException`, no domain exception text leaking.
- [x] Frozen-artifact edit requires a change note (409 without), resets to draft and clears `frozenAt` with one.
- [x] Artifact responses carry the `log` projection parsed server-side.
- [x] Body edits create version rows; log appends do not.
- [x] Board counters are correct; every route 404s cleanly for unknown project/slug/artifact.
</acceptance-criteria>

<refs>
- @../plan.md — Amendments A1/A2 + Task 02
- @../spec.md — "Lifecycle & gates", Requirements R1/R2
- @./01b-domain-schema-hardening.md — final domain signatures and exception contract
- audits/architect-review-2026-09-08-006-01.md — IMPORTANT-6/7 (why the seam and the projection exist)
- src/Overmind.Host/Api/{EntriesApi,KitsApi,HttpErrors}.cs, src/Overmind.Host/DependencyInjection.cs
- src/Overmind.Core/Abstractions/MaterializationException.cs — the exception-shape precedent
- STRUCTURE.md — backend architecture rules 4–6
</refs>

<obstacles>
- `PlanningException.MissingFeature` takes a `string identifier` (not just slug) so both the feature service (passing slug) and artifact service (passing featureId.ToString()) can use it. The task contract listed it without specifying parameter type.
- `NullPlanningMaterializer` is `public sealed` (not `internal`) because the composition root in `Overmind.Host` registers it via DI and has no `InternalsVisibleTo` from Infrastructure.
- `PlanningFeatureService` is a `partial class` only because of the `[GeneratedRegex]` for slug validation — the regex is a static member on the class.
- The `UpdateArtifact` handler in `PlanningArtifactsApi` loads the existing artifact to get its `FeatureId`/`Kind`/`Order`, then calls `UpsertAsync` with those values. After the service mutates, it reloads via `Entry.ReloadAsync`. This means the PUT handler does one extra read compared to a direct domain call — acceptable for the thin-handler pattern.
- The `CreateArtifact` handler returns 201 for version 1 (new) and 200 for version > 1 (upserted existing). The task contract says "201 when created, 200 with the new version when updated" — `Version == 1` is the proxy for "just created" since a fresh artifact starts at version 1.
- ~~`PlanningErrors.ResolveTitle` derives ProblemDetails titles from exception message content~~ — **superseded 2026-09-10** (audit `audits/audit-2026-09-10.md`, WARNING-2). `PlanningException` now carries a `Code` (`PlanningFailureCode`, `Core/Abstractions/PlanningFailureCode.cs`) alongside `Kind`: `Kind` selects the HTTP status, `Code.ToString()` **is** the ProblemDetails `title`. `ResolveTitle` and its message matching are deleted. Consequence for task 04: add a `PlanningFailureCode.HumanValidationRequired` member and pass it to a new factory — do **not** add a message-matching branch. Note the 400 titles changed from the prose `"Invalid status"` to the machine codes `InvalidSlug` / `InvalidStatusForKind`; nothing asserted the old value. `DuplicateArtifact` is unreachable through the human door (a second `spec` upserts to 200 + version 2 per A1) and only fires on a unique-index race, so it has no API-level title test.
- `UpsertAsync` also applies `review` to an existing artifact via `PlanningArtifact.SetReview`. **Intentional, user decision 2026-09-10** (audit WARNING-3): kept as-is and pinned by `PlanningServiceTests.UpsertArtifact_ExistingWithReview_UpdatesReviewWithoutLeavingATrace`. Accepted trade-off: a review-level change leaves **no** version row (versions snapshot `Body` only) and **no** automatic `<log>` line — the log carries only the caller's `logNote`. If review level ever becomes user-editable in the UI (task 09), revisit whether it needs its own traceable path.
- Feature version bumps only when `Update(title, body)` is called while `Status != Draft`. `SetStatus` alone does not bump version. This follows the domain entity's design from task 01b.
- `CancellationToken` parameters on both services default to `default` so test code can omit them.
</obstacles>

<log>
2026-09-08 — task created from the validated plan.
2026-09-08 — amended for plan A1 (services + `PlanningException` + `NullPlanningMaterializer`, artifact delete added to the artifact service, `ImportAsync` deferred to task 06) and A2 (`log` projection); the original "no application-service extraction" anti-scope line is withdrawn.

2026-09-08 — executed. Changes by file:

**src/Overmind.Core/Abstractions/PlanningFailureKind.cs** — new enum: `NotFound`, `Duplicate`, `RuleViolation`, `InvalidStatus`.

**src/Overmind.Core/Abstractions/PlanningException.cs** — new exception shaped like `MaterializationException`: `Kind` + `Message` + private ctor + 9 static factories (`MissingProject`, `MissingFeature`, `MissingArtifact`, `DuplicateSlug`, `DuplicateArtifact`, `InvalidSlug`, `ChangeNoteRequired`, `CannotValidateTask`, `InvalidStatusForKind`).

**src/Overmind.Infrastructure/Planning/NullPlanningMaterializer.cs** — public sealed no-op `IPlanningMaterializer` placeholder for task 05 to replace.

**src/Overmind.Infrastructure/Planning/PlanningFeatureService.cs** — 3 public methods: `CreateAsync`, `UpdateAsync`, `DeleteAsync`. Slug validation via `[GeneratedRegex]`. Delete clears materialization before removing rows. `SyncAsync` wrapped in try/catch (logs warning, never fails write).

**src/Overmind.Infrastructure/Planning/PlanningArtifactService.cs** — 5 public methods: `UpsertAsync`, `SetStatusAsync`, `ValidateAsync`, `AppendLogAsync`, `DeleteAsync`. Upsert resolves order (doc kinds → 0, tasks → max+1 when null). Translates domain exceptions to `PlanningException`. `SyncAsync` wrapped same as feature service.

**src/Overmind.Host/Api/Contracts/PlanningContracts.cs** — all DTOs: board item, create/update feature requests, feature response, create/update artifact requests, set-status/append-log requests, artifact response with `log` projection (A2), version response.

**src/Overmind.Host/Api/Validation/PlanningValidators.cs** — 6 FluentValidation validators (request-level pre-checks only).

**src/Overmind.Host/Api/Mapping/PlanningMapper.cs** — entity → response mapping. Artifacts ordered by Kind then Order. Log projection from `PlanningLog.Parse`.

**src/Overmind.Host/Api/PlanningErrors.cs** — single static `Map(PlanningException)` → ProblemDetails. `NotFound` → 404, `Duplicate`/`RuleViolation` → 409 (title derived from message), `InvalidStatus` → 400.

**src/Overmind.Host/Api/PlanningFeaturesApi.cs** — `MapPlanningFeaturesApi`, all handlers `internal static`. 5 routes.

**src/Overmind.Host/Api/PlanningArtifactsApi.cs** — `MapPlanningArtifactsApi`, all handlers `internal static`. 8 routes.

**src/Overmind.Host/DependencyInjection.cs** — registered `PlanningFeatureService`, `PlanningArtifactService`, `IPlanningMaterializer → NullPlanningMaterializer`, 6 validators.

**src/Overmind.Host/Program.cs** — added `MapPlanningFeaturesApi()` and `MapPlanningArtifactsApi()`.

**tests/Overmind.Infrastructure.Tests/PlanningServiceTests.cs** — 19 tests: create/duplicate/invalid slug, missing project, update null untouched, status change version bump, delete clears materialization before rows, upsert create-then-version, task auto-increment, frozen without note, validate task, invalid status, empty note, sync throwing, delete artifact, append log.

**tests/Overmind.Api.Tests/PlanningApiTests.cs** — 24 tests: board empty/counters, create 201/dup 409/bad slug 400, unknown slug/project 404, ordered artifacts, version bump rules, upsert 201/200, PUT version+versions, log projection, validate spec/task, frozen edit with/without note, status move, spec→done 400, append log, delete artifact/feature 204→404, unknown artifact 404.

**Route table:**
| Method | Path | Success | Error |
|--------|------|---------|-------|
| GET | /api/projects/{projectId}/planning/features | 200 | 404 unknown project |
| POST | /api/projects/{projectId}/planning/features | 201 | 409 dup slug, 400 bad slug |
| GET | /api/projects/{projectId}/planning/features/{slug} | 200 | 404 unknown project/slug |
| PUT | /api/projects/{projectId}/planning/features/{slug} | 200 | 404 unknown project/slug |
| DELETE | /api/projects/{projectId}/planning/features/{slug} | 204 | 404 unknown project/slug |
| POST | /api/projects/{projectId}/planning/features/{slug}/artifacts | 201/200 | 404 unknown feature, 409 dup artifact |
| GET | /api/planning/artifacts/{id} | 200 | 404 |
| GET | /api/planning/artifacts/{id}/versions | 200 | 404 |
| PUT | /api/planning/artifacts/{id} | 200 | 404, 409 ChangeNoteRequired |
| DELETE | /api/planning/artifacts/{id} | 204 | 404 |
| POST | /api/planning/artifacts/{id}/status | 200 | 400 invalid status, 409 ChangeNoteRequired |
| POST | /api/planning/artifacts/{id}/log | 200 | 404 |
| POST | /api/planning/artifacts/{id}/validate | 200 | 409 CannotValidateTask |

**Service public signatures:**
- `PlanningFeatureService(OvermindDbContext, IPlanningMaterializer, ILogger<PlanningFeatureService>)`
  - `Task<PlanningFeature> CreateAsync(Guid projectId, string slug, string title, string body, CancellationToken ct = default)`
  - `Task<PlanningFeature> UpdateAsync(Guid projectId, string slug, string? title, string? body, PlanningFeatureStatus? status, CancellationToken ct = default)`
  - `Task DeleteAsync(Guid projectId, string slug, CancellationToken ct = default)`
- `PlanningArtifactService(OvermindDbContext, IPlanningMaterializer, ILogger<PlanningArtifactService>)`
  - `Task<PlanningArtifact> UpsertAsync(Guid featureId, PlanningArtifactKind kind, string body, int? order, PlanningReviewLevel? review, string? logNote, CancellationToken ct = default)`
  - `Task SetStatusAsync(Guid artifactId, PlanningArtifactStatus status, string logNote, CancellationToken ct = default)`
  - `Task ValidateAsync(Guid artifactId, CancellationToken ct = default)`
  - `Task AppendLogAsync(Guid artifactId, string note, CancellationToken ct = default)`
  - `Task DeleteAsync(Guid artifactId, CancellationToken ct = default)`

**PlanningFailureKind → HTTP status mapping:**
- `NotFound` → 404
- `Duplicate` → 409 (title: DuplicateSlug / DuplicateArtifact)
- `RuleViolation` → 409 (title: ChangeNoteRequired / CannotValidateTask)
- `InvalidStatus` → 400

**Verification:** `dotnet build` → 0 warnings / 0 errors. `dotnet test` → 296 passed, 0 failed (Core 85, Infrastructure 76, Mcp 34, Api 101).

**Notes for task 04 (agent door):**
- All handler methods are `internal static` — task 04 maps `/api/agent/planning/*` onto the same delegates.
- `CreateArtifact` handler resolves feature via `ResolveFeatureOrThrow` (private static in `PlanningArtifactsApi`) — task 04 may need to reuse this.
- The `CreateArtifact` handler returns 201 for `Version == 1` and 200 otherwise.

**Notes for task 05 (materializer):**
- Delete `NullPlanningMaterializer` and register the real `PlanningMaterializer`.
- `SyncAsync` has exactly two call sites (one per service). Both swallow exceptions and log warnings.
- `ClearAsync` is called from `PlanningFeatureService.DeleteAsync` before removing rows.
</log>
