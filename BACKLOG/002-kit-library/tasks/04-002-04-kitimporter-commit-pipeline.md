---
feature: 002
task: 04
slug: kit-library
status: done
review: light
created: 2026-09-14
---

# 002/04 — KitImporter: commit pipeline

<goal>
`KitImporter.CommitAsync` turns scan selections into real kits: per selected candidate, in its own transaction, it creates the `SkillKit` (imported origin), entries (skills with per-platform `SkillFile`s, gates, guards), `SkillKitEntry` links with `ImportedContentHash`, and returns a per-kit report. A failing kit rolls back fully; other kits import normally. Plus `ClearSourceAsync(kitId)` converting an imported kit back to manual.
</goal>

<context>
Second phase of the two-phase import (scan = task 03). Multiple write paths will exist for kits (folder import now, `.omkit` and bundled kits later), so import logic lives in a service, not in API handlers — same extraction trigger as the planning services. Entry creation must reuse the existing domain factories (`ContextEntry.CreateSkill` etc. — check how `KitsApi`/entries flows create skill entries today and mirror it) so imported kits are indistinguishable from hand-made ones. Executor reads ONLY this file.
</context>

<scope>
- `src/Overmind.Infrastructure/Kits/KitImporter.cs` — `CommitAsync(scanPath, selections)` and `ClearSourceAsync(kitId)`
- Per-kit unit: load scan candidate → `SkillKit` + `MarkImported(sourcePath)` (task 02) → entries via existing domain factories → `SkillKitEntry` links with `ImportedContentHash` computed via `ContentHash` (same normalization as materialization: CRLF/CR→LF, UTF-8 no BOM, SHA-256) → `SaveChangesAsync` in one transaction per kit
- Platform-agnostic skills get a single `SkillFile` marked agnostic — check `src/Overmind.Core/Entities/SkillFile.cs` for how platform is keyed today and pick the representation that fits existing invariants (nullable platform or dedicated marker); record the choice in `<log>`
- Name collision at commit time → that kit fails with `KitImportException` (duplicate-name kind), other selections unaffected; per-kit report entries: created kit id/name or failure reason
- `ClearSourceAsync` → `SkillKit.ClearSource()` + persist; clearing an already-manual kit is a no-op success
- Rejecting a selection whose candidate is flagged as re-import match (must go through re-import, task 06) with a typed failure
- Tests: `tests/Overmind.Infrastructure.Tests/` — happy path (multi-kit, platform variants, agnostic, gates/guards), hash correctness, name-collision isolation (one kit fails, the other commits), rollback leaves no partial kit, clear-source
</scope>

<anti-scope>
- No re-import/update logic (task 06)
- No `SkillMaterializer` changes (task 05)
- No API endpoints (task 07), no web UI (task 08)
- No changes to existing entry-creation API handlers — reuse domain factories, don't refactor handlers
</anti-scope>

<constraints>
- No new NuGet packages
- `dotnet build Overmind.sln` + `dotnet test tests/Overmind.Infrastructure.Tests` green
- No comments in code; no auto-commits
</constraints>

<acceptance-criteria>
- [ ] Committing a two-kit selection creates both kits fully queryable through existing kit read paths, with origin = imported and correct `SourcePath`
- [ ] `ImportedContentHash` on every created link equals `ContentHash` of the normalized file content
- [ ] Forced failure on one kit leaves zero partial rows for it and does not affect the other kit
- [ ] Duplicate kit name fails only that kit with the typed failure
- [ ] `ClearSourceAsync` converts to manual (nulls); imported kit after clearing still works via existing kit APIs
- [ ] Build + Infrastructure.Tests green
</acceptance-criteria>

<refs>
- @../spec.md — D1, D7, D9, R5–R7, R10 (reviewer reads, not executor)
- @../plan.md — task 04 row
- tasks/02-origin-model.md — origin fields/methods
- tasks/03-kit-scanner.md — scan model consumed here
- tasks/06-kit-reimport.md — sibling update flow in the same service
</refs>

<obstacles>
- **Agnostic `SkillFile` representation (recorded per scope requirement): chose nullable platform — `SkillFile.Platform` is now `AgentPlatform?`, `null` = platform-agnostic.** Matches plan.md approach ("single SkillFile with a null/'any' platform marker") and the scan model (`KitScanSkill.Platform` was already `AgentPlatform?`). Costs: migration `20260914151947_SkillFileAgnosticPlatform` (Platform INTEGER → nullable); `SkillFileDto.Platform` widened to nullable (additive on the wire — MCP adapter does not read it, verified; web TS types are task 08's problem); forced 3-line compile fix in `SkillMaterializer.BuildPlans` (anti-scope "no materializer changes" bent minimally: agnostic files are skipped there until task 05 implements the expansion to every platform dir — hand-made kits are unaffected, all materialization tests green).
- **Imported entries need a `ProjectId` (`ContextEntry` FK is required) but the contract signature `CommitAsync(scanPath, selections)` carries none, and no system-project concept existed.** Decision: importer find-or-creates a dedicated library project named `Kit Library` (find by name, idempotent across runs; on create-race unique violation → re-query; DiskPath = directory of the current SQLite data file via `GetDbConnection().DataSource`). The project is visible in project lists — spec never sanctioned a hidden project; if the UI must hide it, that's a task-08/09 call.
- Imported `SkillFile.RelativePath` is stored relative to the skill directory (`SKILL.md`, `scripts/run.sh`), not the kit root — otherwise files of same-named skills collide and materialization would double-nest; materializer writes `<skillsDir>/<slug>/<rel>`.
- `ImportedContentHash` = `ContentHash.Sha256(entry.Content)` where skill entry Content = raw SKILL.md and gate/guard Content = whole .md file — the re-import diff (task 06) keys on the same content.
- Empty gates/guards are skipped at commit (no entry created) — works around the open NIT-8 scanner gap (zero-byte gate files get no `EmptyFile` issue in preview); an empty SKILL.md instead fails the whole kit via the `ContextEntry` content invariant (the preview does flag it). If the scanner gains the NIT-8 issue, revisit the skip.
- Rollback test coverage: the "zero partial rows" test forces failure at entity-construction time (empty SKILL.md), which is the realistic failure mode; the SaveChanges-time generic catch (tx rollback + Added-detach so the next kit stays clean) has no deterministic way to force a DB constraint violation mid-commit — verified by inspection, not pinned by a test.
- `KitScanner`/`KitImporter` deliberately NOT registered in Host DI — review-002-03 set the precedent ("task 07 owns the API surface").
</obstacles>

<log>
2026-09-14 — Executed. Core: `SkillFile.Platform` → `AgentPlatform?` (null = agnostic — decision recorded in <obstacles>); `SkillKit.AddEntry(entryId, importedContentHash = null)` + `SkillKitEntry.Create` hash param (task 02 left the hash unsettable; writer added now, task 06 reuses); `KitImportFailureKind` extended with explicit values (DuplicateName/ReImportRequired/CandidateNotFound/KitNotFound — NIT-7 from review-002-03 folded in) + matching `KitImportException` factories. Infrastructure: `Kits/KitImporter.cs` (`CommitAsync(scanPath, selections)` — rescans via `KitScanner`, find-or-creates the `Kit Library` project once, then per selection: own transaction, candidate lookup → re-import-match rejection (typed) → live OrdinalIgnoreCase name-collision check → `SkillKit.Create`+`MarkImported` → entries via `ContextEntry.CreateSkill`/`Create` (SKILL.md content = entry Content, all skill-dir files become `SkillFile`s, symlinks skipped, empty gates/guards skipped) → links with `ContentHash.Sha256` → single `SaveChanges` + commit; a failing kit rolls back and is reported, other kits proceed; generic catch also detaches Added entities so the tracker can't poison the next kit) and `ClearSourceAsync(kitId)` (typed KitNotFound, no-op success on already-manual). Models: `KitImportSelection` (record), `KitImportReport`, `KitImportKitResult` (KitId or FailureKind+Error). Migration `20260914151947_SkillFileAgnosticPlatform`. Ripple: `SkillFileDto.Platform` nullable, 3-line `SkillMaterializer` compile fix (agnostic files not materialized until task 05). Tests: `KitImporterTests.cs` — 10 cases (two-kit full link incl. platform variant + agnostic + nested file + gates/guards + hash-on-every-link + library project; edited name; CRLF hash vs independently computed SHA-256/LF; case-insensitive name-collision isolation; failed-kit zero partial rows + good kit intact; re-import-match typed rejection; unknown-candidate typed failure; empty gate skip; clear-source + idempotency + typed not-found). Verification: `dotnet build Overmind.sln` 0/0; `dotnet test Overmind.sln` 437/437 green (Core 93, Infrastructure 150, Api 135, Mcp 59); scoped `dotnet format --verify-no-changes` clean on all 12 touched files.
</log>
