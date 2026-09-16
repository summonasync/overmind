---
feature: 006
task: 01
slug: planning
status: done
review: hard
created: 2026-09-08
---

# 006/01 — Planning domain & persistence

<goal>
The planning ceremony exists in the domain and in SQLite: `PlanningFeature`, `PlanningArtifact`, `PlanningArtifactVersion`, `PlanningMaterializedFile`, their enums and invariants, the two new `Project` workflow properties, EF configuration and one generated `Planning` migration — all covered by tests. No API surface yet.
</goal>

<context>
Overmind is a .NET 10 solution: `src/Overmind.Core` (pure domain, zero dependencies — no `using Microsoft.*`, no EF annotations, no serialization attributes), `src/Overmind.Infrastructure` (EF Core Sqlite), `src/Overmind.Host` (minimal APIs). Read `STRUCTURE.md` before writing code.

Versioning mechanic to mirror: `src/Overmind.Core/Entities/ContextEntry.cs` (`UpdateContent` snapshots an `EntryVersion` and bumps `CurrentVersion`) + `EntryVersion.cs` (internal static factory) + the `EntryVersion` block in `OvermindDbContext.OnModelCreating` (`ValueGeneratedNever`, unique `(EntryId, Version)`).

Entity house style: private parameterless ctor, private setters, static `Create` factory with `ArgumentException.ThrowIfNullOrWhiteSpace` guards, `DateTime.UtcNow` timestamps, collection navigations initialized to `[]`.

Exception contract (the API task depends on it): invariant violations throw `InvalidOperationException` (mapped to 409 later); an invalid status for a kind throws `ArgumentOutOfRangeException` (mapped to 400 later).

Migrations live in `src/Overmind.Infrastructure/Migrations/` and are generated only: `dotnet ef migrations add Planning --project src/Overmind.Infrastructure` (a design-time factory `OvermindDbContextFactory` exists, so no startup project is needed). Never hand-edit generated migrations.

Tests: `tests/Overmind.Core.Tests` (xUnit + Shouldly), `tests/Overmind.Infrastructure.Tests` (temp SQLite via the existing `TempDatabase` helper).
</context>

<scope>
Core enums (`src/Overmind.Core/Enums/`, one file per type):
- `PlanningFeatureStatus { Draft, Ready, InProgress, Done }`
- `PlanningArtifactKind { Spec, Plan, Task, Result }`
- `PlanningArtifactStatus { Draft, Validated, Todo, InProgress, Review, Done, Blocked }`
- `PlanningReviewLevel { Light, Hard }`
- `PlanningMaterialization { Inherit, Off, On, Archive }`
- `HumanValidationOverride { Unset, On, Off }`

Core entities (`src/Overmind.Core/Entities/`):
- `PlanningFeature { Id, ProjectId, Slug, Title, Body, Status, Version, CreatedAt, UpdatedAt }` + `List<PlanningArtifact> Artifacts`.
  - `Create(projectId, slug, title, body)` — slug and title required, body may be empty (stored as `string.Empty` when null); `Status = Draft`, `Version = 1`.
  - `Update(title, body)` — bumps `Version` only when `Status != Draft`; always touches `UpdatedAt`.
  - `SetStatus(status)`.
- `PlanningArtifact { Id, FeatureId, Kind, Status, Review, Body, Order, FrozenAt, Version, CreatedAt, UpdatedAt }` (`FrozenAt` is `DateTime?`) + `List<PlanningArtifactVersion> Versions`.
  - `Create(featureId, kind, body, order = 0, review = PlanningReviewLevel.Light)` — initial status: `Task` → `Todo`, every other kind → `Draft`; `Version = 1`; body required.
  - `static IReadOnlySet<PlanningArtifactStatus> AllowedStatuses(PlanningArtifactKind kind)` — Spec/Plan/Result → `Draft|Validated`; Task → `Todo|InProgress|Review|Done|Blocked`.
  - `UpdateBody(body, logNote = null)` — snapshots the previous body into `PlanningArtifactVersion`, bumps `Version`, touches `UpdatedAt`. When `IsFrozen`: throws `InvalidOperationException` if `logNote` is null/whitespace; otherwise appends the dated note through `PlanningLog.Append`, sets `Status = Draft` and clears `FrozenAt`.
  - `SetStatus(status, logNote = null)` — throws `ArgumentOutOfRangeException` when `status` is outside `AllowedStatuses(Kind)`; appends the note to `<log>` when provided; moving off `Validated` clears `FrozenAt`.
  - `Validate()` — Spec/Plan/Result only (`InvalidOperationException` for `Task`); sets `Status = Validated` and `FrozenAt = DateTime.UtcNow`.
  - `AppendLog(note)` — `PlanningLog.Append` on the body, touches `UpdatedAt`, does NOT bump `Version` and creates no version row.
  - `SetOrder(order)`, `SetReview(review)`.
  - `IsFrozen => Status == PlanningArtifactStatus.Validated`.
- `PlanningArtifactVersion { Id, ArtifactId, Version, Body, CreatedAt }` — internal static `Create`, mirroring `EntryVersion`.
- `PlanningMaterializedFile { Id, ProjectId, FeatureId, RelativePath, Sha256, MaterializedAt }` + static `Create(...)` + `Refresh(sha256)`.
- `Project`: add `PlanningMaterialization PlanningMaterialization` (default `Inherit`) and `HumanValidationOverride RequireHumanValidation` (default `Unset`) plus `SetPlanningWorkflow(materialization, validation)`. `Project.Create` keeps its current signature; defaults are applied by the factory.

Core `PlanningLog` (`src/Overmind.Core/PlanningLog.cs`, static):
- `Append(body, note, DateTime? now = null)` → inserts `- yyyy-MM-dd — <note>` as the last line inside `<log>…</log>`, creating the section at the end of the body when absent; returns the new body. Empty/whitespace note → `ArgumentException`.
- `Parse(body)` → `IReadOnlyList<PlanningLogLine>` where `record PlanningLogLine(DateOnly? Date, string Text)`; reads `- …` lines inside `<log>`; missing section → empty list; unparseable date → `Date = null` with the raw text kept.

Core abstractions (`src/Overmind.Core/Abstractions/`):
- `IPlanningMaterializer` with exactly three methods: `SyncAsync(projectId, ct)` → `PlanningMaterializationReport`, `DetectDriftAsync(projectId, ct)` → `IReadOnlyList<PlanningDriftItem>`, `ClearAsync(projectId, ct)` → `PlanningMaterializationReport`.
- `PlanningDriftItem(Guid ProjectId, string FeatureSlug, string RelativePath, DriftKind Kind)` — reuse the existing `DriftKind` enum.
- `PlanningMaterializationReport(Guid ProjectId, PlanningMaterialization Strategy, IReadOnlyList<PlanningMaterializedFileInfo> Files)` + `PlanningMaterializedFileInfo(string RelativePath, string Sha256)`.

Infrastructure:
- `OvermindDbContext`: `DbSet`s for `PlanningFeatures`, `PlanningArtifacts`, `PlanningArtifactVersions`, `PlanningMaterializedFiles`; cascade `Project → PlanningFeature → PlanningArtifact → PlanningArtifactVersion`; cascade `PlanningFeature → PlanningMaterializedFile`; unique index `(ProjectId, Slug)` on features; unique index `(FeatureId, Kind, Order)` on artifacts; index on `PlanningMaterializedFile.ProjectId`; `ValueGeneratedNever` on `PlanningArtifactVersion.Id`.
- Generated migration `Planning`.

Tests:
- `tests/Overmind.Core.Tests/Entities/PlanningFeatureTests.cs` — factory defaults, `Version` bumps only once status leaves `Draft`, `Update` touches `UpdatedAt`.
- `tests/Overmind.Core.Tests/Entities/PlanningArtifactTests.cs` — initial status per kind; allowed-status subsets (a task cannot become `Validated`, a spec cannot become `Done`); `Validate` freezes and stamps `FrozenAt`; `Validate` on a task throws; frozen `UpdateBody` without a note throws; frozen `UpdateBody` with a note → `Draft` + `FrozenAt` null + note present in `<log>` + one version row; `SetStatus` off `Validated` unfreezes; `AppendLog` bumps no version.
- `tests/Overmind.Core.Tests/PlanningLogTests.cs` — append into an existing section, create a missing section, parse dates/text, tolerate a missing section and malformed lines.
- `tests/Overmind.Infrastructure.Tests/CrudRoundtripTests.cs` (extend) — feature + artifacts + versions roundtrip; cascade delete from project and from feature; unique `(ProjectId, Slug)` and `(FeatureId, Kind, Order)` violations throw; `Project.SetPlanningWorkflow` persists both values.
</scope>

<anti-scope>
- No HTTP API, no contracts/validators/mappers, no DI registration of planning services.
- No `IPlanningMaterializer` implementation (interface only), no importer, no MCP, no web changes.
- No FTS5 indexing of planning bodies (deferred by the plan).
- No changes to existing entities/enums beyond the two new `Project` properties.
</anti-scope>

<constraints>
- `dotnet build && dotnet test` fully green; the 189 existing tests must not break.
- No new NuGet packages. Core stays dependency-free.
- File per type, filename matches the type. No comments unless a decision is non-obvious. English only in code and messages.
- Migration generated, never hand-edited.
- No git commits — report the changed-file list instead.
</constraints>

<acceptance-criteria>
- [ ] `dotnet build && dotnet test` green, existing suite intact.
- [ ] Editing a frozen artifact without a change note throws; with a note it appends the dated line, resets to `Draft`, clears `FrozenAt` and snapshots a version.
- [ ] Status vocabulary is enforced per kind inside the domain, not by callers.
- [ ] `AppendLog` changes the body without creating a version row or bumping `Version`.
- [ ] The generated migration contains unique `(ProjectId, Slug)` and `(FeatureId, Kind, Order)` indexes.
- [ ] `PlanningMaterializedFile` lives in `Core/Entities`, not in Infrastructure.
</acceptance-criteria>

<refs>
- @../spec.md — "Data model" (amended 2026-09-08), "Lifecycle & gates"
- @../plan.md — "Cross-cutting decisions" + Task 01
- src/Overmind.Core/Entities/ContextEntry.cs, src/Overmind.Core/Entities/EntryVersion.cs — versioning mechanic to mirror
- src/Overmind.Infrastructure/Persistence/OvermindDbContext.cs — EF configuration style
- STRUCTURE.md — backend architecture rules 1–3, backend code style
</refs>

<obstacles>
- dotnet-ef tool was v10.0.9 while runtime is 10.0.11 — tools printed a warning but migration generated successfully, no upgrade was needed.
- PlanningMaterializedFile has cascade FK to both Project and PlanningFeature. SQLite handles dual cascade paths without issue; if this is ever migrated to SQL Server, one path may need to switch to Restrict.
</obstacles>

<log>
2026-09-08 — task created from the validated plan.
2026-09-08 — implemented:
  Core enums (6 files): PlanningFeatureStatus, PlanningArtifactKind, PlanningArtifactStatus, PlanningReviewLevel, PlanningMaterialization, HumanValidationOverride.
  Core entities (4 files): PlanningFeature, PlanningArtifact, PlanningArtifactVersion, PlanningMaterializedFile.
  Core PlanningLog (static): Append/Parse with <log> section management, PlanningLogLine record.
  Core abstractions (3 files): IPlanningMaterializer, PlanningDriftItem, PlanningMaterializationReport + PlanningMaterializedFileInfo.
  Project entity: added PlanningMaterialization + RequireHumanValidation properties with defaults, SetPlanningWorkflow method.
  Infrastructure: OvermindDbContext extended with 4 DbSets + EF configuration (cascades, unique indexes, ValueGeneratedNever).
  Migration: 20260908133734_Planning (generated, not hand-edited).
  Tests: PlanningFeatureTests (10), PlanningArtifactTests (16), PlanningLogTests (11), CrudRoundtripTests extended (6 new).
  Verification: dotnet build green (0 warnings, 0 errors). dotnet test: Core 67, Infrastructure 54, Mcp 34, Api 77 — total 232, all passed.
  Design-time DB cleaned up.
</log>
