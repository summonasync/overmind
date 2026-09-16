---
feature: 006
task: 05
slug: planning
status: done
review: hard
created: 2026-09-08
---

# 006/05 — Planning materialization (off / on / archive)

<goal>
Planning artifacts can be written to disk per project: `on` writes a repo `BACKLOG/<slug>/` folder plus a managed block in the repo's `BACKLOG.md` with drift guards, `archive` writes the same markdown under the Overmind data directory with free rewrites, `off` deletes what was tracked. Drift is detectable and re-materialization is an explicit user action.
</goal>

<context>
This writes into **real user repositories**, so the safety model matters more than the feature. The existing skill materializer is the template: `src/Overmind.Infrastructure/Materialization/SkillMaterializer.cs` (plan → transaction → write → track rows → rollback with best-effort cleanup), `PathGuard.cs` (`ResolveUnderRoot` traversal defense, `PathsEqual`, `PathComparer` — `internal` to Infrastructure, reuse it), `MaterializedFile.cs`, and `MaterializationException` (`Overwrite`, `Traversal`, `IoFailure`, `MissingProject`, …).

Task 01 defined `IPlanningMaterializer` (`SyncAsync` / `DetectDriftAsync` / `ClearAsync`), `PlanningMaterializedFile`, `PlanningDriftItem`, `PlanningMaterializationReport`. Task 03 defined `PlanningPolicy.ResolveMaterializationAsync`.

Archive root convention: Infrastructure must not hardcode `~/.overmind` (that duplication is already recorded debt). `Host/DependencyInjection` passes `OvermindPaths.DataDirectory` into the materializer constructor.

Frontmatter/`<log>` grammar: bodies already carry their XML sections; materialization only adds frontmatter and headings around them.
</context>

<scope>
**Shared primitives first (plan amendment A3).** Task 05's acceptance is that the written layout round-trips through the importer byte-for-byte, so hashing, slugifying and directory pruning must exist exactly once:
- `src/Overmind.Infrastructure/Materialization/ContentHash.cs` (internal static) — normalize CRLF/CR → LF, UTF-8 no BOM, SHA-256 hex; one `Normalize(content)` + one `Sha256(content)` used by both materializers.
- `src/Overmind.Infrastructure/Materialization/Slug.cs` (internal static) — the single slugify (lowercase letters/digits, runs of anything else → single dash, no leading/trailing dash, fallback when empty).
- `src/Overmind.Infrastructure/Materialization/DirectoryPruner.cs` (internal static) — `PruneEmptyDirectories(filePath, root)` + `TryDelete(path)`.
- Refactor `SkillMaterializer` onto these three, behavior-identically: its private `Slugify`, `PruneEmptyDirectories`, `TryDelete`, `Utf8NoBom` and the normalize+hash inside `FilePlan.Create` all move. `MaterializationTests` is the safety net — it must pass unchanged; if a test has to change, the behavior changed, which is not allowed here.
- `src/Overmind.Infrastructure/Planning/BacklogNaming.cs` (internal static) — the single home of the `feature:`/`slug:` frontmatter split rule and of task file naming (`NN-<slug>.md`, `NN` = zero-padded order). Task 06's importer consumes this same type.

**The materializer.** `src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs` — `IPlanningMaterializer`, constructor `(OvermindDbContext db, PlanningPolicy policy, string dataDirectory)`:

Layout, identical for `on` and `archive`:
- `BACKLOG/<feature-slug>/feature.md` — frontmatter (`feature`, `slug`, `status`, `version`, `created`) + `# Feature <slug> — <title>` + `Body`.
- `BACKLOG/<feature-slug>/spec.md`, `plan.md` — frontmatter (`feature`, `slug`, `status`, `created`, plus `validated: <yyyy-MM-dd>` when `FrozenAt` is set) + body.
- `BACKLOG/<feature-slug>/tasks/NN-<task-slug>.md` — `NN` = `Order` zero-padded to 2; `<task-slug>` = slugified first `# ` heading of the body, fallback `task-NN`; frontmatter (`feature`, `task: <Order>`, `slug`, `status`, `review`, `created`) + body.
- `result.md` when a Result artifact exists.
- Frontmatter split rule: if the text before the first `-` of the feature slug is all digits → `feature: 006` + `slug: planning`; otherwise both fields get the full slug.
- Only artifacts that exist are written; a feature with no spec writes no `spec.md`.

Roots: `on` → `project.DiskPath`; `archive` → `<dataDirectory>/projects/<project-slug>/backlog` where `<project-slug>` is the slugified project name (fallback: project id). All paths go through `PathGuard.ResolveUnderRoot`.

Managed block in `BACKLOG.md` (`src/Overmind.Infrastructure/Planning/ManagedBlock.cs`):
- Markers `<!-- overmind:planning:start -->` / `<!-- overmind:planning:end -->`.
- Block content: one line per feature — `- \`[x]\` <slug> → \`BACKLOG/<slug>/\` — <title>` with the status marker mapped from `PlanningFeatureStatus` (`Draft`/`Ready` → `[ ]`, `InProgress` → `[~]`, `Done` → `[x]`).
- File exists with markers → replace only the text between them, byte-for-byte preserving everything outside. File exists without markers → append a blank line + the block. File missing → create it with `# BACKLOG\n\n` + the block.
- The tracked `PlanningMaterializedFile` row for `BACKLOG.md` stores the SHA-256 of **the block content only**, so human edits outside the markers never register as drift.

Write semantics:
- Normalized content (CRLF/CR → LF), UTF-8 without BOM, SHA-256 over the normalized bytes — same as `SkillMaterializer`.
- `on`: a target path that exists on disk but is not tracked for this project → `MaterializationException.Overwrite` (surfaces as 409). Tracked paths are rewritten freely. Writes plus row updates run in one DB transaction with best-effort cleanup of already-written files on failure.
- `archive`: free rewrite, no overwrite guard (internal cache), rows still tracked.
- `off`: `ClearAsync` deletes tracked files, prunes now-empty directories up to the root, removes the managed block from `BACKLOG.md` when it was written by us, and drops the rows.
- `DetectDriftAsync`: `on` only (empty list otherwise) — `Missing` when a tracked file is gone, `Modified` when the on-disk hash differs (for `BACKLOG.md`, compare the extracted block hash).
- `SyncAsync` resolves the strategy first and delegates: `Off` → `ClearAsync` behavior; strategy change from a previous one → clear the old root, then write the new.

Host wiring:
- Replace the `NullPlanningMaterializer` registration from task 02 with the real `PlanningMaterializer` (scoped), the data directory captured from `OvermindPaths.DataDirectory`, and delete the placeholder type. **Do not add `SyncAsync` calls to handlers**: since plan amendment A1 the services (`PlanningFeatureService`, `PlanningArtifactService`) already call `SyncAsync(projectId, force: false)` after every mutation and swallow-and-log `MaterializationException`, so a disk problem can never fail a DB write. This task only makes that call do real work — verify the wiring by test, do not restructure it.
- Surfacing swallowed failures: the report's `Warnings` channel (task 01b) is how "synced, but refused to overwrite N foreign file(s)" reaches the caller; the explicit `materialize` endpoint returns it, the background sync logs it.
- `POST /api/projects/{projectId:guid}/planning/materialize` with body `{ force: bool }` → explicit re-sync; `force: false` returns 409 on untracked foreign files, `force: true` overwrites them and refreshes the hashes. Response: `PlanningMaterializationReport` mapped to `{ projectId, strategy, files: [{ relativePath, sha256 }] }`.
- `GET /api/projects/{projectId:guid}/planning/drift` → `[{ projectid, featureSlug, relativePath, kind }]` (camelCase).
- `ProjectsApi.Update`: extend the DiskPath-immutability guard to also count `PlanningMaterializedFile` rows (same hazard as the existing kit guard — relative paths resolve under `DiskPath`).

Tests:
- `tests/Overmind.Infrastructure.Tests/PlanningMaterializerTests.cs` (temp dirs + temp DB): `on` writes the full layout with correct NN naming and frontmatter; foreign content outside the managed block survives byte-for-byte across re-syncs; block appended when markers are absent; block created with the file when `BACKLOG.md` is missing; `BACKLOG.md` drift is block-scoped (edit outside the block → no drift, edit inside → `Modified`); untracked existing file → `Overwrite`; tracked file deleted on disk → `Missing`; `archive` writes under the data directory and rewrites freely; `off` clears files, prunes empty dirs, removes the block and drops rows; strategy switch `on → archive` clears the repo copy; path traversal in a slug/heading is rejected.
- `tests/Overmind.Api.Tests/PlanningMaterializationTests.cs`: `materialize` with and without `force`; `drift` shape; DiskPath change → 409 while planning files are tracked; a normal artifact write in `on` mode produces the updated file on disk.
</scope>

<anti-scope>
- No git operations of any kind (no add/commit/status) — we only write files.
- No importer (task 06); no MCP or web changes (drift UI is task 09).
- No two-way sync: disk is a derived cache, edits on disk are never read back into the DB.
- No materialization of Entries/kits changes; `SkillMaterializer` is untouched.
</anti-scope>

<constraints>
- `dotnet build && dotnet test` fully green.
- No new packages. English only. No comments unless non-obvious.
- Every resolved path goes through `PathGuard.ResolveUnderRoot`.
- `PlanningMaterializer` keeps exactly the three interface methods public; helpers are private or in `ManagedBlock`.
- Never write outside the resolved root; never delete a file that has no tracked row.
- No git commits — report the changed-file list.
</constraints>

<acceptance-criteria>
- [x] `on` mode layout matches the file-based convention the importer reads (feature/spec/plan/tasks/NN-slug.md).
- [x] Human edits outside the managed block in `BACKLOG.md` are preserved byte-for-byte and never reported as drift.
- [x] Untracked existing files block the write (409) unless `force` is passed.
- [x] `archive` writes under the Host-provided data directory; Infrastructure contains no `~/.overmind` literal.
- [x] `off` / strategy switch removes tracked files, prunes empty directories and drops rows.
- [x] DiskPath change is rejected with 409 while planning files are tracked.
- [x] A failed sync never rolls back or loses the DB write.
</acceptance-criteria>

<refs>
- @../spec.md — "Materialization" (off/on/archive, drift guards, managed block)
- @../plan.md — Task 05 + Risks (managed block, DiskPath retargeting)
- src/Overmind.Infrastructure/Materialization/{SkillMaterializer,PathGuard,MaterializedFile}.cs
- src/Overmind.Core/Abstractions/MaterializationException.cs, DriftItem.cs
- TECHNICAL-SOLUTIONS.md — "Project DiskPath is immutable while a kit is active"
- @./01-domain-persistence.md, @./03-planning-workflow.md
</refs>

<obstacles>
- **Pre-decided before this task started** (audit `audits/audit-2026-09-10.md`, WARNING-4, user decision 2026-09-10): `PlanningFeatureService.DeleteAsync` calls `ClearAsync` through a private wrapper that swallows any exception and logs a warning, mirroring the existing `SyncAsync` wrapper. A disk failure therefore can never block removing the feature row — pinned by `PlanningServiceTests.ClearAsync_Throwing_FeatureDeleteStillSucceeds`. Consequences for the real materializer: do **not** design `ClearAsync` on the assumption that throwing aborts the delete (it does not), and surface files it could not remove via `PlanningMaterializationReport.Warnings` so the caller still learns about them. `IPlanningMaterializer` in Core is deliberately unchanged — the policy lives at the call site.
- **Hit during execution 2026-09-12 — `BACKLOG.md` is a documented exception to the untracked-overwrite guard.** The scope both demands "untracked existing file → Overwrite" and "file exists without markers → append the block"; a real repo almost always has a human `BACKLOG.md`. Resolution: the Overwrite guard applies to feature files (`BACKLOG/<slug>/**`), while `BACKLOG.md` always merges (append block when markers are absent, replace between markers when present). Foreign feature files still hard-block with 409, pinned by tests.
- **Hit during execution 2026-09-12 — task frontmatter `task:` is written zero-padded (`task: 05`), not bare `<Order>`.** The scope text says `task: <Order>` but the dogfood corpus (`BACKLOG/006-planning/tasks/05-*.md`) uses `task: 05`; the importer (task 06) reads `Order` from the `NN` filename prefix, so the frontmatter field is cosmetic. Corpus convention wins; importer authors take note.
- **Hit during execution 2026-09-12 — .NET `File.Delete` on Unix ignores the read-only file bit** (unlink only needs directory write permission). The undeletable-file warning test therefore locks the parent directory (`0555`) instead of the file; test is Linux-gated (`OperatingSystem.IsLinux()`).
- **Hit during execution 2026-09-12 — EF Core identity-map staleness in tests.** Switching `PlanningMaterialization` from a second context leaves the first context's tracked `Project` stale, so a materializer bound to the old context resolves the old strategy. Production is safe (one scoped context per request; services and materializer share it), but tests must use a fresh context + materializer after a strategy switch.
- **Closed by review 2026-09-13 (WARNING-1, Slug vs legacy `Slugify` on punctuation):** battle-database check done — `~/.overmind/overmind.db` holds zero kits (fresh DB after the move to Ubuntu), zero old/new slug diffs. The shared `Slug` maps punctuation runs to a dash where the old `Slugify` dropped them (e.g. `a,b` → `a-b` vs `ab`), which only affects on-disk skill catalog names. **If the old Windows database is ever restored, repeat the check before shipping:** for every skill `Title` in every kit, compare legacy `Slugify(title)` vs `Slug.From(title, "skill")`; any divergence means stale catalog directories on disk — the data itself is not lost, `DematerializeAsync` of the old kit (or a force rematerialize) rebuilds the tree.
</obstacles>

<log>
2026-09-08 — task created from the validated plan.
2026-09-10 — `<obstacles>` pre-populated with the `ClearAsync` failure policy decided during the 2026-09-10 system audit, so this task implements against it instead of rediscovering it.
2026-09-12 — implemented. Shared primitives (`ContentHash`/`Slug`/`DirectoryPruner`) extracted; `SkillMaterializer` refactored onto them with `MaterializationTests` passing **unchanged** (11/11, zero edits to the test file). `PlanningMaterializer` + `ManagedBlock` + `BacklogNaming` done; DI wired with `OvermindPaths.DataDirectory`; `NullPlanningMaterializer` deleted; `POST .../planning/materialize` + `GET .../planning/drift` added; `ProjectsApi.Update` DiskPath guard counts `PlanningMaterializedFile` rows. No `SyncAsync` calls added to handlers (A1 wiring verified by test — the real materializer now runs inside the existing swallow-and-log wrappers). New tests: `PlanningMaterializerTests` 13, `PlanningMaterializationTests` 5. `dotnet build Overmind.sln` 0/0; `dotnet test Overmind.sln` 351/351 (base 333 + 18). No git commits per constraints.
2026-09-13 — hard review PASS with 4 warnings; three fixed. WARNING-2: overwrite guard of the new root now runs BEFORE the old-root clear, and ownership requires that the old root no longer holds a copy (otherwise `archive→on` would silently rewrite a foreign repo file that merely shares a relative path with a stale row) — pinned by `StrategySwitch_ArchiveToOn_WithForeignRepoFile_BlocksBeforeTouchingArchive`. WARNING-3: rollback cleanup deletes only files created during the current run (`preExisting` set), never files that were on disk before it — pinned by `Sync_RollbackAfterForceAdoption_KeepsPreExistingFiles`. WARNING-4: `CONTEXT.md`/`STRUCTURE.md` no longer mention the deleted `NullPlanningMaterializer`. Review SUGGESTION done: `SlugTests` (12 cases) pins the path-safety invariant via a new `InternalsVisibleTo` for the test assembly; battle-DB check for WARNING-1 recorded in `<obstacles>`. `dotnet build Overmind.sln` 0/0; `dotnet test Overmind.sln` 365/365 (base 351 + 14).
</log>
