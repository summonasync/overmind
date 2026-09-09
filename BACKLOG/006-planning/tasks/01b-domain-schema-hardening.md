---
feature: 006
task: 01b
slug: domain-schema-hardening
status: done
review: light
created: 2026-09-08
---

# 006/01b — Domain & schema hardening (post-review fix round)

<goal>
Close the four BLOCKERs and the accepted IMPORTANT/MINOR findings from the two reviews of task 01, so tasks 02–06 can be built without touching the domain again or re-opening a migration that has already shipped to a user database.
</goal>

<context>
Task 01 landed the planning domain: `PlanningFeature`, `PlanningArtifact`, `PlanningArtifactVersion`, `PlanningMaterializedFile`, six planning enums, `PlanningLog`, `IPlanningMaterializer` + report/drift DTOs, EF configuration, migration `20260908133734_Planning`, 232/232 tests green.

Two reviews ran on it. Read the hard architect review **first** — it is the source of truth for the findings below, with `file:line`, risk and rationale: `audits/architect-review-2026-09-08-006-01.md`. Its "Orchestrator disposition" section at the bottom lists exactly which findings this task owns and which were routed elsewhere — do not implement the routed ones here.

**Migration strategy is already decided and verified:** the `Planning` migration has never been applied to a real database (`~/.overmind/overmind.db` holds `__EFMigrationsHistory` + `20260906210328_InitialSchema`, but no `20260908133734_Planning` and no `PlanningFeatures` table). So all schema deltas go into ONE regenerated migration: delete `src/Overmind.Infrastructure/Migrations/20260908133734_Planning.cs` and its `.Designer.cs`, then run `dotnet ef migrations add Planning --project src/Overmind.Infrastructure`. Never hand-edit the generated file, and delete the `overmind-design.db` the design-time factory may drop in the repo root.
</context>

<scope>
**`src/Overmind.Core/PlanningLog.cs` — BLOCKER-1 + line-level findings**
- Detect the log section by line-anchored tags, not by first-match substring: source-generated regexes `(?m)^[ \t]*<log>[ \t]*$` and `(?m)^[ \t]*</log>[ \t]*$` via `[GeneratedRegex]` (this makes `partial` and the `System.Text.RegularExpressions` import legitimate).
- A body that merely mentions `<log>`/`</log>` in prose must be treated as having **no** log section: `Append` creates a real section at the end and leaves the prose byte-identical; `Parse` returns an empty list.
- Multiple anchored pairs → operate on the **last** one.
- Every inserted newline is `\n`. No `AppendLine`, no `Environment.NewLine` — bodies stay LF (they are hashed and materialized later).
- Remove the redundant `.ToString()` calls; rename the snake_case local (`content_part`) to camelCase.
- Reproducible test corpus for the prose case: the shape of `BACKLOG/006-planning/plan.md`, which mentions `<log>` and `</log>` inline and has no real section.

**`src/Overmind.Core/Entities/PlanningArtifact.cs` — BLOCKER-4, IMPORTANT-1, IMPORTANT-2**
- `UpdateBody(body, logNote)`: append the note through `PlanningLog.Append` whenever `logNote` is non-whitespace, frozen or not; keep throwing `InvalidOperationException` when frozen and the note is missing; keep the reset to `Draft` + `FrozenAt = null`. Store the body verbatim — no trimming.
- `SetStatus(status, logNote)`: when `IsFrozen` and the target status is not `Validated`, a non-whitespace `logNote` is **required** — throw the same `InvalidOperationException` otherwise. The invariant must live in the domain, not in a request validator (STRUCTURE rule 6).
- `Validate()`: one `var now = DateTime.UtcNow` assigned to both `FrozenAt` and `UpdatedAt`.
- New `static PlanningArtifact Restore(Guid featureId, PlanningArtifactKind kind, PlanningArtifactStatus status, PlanningReviewLevel review, string body, int order, DateTime? frozenAt, int version, int createdAt, DateTime updatedAt)` for the task-06 importer: no log side effects, no version rows, `Version`/timestamps taken as given; validates `status` against `AllowedStatuses(kind)` and requires `frozenAt` to be set exactly when `status == Validated`.
- Version-row numbering stays as built (a row labelled N holds the body that was live at version N; a fresh artifact is version 1). This deliberately diverges from `ContextEntry.UpdateContent`; pin it with a test rather than "fixing" it.

**`src/Overmind.Core/Entities/PlanningFeature.cs` — BLOCKER-4, MINOR-2**
- `Update(string? title, string? body)`: `null` means untouched for each field; title trimmed, body stored verbatim; `Version` bumps only when `Status != Draft`; `UpdatedAt` always touched.
- New `static PlanningFeature Restore(Guid projectId, string slug, string title, string body, PlanningFeatureStatus status, int version, DateTime createdAt, DateTime updatedAt)`.

**`src/Overmind.Core/Entities/PlanningMaterializedFile.cs` — BLOCKER-2**
- `FeatureId` becomes `Guid?`; `null` means a project-scoped row (the `BACKLOG.md` index file). `Create` takes `Guid? featureId`.

**`src/Overmind.Core/Abstractions/` — BLOCKER-2, BLOCKER-3**
- `IPlanningMaterializer` keeps exactly three methods but grows the missing semantics: `SyncAsync(Guid projectId, bool force = false, CancellationToken ct = default)`, `DetectDriftAsync(Guid projectId, CancellationToken ct = default)`, `ClearAsync(Guid projectId, Guid? featureId = null, CancellationToken ct = default)`.
- `PlanningMaterializationReport` gains `IReadOnlyList<string> Warnings` so a background sync can report files it refused to overwrite.
- `PlanningDriftItem.FeatureSlug` becomes `string?` (null for project-scoped files).

**`src/Overmind.Core/Enums/` — IMPORTANT-5**
Explicit values on all six planning enums, keeping today's defaults at `0` so existing rows/columns keep their meaning, and grouping the two status vocabularies in separate decades to document the split:
- `PlanningFeatureStatus { Draft = 0, Ready = 1, InProgress = 2, Done = 3 }`
- `PlanningArtifactKind { Spec = 0, Plan = 1, Task = 2, Result = 3 }`
- `PlanningArtifactStatus { Draft = 0, Validated = 1, Todo = 10, InProgress = 11, Review = 12, Done = 13, Blocked = 14 }`
- `PlanningReviewLevel { Light = 0, Hard = 1 }`
- `PlanningMaterialization { Inherit = 0, Off = 1, On = 2, Archive = 3 }`
- `HumanValidationOverride { Unset = 0, On = 1, Off = 2 }`
Do **not** touch `EntryType` or `AgentPlatform`.

**`src/Overmind.Infrastructure/Persistence/OvermindDbContext.cs` — IMPORTANT-3, IMPORTANT-4, MINOR-3**
- `PlanningArtifactVersion`: add unique `(ArtifactId, Version)` next to the existing `ValueGeneratedNever`.
- `PlanningMaterializedFile`: add unique `(ProjectId, RelativePath)`.
- One comment line above the `PlanningMaterializedFile` configuration recording that it has multiple cascade paths (Project direct, Project → PlanningFeature → row, and a nullable `FeatureId`) which SQLite accepts but SQL Server would reject without `Restrict`. This is the "hidden constraint" case where a comment is warranted.
- Then regenerate the single `Planning` migration.

**Tests**
- `tests/Overmind.Core.Tests/PlanningLogTests.cs`: prose mention with a real section (note lands inside the section, prose untouched); prose mention without one (section created at the end); multiple sections → last wins; result contains no `\r`; `Parse` on a prose-only body → empty; existing cases kept.
- `tests/Overmind.Core.Tests/Entities/PlanningArtifactTests.cs`: unfreezing via `SetStatus` without a note now throws (update the test that pinned the old behavior); a note supplied on a **non-frozen** `UpdateBody` is appended; three consecutive `UpdateBody` calls assert every version row's number and body plus the final `Version`; `AppendLog("")`/`" "` → `ArgumentException`; `Restore` round-trips kind/status/order/review/frozenAt/version/timestamps and rejects an invalid status or a mismatched `frozenAt`.
- `tests/Overmind.Core.Tests/Entities/PlanningFeatureTests.cs`: `Update(null, null)` changes nothing but touches `UpdatedAt`; `Update(title, null)` keeps the body; bodies are stored verbatim (leading/trailing whitespace and newlines preserved); `Restore` round-trip.
- `tests/Overmind.Infrastructure.Tests/CrudRoundtripTests.cs`: a project-scoped `PlanningMaterializedFile` row (`FeatureId == null`) persists and survives the deletion of an unrelated feature; unique `(ArtifactId, Version)` and `(ProjectId, RelativePath)` violations throw.
</scope>

<anti-scope>
- No HTTP API, no materializer implementation, no importer, no MCP, no web changes.
- No changes to `ContextEntry`/`EntryVersion` version numbering (separate hardening item, deliberately out of 006).
- No moving of `MaterializedFile` into `Core/Entities` (separate hardening item).
- No shared materialization helpers (`ContentHash`/`Slug`/`DirectoryPruner`) — that is task 05's scope.
- No log projection in API contracts — task 02's scope.
- No second migration, no hand-edited migration, no new packages.
</anti-scope>

<constraints>
- `dotnet build && dotnet test` fully green. No existing test may be deleted to get there; changing an assertion that pinned the OLD behavior is expected and must be listed in `<log>`.
- Core stays dependency-free (BCL only — `System.Text.RegularExpressions` is fine).
- Exactly one `Planning` migration exists at the end; the `20260908133734_Planning.*` pair is gone.
- House style: file per type, no `region`s, English only, no comments except the single DbContext cascade note.
- No git commits — report the changed-file list.
</constraints>

<acceptance-criteria>
- [ ] A body that merely mentions `<log>` in prose is never corrupted; the note lands in a real anchored section, created when absent.
- [ ] No `\r` appears in any body produced by `PlanningLog.Append`.
- [ ] Leaving the frozen state without a change note throws from the domain (both `UpdateBody` and `SetStatus`).
- [ ] A `logNote` supplied on a non-frozen update is appended instead of dropped.
- [ ] `Restore` factories exist on feature and artifact and carry version/createdAt/updatedAt/frozenAt.
- [ ] `PlanningMaterializedFile.FeatureId` is nullable and a project-scoped row round-trips and survives unrelated feature deletes.
- [ ] `IPlanningMaterializer` expresses `force`, feature-scoped `ClearAsync`, and report warnings — still three methods.
- [ ] Unique `(ArtifactId, Version)` and `(ProjectId, RelativePath)` indexes exist; all six planning enums have explicit values with unchanged `0` defaults.
- [ ] One regenerated `Planning` migration, old pair deleted, nothing hand-edited.
- [ ] `dotnet build` 0 warnings / 0 errors and the full suite green.
</acceptance-criteria>

<refs>
- audits/architect-review-2026-09-08-006-01.md — BLOCKER-1..4, IMPORTANT-1..5, MINOR-2, and the disposition section that scopes this task
- @./01-domain-persistence.md — the contract being hardened (including its `<obstacles>`)
- @../spec.md — "Data model" (amended 2026-09-08), "Lifecycle & gates"
- src/Overmind.Core/Entities/ContextEntry.cs — the versioning neighbour we deliberately diverge from
- STRUCTURE.md — backend architecture rule 6 (invariants regardless of entry point), backend code style
</refs>

<obstacles>
- `PlanningArtifact.Restore` and `PlanningFeature.Restore` are symmetric: both take `DateTime createdAt, DateTime updatedAt`. Task 06 parses frontmatter `created: 2026-09-08` via `BacklogMarkdown.ParseDate` into a `DateTime`, so no conversion is needed at the call site.
- `InternalsVisibleTo` was initially added to `Overmind.Core.csproj` for the unique-index test but was removed after review: the test now violates the constraint via raw SQL (`ExecuteSqlRawAsync` INSERT) and asserts `SqliteException`, keeping Core's internals closed.
</obstacles>

<log>
2026-09-08 — task created by the orchestrator after the hard architect review + line-level code review of task 01; scope is the accepted findings only, everything else was routed to tasks 02/05/06/09/10 or to BACKLOG hardening.

2026-09-08 — executed. Changes by file:

**src/Overmind.Core/PlanningLog.cs** — BLOCKER-1: replaced substring `IndexOf` with `[GeneratedRegex]` anchored patterns `(?m)^[ \t]*<log>[ \t]*$` / `(?m)^[ \t]*</log>[ \t]*$`; `FindLastAnchoredPair` operates on the last matched pair; `Append` creates section at end when no anchored pair exists; all newlines are `\n` (no `AppendLine`, no `Environment.NewLine`); removed redundant `.ToString()` and renamed `content_part` → `textPart`.

**src/Overmind.Core/Entities/PlanningArtifact.cs** — BLOCKER-4: added `static Restore(featureId, kind, status, review, body, order, frozenAt, version, createdAt, updatedAt)` with status/frozenAt validation. IMPORTANT-1: `SetStatus` now throws `InvalidOperationException` when `IsFrozen && status != Validated && string.IsNullOrWhiteSpace(logNote)`. IMPORTANT-2: `UpdateBody` appends `logNote` via `PlanningLog.Append` whenever it is non-whitespace (frozen or not); frozen-without-note still throws. `Validate()` uses single `var now = DateTime.UtcNow` for both `FrozenAt` and `UpdatedAt`. `AppendLog` now validates note via `ArgumentException.ThrowIfNullOrWhiteSpace`.

**src/Overmind.Core/Entities/PlanningFeature.cs** — BLOCKER-4: added `static Restore(projectId, slug, title, body, status, version, createdAt, updatedAt)`. MINOR-2: `Update(string? title, string? body)` — null means untouched per field; title trimmed, body stored verbatim (no trim); `Version` bumps only when `Status != Draft`; `UpdatedAt` always touched. `Create` no longer trims body.

**src/Overmind.Core/Entities/PlanningMaterializedFile.cs** — BLOCKER-2: `FeatureId` is now `Guid?`; `Create` takes `Guid? featureId`.

**src/Overmind.Core/Enums/** — IMPORTANT-5: explicit values on all six planning enums. `PlanningFeatureStatus { Draft=0, Ready=1, InProgress=2, Done=3 }`. `PlanningArtifactKind { Spec=0, Plan=1, Task=2, Result=3 }`. `PlanningArtifactStatus { Draft=0, Validated=1, Todo=10, InProgress=11, Review=12, Done=13, Blocked=14 }`. `PlanningReviewLevel { Light=0, Hard=1 }`. `PlanningMaterialization { Inherit=0, Off=1, On=2, Archive=3 }`. `HumanValidationOverride { Unset=0, On=1, Off=2 }`.

**src/Overmind.Core/Abstractions/IPlanningMaterializer.cs** — BLOCKER-3: `SyncAsync(projectId, force=false, ct)`, `ClearAsync(projectId, featureId=null, ct)`. `PlanningMaterializationReport` gains `IReadOnlyList<string> Warnings`. `PlanningDriftItem.FeatureSlug` is `string?`.

**src/Overmind.Infrastructure/Persistence/OvermindDbContext.cs** — IMPORTANT-3: unique `(ArtifactId, Version)` index on `PlanningArtifactVersion`. IMPORTANT-4: unique `(ProjectId, RelativePath)` index on `PlanningMaterializedFile` (replaces non-unique `ProjectId` index). MINOR-3: single comment line above `PlanningMaterializedFile` config recording multiple cascade paths caveat.

**Migration** — deleted `20260908133734_Planning.cs` + `.Designer.cs`; regenerated `20260908173101_Planning.cs` + `.Designer.cs` from clean snapshot. `OvermindDbContextModelSnapshot.cs` updated by `dotnet ef migrations add`. No `overmind-design.db` left in repo root.

**Tests flipped/added:**
- `SetStatus_OffValidated_Unfreezes` → renamed to `SetStatus_OffValidated_WithNote_Unfreezes` (now supplies note, asserts log append); old assertion that unfreezing needs no note was the pinned OLD behavior.
- Added `SetStatus_OffValidated_WithoutNote_Throws` — verifies the new domain invariant.
- Added `UpdateBody_NotFrozen_WithNote_AppendsNote` — IMPORTANT-2 coverage.
- Added `UpdateBody_ThreeConsecutiveCalls_VersionRowsTrackBodies` — asserts row N holds body live at version N.
- Added `AppendLog_EmptyNote_Throws` (Theory: "" and " ") — validates the new guard.
- Added `Restore_RoundTripsAllFields`, `Restore_InvalidStatus_Throws`, `Restore_ValidatedWithoutFrozenAt_Throws`, `Restore_DraftWithFrozenAt_Throws`.
- Added `Append_ProseMentionWithRealSection_NoteLandsInSection`, `Append_ProseMentionWithoutSection_CreatesAtEnd`, `Append_MultipleSections_OperatesOnLast`, `Append_NeverProducesCarriageReturn`, `Parse_ProseOnlyBody_ReturnsEmpty`.
- Added `Update_NullTitle_NullBody_ChangesNothingButTouchesUpdatedAt`, `Update_TitleOnly_KeepsBody`, `Update_BodyStoredVerbatim_PreservesWhitespace`, `Restore_RoundTripsAllFields` (feature).
- Added `Planning_ProjectScopedMaterializedFile_RoundtripsAndSurvivesFeatureDelete`, `Planning_UniqueArtifactVersion_Throws`, `Planning_UniqueProjectRelativePath_Throws`.

**Verification:** `dotnet build` → 0 warnings / 0 errors. `dotnet test` → 253 passed, 0 failed (Core 85, Infrastructure 57, Mcp 34, Api 77).

2026-09-08 — fix round (orchestrator verification):
- `PlanningArtifact.Restore` parameter `int createdAt` → `DateTime createdAt` (was based on wrong premise about frontmatter format; frontmatter is `created: yyyy-MM-dd`, parsed to `DateTime` by `BacklogMarkdown.ParseDate`). Removed `DateTime.UnixEpoch.AddSeconds` conversion. Updated 3 tests (`Restore_RoundTripsAllFields`, `Restore_InvalidStatus_Throws`, `Restore_ValidatedWithoutFrozenAt_Throws`, `Restore_DraftWithFrozenAt_Throws`) to pass `DateTime` values.
- Removed `InternalsVisibleTo("Overmind.Infrastructure.Tests")` from `Overmind.Core.csproj`. Reworked `Planning_UniqueArtifactVersion_Throws` to use raw SQL `INSERT INTO PlanningArtifactVersions` via `ExecuteSqlRawAsync` and assert `SqliteException` instead of calling internal `PlanningArtifactVersion.Create`.
- `Planning_UniqueProjectRelativePath_Throws` checked — uses public `PlanningMaterializedFile.Create` API, no change needed.
- **Verification:** `dotnet build` → 0 warnings / 0 errors. `dotnet test` → 253 passed, 0 failed (Core 85, Infrastructure 57, Mcp 34, Api 77).
</log>
