---
feature: 002
task: 03
slug: kit-library
status: done
review: light
created: 2026-09-14
---

# 002/03 — KitScanner: read-only folder scan

<goal>
`KitScanner.ScanAsync(path)` returns a complete import preview model: kit candidates (multi-kit detection), each candidate's skills with platform detection, gates/guards, per-file issues, name collisions against existing kits, and re-import matches by `SourcePath`. Pure read-only service — no writes, no transactions.
</goal>

<context>
Feature 002a import is two-phase: scan (this task) produces the preview the web dialog shows; the commit phase (task 04) consumes this model. Platform detection reuses `PlatformDirectoryMap` (`src/Overmind.Infrastructure/Materialization/`, mapping from embedded `platforms.json`): a skill under `<platform-dir>/skills/<skill>/` is a variant for that platform; any other skill directory is platform-agnostic. Gates/guards use Overmind's own convention: `gates/*.md` / `guards/*.md` at candidate root, one file = one entry, title from H1 (fallback: filename). Executor reads ONLY this file.
</context>

<scope>
- New folder `src/Overmind.Infrastructure/Kits/` with `KitScanner` + scan model types (keep them in Infrastructure; they are service DTOs, not domain entities)
- Scan rules:
  - A skill = a directory containing `SKILL.md`; nested files belong to the skill
  - Candidate = the scanned folder itself (if it directly contains skills/gates/guards) plus each TOP-LEVEL subdirectory containing skill content; one level only, no nested-kit detection
  - Suggested kit name = directory name
  - Per-file issues (unreadable, empty, invalid SKILL.md frontmatter where the format requires it) go into the candidate's issue list — broken files never abort the scan. Validation strictness follows the import gates G1–G5 in `tasks/01-format-spike.md` `<findings>` (canonical `name`, `description` ≤1024, `type` absent or `prompt`, YAML with line-1 `---`, agnostic skills not depending on platform-native fields): violations of G1–G4 on any skill and G5 on agnostic skills = per-file issues
  - Name collision flag when the suggested name matches an existing kit (read `OvermindDbContext` — reads are fine)
  - Re-import match: candidate whose absolute path equals an existing kit's `SourcePath` is flagged with that kit's id (import of it must go through re-import, not commit)
  - Ignore-list: hidden directories, `.git`, `node_modules`, `bin`, `obj`; do not follow symlinks; scan caps (500 directories / 10 MB total file size) with a `truncated` flag in the result
- Error model: introduce `KitImportException` + `KitImportFailureKind` in `src/Overmind.Core/Abstractions/` following the house pattern (`PlanningException`/`PlanningFailureKind` — find and mirror them); scanner raises it only for hard failures (path missing, not a directory)
- Tests: `tests/Overmind.Infrastructure.Tests/` with temp-dir fixtures covering: single kit, multi-kit, platform-marked + agnostic skills, gates/guards, broken files → issues, ignore-list, caps, name collision flag, re-import match
</scope>

<anti-scope>
- No DB writes, no kit creation (task 04)
- No re-import diff logic (task 06)
- No API endpoints (task 07), no web UI (task 08)
- No changes to `SkillMaterializer` or `PlatformDirectoryMap` behavior
</anti-scope>

<constraints>
- No new NuGet packages (frontmatter parsing is hand-rolled; hashing, if needed here, reuses `ContentHash` from `Infrastructure/Materialization`)
- `dotnet build Overmind.sln` + `dotnet test tests/Overmind.Infrastructure.Tests` green
- No comments in code; no auto-commits
</constraints>

<acceptance-criteria>
- [x] `KitScanner.ScanAsync` returns the full preview model for a fixture tree with two candidates, platform-marked and agnostic skills, gates/guards
- [x] Broken/unreadable files surface as per-file issues; scan completes
- [x] Name collision and re-import match flags verified against a seeded test DB
- [x] Missing path / not-a-directory raises `KitImportException` with the right kind
- [x] Build + Infrastructure.Tests green
</acceptance-criteria>

<refs>
- @../spec.md — D4, D5, D10, R1–R4 (reviewer reads, not executor)
- @../plan.md — task 03 row
- tasks/02-origin-model.md — provides `SourcePath`/origin fields the scanner matches against
- tasks/04-kit-importer.md — consumes the scan model
</refs>

<obstacles>
- Hidden-dir ignore-list vs platform dirs: hidden directories are skipped EXCEPT by directory NAME matching the first segment of a `PlatformDirectoryMap` skillsDir (`.claude`, `.kimi-code`, …) at ANY depth — root-relative prefix matching (original approach) silently dropped platform variants nested inside kit subfolders (`kit-a/.claude/skills/x`). Fixed per review IMPORTANT-1.
- Candidate ownership rule (interpretation): a top-level subdir becomes a kit candidate only when it contains skills nested strictly inside it (or gates/guards); a skill directory sitting directly at the scan root belongs to the root candidate. Prevents every root-level skill from being promoted to its own kit candidate. Confirmed by light review.
- `UnreadableFile` paths are implemented (SKILL.md/gates/guards read failures, stat/enum failures, symbolic links) but read-denial is not unit-tested: chmod-based denial is unreliable when the test runner executes as root. Broken-file coverage (InvalidFrontmatter/EmptyFile/InvalidName/InvalidDescription/InvalidType) and symlink coverage (dir + SKILL.md) are tested.
- 10 MB total-size cap not exercised by a test (fixture would be slow); the 500-directory cap covers the `truncated` flag.
- Symlink tests skip silently when symlink creation is denied (Windows without developer mode/elevation); cap test uses a wide-shallow tree (depth 2) to stay under MAX_PATH.
- `dotnet format --verify-no-changes` reports pre-existing IDE0161 violations in EF migration files from task 002/02 (untouched here); all task-03 files are format-clean.
</obstacles>

<log>
2026-09-14 — sub-agent (K2.7) — KitScanner implemented: Core error model (KitImportException/KitImportFailureKind), Infrastructure/Kits scan model + scanner (multi-kit detection, platform detection via PlatformDirectoryMap, G1–G5 per-file issues, gates/guards titles, ignore-list + caps, collision/re-import flags), 13 tests green; build + full Infrastructure.Tests (138) green.
2026-09-14 — sub-agent (K2.7) — review fixes (audits/review-002-03.md, PASS-WITH-NITS): IMPORTANT-1 platform exemption now name-based at any depth (regression test `kit-a/.claude/skills/deploy`); IMPORTANT-2 file reparse points skipped + surfaced as UnreadableFile issue (test: symlinked SKILL.md never read); NIT-1 cap test wide-shallow, symlink tests skip on Windows denial. Build green, Infrastructure.Tests 140/140.
</log>
