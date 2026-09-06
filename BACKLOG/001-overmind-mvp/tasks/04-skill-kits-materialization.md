---
feature: 001
task: 04
slug: skill-kits-materialization
status: todo
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
  - `Task<MaterializationReport> MaterializeAsync(Guid projectId, Guid kitId, CancellationToken ct)` — for each Skill entry in the kit with SkillFiles: write files under `<DiskPath>/<skillsDir>/<skill-slug>/<RelativePath>` for every platform present in the kit's SkillFiles. Gates/guards from the kit are compiled into a single bootstrap file `<skillsDir>/overmind-bootstrap/SKILL.md` per platform (content: instruct the agent to read `ctx://projects/<id>/gates` and `guards` at session start, plus inline copies of gate/guard text).
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
- Bootstrap SKILL.md contains gate/guard content and the `ctx://` read instruction.
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
<!-- Filled by executors during work -->
</obstacles>

<log>
2026-09-06 — writing-plans — task created from approved spec.
</log>
