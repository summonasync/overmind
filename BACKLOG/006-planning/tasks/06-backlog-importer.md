---
feature: 006
task: 06
slug: backlog-importer
status: done
review: light
created: 2026-09-08
amended: 2026-09-08 — plan A3/A4: no private slugify (consume `Slug`/`BacklogNaming` from task 05) and all writes go through `PlanningFeatureService.ImportAsync`; this task therefore depends on task 02 as well as 01b
---

# 006/06 — Backlog importer CLI (`--import-backlog`)

<goal>
An existing file-based `BACKLOG/` becomes database planning artifacts in one command: `--import-backlog <path>` parses frontmatter and XML-sectioned markdown into features, specs, plans and tasks — idempotent by `(ProjectId, Slug)`, so re-running updates instead of duplicating.
</goal>

<context>
The corpus is this repo's own `BACKLOG/` (features 001–006) — that is both the test fixture shape and the R7 dogfood target. Folder layout: `BACKLOG/<nnn-slug>/feature.md` (always), `spec.md`, `plan.md`, `requirements.md` (optional), `tasks/NN-<slug>.md` (optional). `BACKLOG.md` at the repo root is a hand-written index and is **not** an input.

Frontmatter is `---`-delimited `key: value` lines at the top of a file; bodies are markdown with XML sections (`<goal>`, `<log>`, …). Parse both by hand — no YAML package (license + dependency rules).

Task 01 owns the entities; task 05 owns the on-disk layout the importer must read back (frontmatter fields, `NN-` order prefix, `feature:`/`slug:` split rule). Read `tasks/05-planning-materialization.md` for the exact field set so import and materialization round-trip.

Host startup lives in `src/Overmind.Host/Program.cs`: top-level statements, `--no-tray` is parsed from `args` today, migrations run inside `RunAsync` after `app.Build()`.
</context>

<scope>
`src/Overmind.Infrastructure/Planning/BacklogMarkdown.cs` (internal static): frontmatter parse to a case-insensitive key/value map + remaining body; `ParseDate` for `yyyy-MM-dd` prefixes (frontmatter values may carry a trailing comment, e.g. `validated: 2026-09-08 — user approved`); `FirstHeading(body)` → first `# ` line text. **No slugify and no file-naming rule here** — plan amendment A3 put those in `Infrastructure/Materialization/Slug.cs` and `Infrastructure/Planning/BacklogNaming.cs` (task 05), and this task consumes them so that import and materialization agree byte-for-byte by construction rather than by coincidence.

`src/Overmind.Infrastructure/Planning/BacklogImporter.cs` — one public method `ImportAsync(BacklogImportRequest request, CancellationToken ct)` returning `BacklogImportReport(int FeaturesCreated, int FeaturesUpdated, int ArtifactsCreated, int ArtifactsUpdated, IReadOnlyList<string> Skipped)`. The importer **parses only**; every write goes through `PlanningFeatureService.ImportAsync` (plan amendment A4 — the fourth method on that service, added by this task), so upsert-by-`(Kind, Order)`, idempotency, conflict translation and the materialization sync are not re-implemented here:
- `BacklogImportRequest(string BacklogPath, Guid ProjectId)`.
- Root resolution: `<path>` is either a repo root containing `BACKLOG/` or the `BACKLOG/` folder itself; anything else → a clear failure.
- Per feature folder (skip `_TEMPLATE` and folders without `feature.md`, recording them in `Skipped`):
  - slug = folder name; title = `FirstHeading` of `feature.md` (fallback: slug); status from frontmatter `status` (`draft|ready|in_progress|done`, unknown → `draft` + a `Skipped` note); `version` (fallback 1); `created` → `CreatedAt`.
  - `feature.md` body after frontmatter → `PlanningFeature.Body`.
  - `spec.md` / `plan.md` → artifacts of kind Spec/Plan; frontmatter `status: validated` → `Validate()` semantics with `FrozenAt` from the `validated:` date (fallback `created`); body after frontmatter → `Body`.
  - `tasks/NN-*.md` → kind Task, `Order = NN` (parse the leading digits, ignore unparsable filenames with a `Skipped` note), status from frontmatter (`todo|in_progress|review|done|blocked`, unknown → `todo`), `review` → `PlanningReviewLevel` (default `light`), `created` → `CreatedAt`, body after frontmatter → `Body` (the `<log>` section rides along inside the body verbatim).
  - Idempotency and persistence are the service's job, not the importer's: `PlanningFeatureService.ImportAsync` (added by this task) matches an existing feature by `(ProjectId, Slug)`, restores new features/artifacts through the `Restore` factories from task 01b so `Version`, `CreatedAt`, `UpdatedAt` and `FrozenAt` come from frontmatter instead of `DateTime.UtcNow`, and updates existing ones through `UpdateBody`/`SetStatus` with the log note `"re-imported from <relative path>"` so the version history shows why a body changed. Artifacts upsert by `(Kind, Order)` — for Spec/Plan/Result the kind alone is the key. Artifacts that exist in the DB but not on disk are **never** deleted (they may have been created in the UI) — report them in `Skipped` as `"kept: <kind> <order> not present on disk"`.
  - `requirements.md` and any other unrecognized file → ignored, listed in `Skipped`.
- Unit of work: `ImportAsync` on the service is one transaction per feature (restore-or-update the feature plus its artifacts), committed before the materialization sync; a failure rolls that feature back and surfaces as `PlanningException`, and the importer rethrows with the offending relative path so the CLI log names the broken file. Features already imported stay imported — a partial run is resumable, which is what idempotency buys.

Host wiring (`src/Overmind.Host/Program.cs`):
- `--import-backlog <path>` plus optional `--project <id|name>`; the next argument after the flag is the value (mirror how `--no-tray` is scanned).
- Runs after migrations, before `app.StartAsync()`.
- Project resolution: `--project` by id (guid) or exact name; otherwise the single registered project whose normalized `DiskPath` equals the repo root (parent of `BACKLOG/`). No match or several matches → log an error naming the candidates and continue booting.
- Import failure never prevents startup: catch, log the error with the path, continue. Success logs `Imported planning from <path>: N features (M new), K artifacts, S skipped`.
- A `BacklogImportService`-style helper in Host is fine, but keep `Program.cs` readable: extract the flag handling into `src/Overmind.Host/BacklogImportCommand.cs` (static, one method).

Tests — `tests/Overmind.Infrastructure.Tests/BacklogImporterTests.cs` against a temp fixture tree built in the test (write the files, don't depend on the repo's own BACKLOG):
- full folder → feature fields (slug/title/status/version/createdAt), spec validated with `FrozenAt` from the `validated:` date, plan draft, two tasks with `Order` 1 and 2, statuses and review levels mapped;
- `<log>` lines survive verbatim inside the task body;
- missing `spec.md`/`plan.md`/`tasks/` tolerated;
- unknown status value → default + `Skipped` note;
- `_TEMPLATE` folder and a folder without `feature.md` skipped;
- re-run is idempotent: same counts of features, no duplicates, artifacts updated not doubled, and the update log note present;
- an artifact that exists in the DB but not on disk is kept and reported;
- root resolution accepts both a repo root and a `BACKLOG/` path, rejects anything else.
</scope>

<anti-scope>
- No materialization calls (task 05 owns disk writes); the importer only reads.
- No HTTP endpoint for import — CLI flag only.
- No import of `BACKLOG.md` index statuses, no cross-feature dependency parsing, no `requirements.md`.
- No UI.
</anti-scope>

<constraints>
- `dotnet build && dotnet test` fully green.
- No new packages (hand-rolled frontmatter parsing). English only. No comments unless non-obvious.
- Import never touches the user's real `~/.overmind/overmind.db` in tests — temp DB + temp fixture tree only.
- Startup must remain bootable when the import fails.
- No git commits — report the changed-file list.
</constraints>

<acceptance-criteria>
- [x] A BACKLOG folder imports into features + spec/plan/task artifacts with statuses, `FrozenAt`, review levels and task order from `NN-` prefixes.
- [x] `<log>` history is preserved inside artifact bodies.
- [x] Re-running the import changes nothing structurally (idempotent by slug / kind+order) and leaves a log note.
- [x] Missing or malformed files are reported in `Skipped`, never crash the import.
- [x] A failed or ambiguous import logs clearly and the Host still starts.
</acceptance-criteria>

<refs>
- @../spec.md — "Migration" (one-off importer, idempotent by slug), R7
- @../plan.md — Task 06 + Risks (importer fidelity)
- @./05-planning-materialization.md — the on-disk layout that must round-trip
- src/Overmind.Host/Program.cs — startup and flag handling
- BACKLOG/005-tree-workspace/ — a real corpus sample (feature.md + tasks/, no spec/plan)
</refs>

<obstacles>
- **Hit during execution 2026-09-13 — status spelling drift between task 05's materializer and this task's contract.** The contract vocabulary is `in_progress`, but `PlanningMaterializer.FeatureStatus` writes `inProgress` and task statuses go through `ToString().ToLowerInvariant()`, which produces `inprogress`. A strict importer would degrade every in-progress feature/task coming back from a materialized tree to the default + a `Skipped` note. Resolution: tolerant mapping — `in_progress`, `inProgress` and `inprogress` all map to `InProgress` for both kinds; genuinely unknown values still default + note. Pinned by `Import_MaterializerStatusSpellings_Mapped`. The materializer itself is untouched (task 05 is done/reviewed); if its spelling is ever unified, the importer keeps working.
- **Hit during execution 2026-09-13 — feature titles would compound across materialize→import cycles.** The materializer writes the H1 as `# Feature {nnn} — {Title}`, so a naive `FirstHeading`-as-title import stores `Feature 010 — Demo…` as the title and the next materialization writes `# Feature 010 — Feature 010 — Demo…`. Resolution: the importer strips the `Feature {featurePart} — ` prefix built from `BacklogNaming.SplitSlug(folderName)` — this is where the importer consumes `BacklogNaming` (A3); the whole repo corpus (001–006) follows the same heading shape, so stripping is corpus-correct too.
- **Hit during execution 2026-09-13 — idempotent re-run vs the re-import log note are in tension.** Always calling `UpdateBody` with the note leaves a note on every run (contract letter) but bumps versions/snapshots forever on an unchanged tree (contract spirit: "changes nothing structurally"). Resolution: skip artifacts whose body, status and review are unchanged, and recognize bodies that differ only by a previous re-import note — `PlanningFeatureService.MatchesImportedBody` re-applies `PlanningLog.Append(incoming, note, recordedDate)` and compares, so the note is appended exactly once per real edit (third run stays at version 2, pinned by `Import_ReRunAfterEdit_UpdatesWithNoteAndStaysStable`).
- **Hit during execution 2026-09-13 — EF nav fixup duplicated imported artifacts.** Adding a restored artifact to BOTH `DbSet<PlanningArtifact>` and the parent's `Artifacts` list put the same instance into the list twice (manual add + EF's Add-time relationship fixup), so any later `Include`/assert saw doubles (8 where 4 belonged). Resolution: only `_db.PlanningArtifacts.Add(restored)` — EF fixup populates the collection — plus an `imported` id set so the kept-check does not flag freshly created rows (fixup adds them to the collection before the orphan scan runs).
- **Scope edge — `result.md` is deliberately not imported** (the task scope enumerates only feature/spec/plan/tasks); a materialized `result.md` lands in `Skipped` as ignored. Round-trip is still lossless in the materialize→import→materialize direction: the DB Result artifact is never deleted by import ("kept" applies only to the DB-not-on-disk direction), so re-materialization rewrites the file from the DB copy.
- **Known infidelity — re-freezing an existing artifact stamps `FrozenAt = now`.** The entity offers no mutation that sets `FrozenAt` to an arbitrary date (`Validate()` stamps `UtcNow`; `Restore` is create-only). Fresh imports are exact (`Restore` carries the `validated:` date); only the update path where disk says `validated` but the DB copy is draft/body-changed re-freezes with the import timestamp — the ceremony-equivalent of a human clicking Approve.
- **Deviation from "one public method"** — `BacklogImporter` also exposes `public static string? ResolveBacklogDirectory(string)`: `BacklogImportCommand` must resolve the repo root BEFORE importing (to match `--project`/`DiskPath` against it), and duplicating the root-resolution rule in Host would let the CLI and the importer drift. The instance API remains the single `ImportAsync`.
</obstacles>

<log>
2026-09-08 — task created from the validated plan.
2026-09-13 — implemented. `BacklogMarkdown` (frontmatter/date/H1 parsing), `BacklogImporter` + request/report/feature/artifact DTOs, `PlanningFeatureService.ImportAsync` (one transaction per feature, Restore on create, UpdateBody/SetStatus with the "re-imported from <path>" note on change, kept-artifact reporting, materialization sync after commit), `PlanningFailureCode.ImportFailed` + factory (additive, `PlanningErrors.Map` handles the Kind already). `BacklogNaming` gained `TryParseTaskOrder` so the `NN-` write/read rule lives in one type. Host: `BacklogImportCommand.TryRunAsync` wired in `Program.cs` after migrations (flag handling mirrors `--no-tray`; `--project` by guid or exact name; else single project whose `DiskPath` equals the repo root; every failure logs and boot continues), `BacklogImporter` registered in DI. S4 riding item landed: `IsUniqueViolation` + the `SyncAsync`/`ClearAsync` swallow-and-log wrappers extracted into `PlanningServiceGuard` (internal, `Infrastructure/Planning/`); `PlanningFeatureService` and `PlanningArtifactService` refactored onto it, behavior identical (`PlanningServiceTests` 24/24 unchanged, incl. both swallow-and-log pins). New tests: `BacklogImporterTests` 10 (full-folder mapping, verbatim `<log>`, feature-only folder, unknown statuses/levels, materializer spellings, template/featureless/stray skips, idempotent re-run, edit+re-import note stability, kept artifact, root resolution). CLI verified by live host boots against a temp DB: bad path → ERR log + `UI ready`; no project → candidates named + `UI ready`; registered project → `Imported planning from …: 1 features (1 new), 0 artifacts, 0 skipped`. `dotnet build Overmind.sln` 0 warnings / 0 errors; `dotnet test Overmind.sln --no-build` 375/375 (Core 85, Infrastructure 122 = 112+10, Api 134, Mcp 34; baseline 365). `dotnet format --verify-no-changes`: no findings in touched files (6 pre-existing in generated migrations + `McpProcessDriver`, untouched). No git commits per constraints.
</log>
