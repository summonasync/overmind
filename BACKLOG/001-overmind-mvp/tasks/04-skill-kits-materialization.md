---
feature: 001
task: 04
slug: skill-kits-materialization
status: done
review: hard
created: 2026-09-06
---

# 001/04 — Skill-kits and materialization engine

<goal>
Skill-kits (named, versioned sets of entries) can be created, linked to projects, and activated. Activation materializes the kit's skills/gates/guards into the project's native agent directories on disk; deactivation/switch removes them. Drift (external modification of materialized files) is detected. All verified by unit tests against temp directories, plus API endpoints wired in Host.
</goal>

<context>
Spec R6 + skill-engine decision: agents load skills only from the filesystem, so DB is the source of truth and disk is a managed cache. v1 materializes INTO the project directory (universal); external-folder materialization is P1 (out of scope). This is the heart of the product — mistakes here write garbage into user projects, hence `review: hard`.
</context>

<scope>
`src/Overmind.Core/`:
- `IMaterializer` interface + `MaterializationReport` / `DriftItem` DTOs (signatures below; Core owns contracts, Infrastructure implements).

`src/Overmind.Infrastructure/Materialization/`:
- `platforms.json` (embedded resource, loaded at startup) — per-platform directory mapping:
  - Claude → `.claude/skills/`, KimiCode → `.kimi-code/skills/`, Codex → `.codex/skills/`, Gemini → `.gemini/skills/`, Antigravity → `.antigravity/skills/` (relative to project DiskPath).
  - JSON shape: `{ "claude": { "skillsDir": ".claude/skills" }, ... }` — fixable without recompile.
- `SkillMaterializer : IMaterializer`:
  - `Task<MaterializationReport> MaterializeAsync(Guid projectId, Guid kitId, CancellationToken ct)` — for each Skill entry in the kit with SkillFiles: write files under `<DiskPath>/<skillsDir>/<skill-slug>/<RelativePath>` for every platform present in the kit's SkillFiles. Gates/guards from the kit are compiled into a single bootstrap file `<skillsDir>/overmind-bootstrap/SKILL.md` per platform (content: instruct the agent to read `overmind://projects/<id>/gates` and `guards` at session start, plus inline copies of gate/guard text).
  - `Task<MaterializationReport> DematerializeAsync(Guid projectId, Guid kitId, CancellationToken ct)` — removes exactly the files recorded in `MaterializedFile` rows; never deletes anything not recorded.
  - `Task<IReadOnlyList<DriftItem>> DetectDriftAsync(Guid projectId, CancellationToken ct)` — compares recorded SHA-256 of each `MaterializedFile` to the file on disk; reports missing/modified.
- `MaterializedFile` entity (new migration): Id, ProjectId, KitId, Platform, RelativePath, Sha256, MaterializedAt.
- Materialize/dematerialize are transactional: DB rows and files written together; on file-IO failure, roll back DB and report which path failed.
- Overwrite rule: materializing over a path that exists on disk but is NOT tracked in `MaterializedFile` → fail with a clear error naming the path (never silently clobber user files).

`src/Overmind.Host/Api/`:
- `KitsApi` — `GET/POST /api/kits`, `GET/PUT/DELETE /api/kits/{id}`, `POST /api/kits/{id}/entries/{entryId}` / `DELETE` (add/remove entries), `POST /api/projects/{projectId}/kits/{kitId}/activate|deactivate`, `GET /api/projects/{projectId}/drift`.

Tests (`tests/Overmind.Infrastructure.Tests/` + Api tests):
- Materialize a kit with 2 skills (Claude + KimiCode variants) into a temp project dir → exact files exist with exact content; `MaterializedFile` rows match.
- Dematerialize → files gone, unrelated files in the dir untouched.
- Switch kit (deactivate A, activate B) → only B's files remain.
- Drift: modify one file externally → `DetectDriftAsync` reports exactly it; delete one → reported missing.
- Overwrite-protection: pre-existing untracked file at target path → materialize fails, names the path.
- Bootstrap SKILL.md contains gate/guard content and the `overmind://` read instruction.
</scope>

<anti-scope>
- No external-folder materialization (`~/.overmind/kits/`), no agent launching, no `--add-dir`/`--skills-dir` — that is P1, do not stub it.
- No MCP delivery of skills (P2).
- No UI (task 07 calls these endpoints).
- No changes to Core entity shapes (task 02 owns them; if a gap is found, stop and report — do not silently extend).
</anti-scope>

<constraints>
- All file writes via async IO; paths combined with `Path.Combine`, rooted under project DiskPath; reject `..` escapes from RelativePath (path traversal guard, unit-tested).
- SHA-256 for drift hashes.
- File content written UTF-8 without BOM, LF endings.
- platforms.json parse errors at startup → host fails fast with clear message.
</constraints>

<acceptance-criteria>
- [ ] All tests listed in scope pass; `dotnet test` green overall.
- [ ] Path traversal attempt (`RelativePath` = `../../evil.md`) rejected with 400 from API.
- [ ] Untracked-file overwrite protection proven by test.
- [ ] Drift detection proven by test (modified + missing cases).
- [ ] API roundtrip: create kit → add entry → link+activate → files on disk; deactivate → gone.
</acceptance-criteria>

<refs>
- @../plan.md — task 04
- @../spec.md — R6, decisions/skill-engine
- tasks/02-domain-and-persistence.md — entities (must be done first)
- tasks/03-core-api.md — API conventions (can be built in parallel, but endpoints need its Program.cs)
</refs>

<obstacles>
- `../../evil.md` does NOT escape the project root when naively combined as `<skillsDir>/<slug>/<RelativePath>` — `GetFullPath` resolves it to `<root>/.claude/evil.md` (still inside the root, so a resolved-path-only guard silently passes and writes garbage into the project). Guard therefore rejects any `..` path SEGMENT in the raw `SkillFile.RelativePath` and any rooted/invalid path upfront (`PathGuard.ResolveUnderRoot`), plus a final resolved-path containment check. Unit-tested both via `../../evil.md` and an absolute-path `RelativePath`.
- Overwrite preflight must run BEFORE any file is touched (one blocked path must leave zero partial writes and zero DB rows) — tested explicitly.
- Re-materializing an already-active kit must UPDATE existing `MaterializedFile` rows (Refresh Sha256/MaterializedAt), not insert duplicates — caught by `Materialize_ReMaterializingOwnFiles_Succeeds`.
- `dotnet build` failed with locked DLLs: a running `Overmind.Host.exe` (PID 45196) from manual smoke-testing held the output. Killed it; reviewers/tests should stop stray host processes first.
- Known v1 behavior (not a bug per scope): if a kit's skill files are edited between two activations, files removed from the kit stay on disk as tracked orphans until dematerialization — they remain visible to drift detection and are cleaned by dematerialize.
- Empty skill directories created by materialization are pruned on dematerialize only when fully empty (never deletes anything containing entries); verified against a foreign `KEEP.md` placed inside `.claude/skills/`.
- Review-gate round: B1 chose the API-level invariant (409 on DiskPath change while active) over persisting absolute paths — `MaterializedFile` still stores root-relative paths; the invariant lives only in `ProjectsApi.Update`, so non-API writers (future UI/MCP) must respect it too or the foreign-delete hazard returns. DiskPath comparison is case-insensitive on Windows so same-path renames of casing are not blocked.
- I1: `Activate` commits the link AFTER `MaterializeAsync` succeeds; the engine provably does not read `ProjectKit` (only Project + SkillKit + SkillFile). `Deactivate` was already dematerialize-then-save. Note the two saves are not one transaction (materializer commits its own tx first); a crash between them could leave files+rows without an active link — harmless for safety (dematerialize/drift don't need the link), only the API-level "active" flag is lost.
- I2 quirk: with the N3 preflight fix, any disk state that makes `WriteAllTextAsync` throw AND is visible at preflight time now surfaces as OverwriteBlocked (409), not IoError — so a true mid-write IO failure is only reachable via a blocker invisible to preflight (test uses a file where a parent directory must be created).
- Single-active-kit rule (ADR) is enforced ONLY in `KitsApi.Activate` — same API-level-invariant caveat as B1: non-API writers (future UI/MCP) must check `ProjectKits` themselves or the invariant breaks. `SkillMaterializer` deliberately allows materializing with another kit's files on disk (the overwrite preflight still protects untracked files).
- The 409 message names the active kit by name AND id (name alone is not unique — kits are name+versioned, no unique constraint).
</obstacles>

<log>
2026-09-06 — writing-plans — task created from approved spec.
2026-09-06 — coder — implemented full scope: Core contracts (`IMaterializer`, `MaterializationReport`/`MaterializedFileInfo`, `DriftItem`/`DriftKind`, `MaterializationException`/`MaterializationFailureKind`); Infrastructure `Materialization/` (`MaterializedFile` entity + migration `20260906214939_MaterializedFiles`, embedded `platforms.json` + fail-fast `PlatformDirectoryMap.Load()`, `PathGuard`, `SkillMaterializer` with transactional materialize/dematerialize, overwrite preflight, exact-file cleanup with empty-dir pruning, SHA-256 drift detection); Host `KitsApi` (kit CRUD, add/remove entry, activate/deactivate, drift) + contracts/mappers/validators + DI registration (`PlatformDirectoryMap` singleton, `IMaterializer` scoped). Tests: 9 materialization tests in `Overmind.Infrastructure.Tests` (exact-bytes UTF-8-no-BOM/LF materialization, bootstrap content + overmind:// links, dematerialize leaves untracked files, kit switch, drift modified+missing, overwrite protection, re-materialize idempotency, `../../evil.md` + absolute-path traversal) and 5 API tests in `Overmind.Api.Tests` (full roundtrip create→add→activate→files-on-disk→deactivate→gone, traversal→400, untracked-file→409, drift via API, kit entry listing). TDD loop caught and fixed 3 real bugs (row duplication on re-materialize, traversal absorbed by `<skillsDir>/<slug>/` prefix, partial-write on blocked overwrite). `dotnet build` 0 warnings; `dotnet test` 79/79 green.
2026-09-06 — coder — review-gate fixes applied. B1: `ProjectsApi.Update` rejects DiskPath change with 409 ("deactivate kits first") while active ProjectKit links or MaterializedFile rows exist (regression test: 409 while active → succeeds after deactivate). I1: `KitsApi.Activate` now materializes BEFORE creating/activating the link — verified `SkillMaterializer` never reads `ProjectKit`, so nothing extra needed; `Deactivate` confirmed already dematerialize-first. I2: IO-rollback test added (file where a parent dir must be created → mid-write IOException → 0 rows, 0 leftover files, IoError naming the path). N3: preflight now treats a directory at a target path as OverwriteBlocked (409) instead of a later 500. `dotnet build` 0 warnings; `dotnet test` 83/83 green.
2026-09-06 — coder — ADR "Single active kit per project" implemented: `KitsApi.Activate` returns 409 naming the currently active kit (name + id) and instructing to deactivate it first when another kit is active for the project; re-activating the same kit stays idempotent (re-materialize, 200). Engine (`SkillMaterializer`) intentionally has no such rule — the invariant is API-level, matching the B1 DiskPath invariant. No existing test activated two kits via the API; engine-level `SwitchKits_LeavesOnlySecondKitFiles` already deactivates explicitly and stays valid. New tests: `Activate_SecondKitWhileAnotherActive_Returns409ThenSwitchSucceeds` (409 names pack-alpha, A's files intact, B wrote nothing, drift clean → deactivate A → activate B → only B's files) and `Activate_AlreadyActiveKit_Returns200Idempotent`. `dotnet build` 0 warnings; `dotnet test` 85/85 green.
</log>
