---
feature: 003
task: 01
slug: bundled-origin
status: done
review: light
created: 2026-09-16
---

# 003/01 — Bundled origin semantics in Core + API

<goal>
`SkillKit` natively understands the bundled origin: `MarkBundled(sourcePath)` stamps kind+path without `ImportedAtUtc`; `ClearSource()` refuses a bundled kit with a typed failure surfaced as 409 by the API. The importer commit path accepts a bundled mode (same pipeline, different origin stamping).
</goal>

<context>
Feature 003a makes "bundled" a first-class kit source riding the 002a import pipeline. This task lays the Core semantics the boot seeding (task 02) and edit-fork (task 03) build on. It also closes the open NIT-3 from the 002/02 hard review ("SkillKit.ClearSource() will accept a Bundled kit once 003 introduces that kind"). Executor reads ONLY this file.
</context>

<scope>
- `src/Overmind.Core/Entities/SkillKit.cs` — `MarkBundled(string sourcePath)` (sets `SourceKind = Bundled`, `SourcePath`, leaves `ImportedAtUtc` null); `ClearSource()` throws the typed failure when `SourceKind == Bundled`
- Typed failure: extend `src/Overmind.Core/Abstractions/KitImportException.cs` with a `BundledSourceProtected`-style kind (follow the existing kind pattern); `KitImportFailureKind`/messages consistent with the house vocabulary
- `src/Overmind.Infrastructure/Kits/KitImporter.cs` — commit accepts a bundled flag/mode: kit origin via `MarkBundled` instead of `MarkImported`, everything else identical (entries, hashes, SkillFiles)
- `src/Overmind.Host/Api/KitImportErrors.cs` — map the new kind → 409
- `KitImporter.ClearSourceAsync` path: bundled kit → typed failure (no silent conversion)
- Tests: Core (entity behavior incl. refusal), Api (409 on clear-source of a bundled kit; bundled commit stamps origin correctly via the API-adjacent infra test if the endpoint doesn't expose bundled yet — a direct KitImporter test is acceptable here)
</scope>

<anti-scope>
- No boot hook / seeding (task 02); no edit-fork (task 03); no UI
- No new endpoints — bundled commit is service-level only in this task
- No migration (origin columns exist from 002)
</anti-scope>

<constraints>
- No new NuGet packages; no comments; English-only messages
- `dotnet build Overmind.sln` + `dotnet test Overmind.sln` green
- No auto-commits
</constraints>

<acceptance-criteria>
- [x] `MarkBundled` stamps kind+path, `ImportedAtUtc` stays null (pinned by test)
- [x] `ClearSource()` on a bundled kit throws the typed failure; on imported/manual kits behaves exactly as before (pinned)
- [x] API maps the new failure kind to 409 with explanatory detail (pinned)
- [x] Bundled-mode import commit produces a kit with correct origin fields (pinned)
- [x] Full suite green
</acceptance-criteria>

<refs>
- @../spec.md — D5, R4 (reviewer reads, not executor)
- @../plan.md — task 01 row
- tasks/02-boot-seed-sync.md, tasks/03-edit-fork.md — consumers of these semantics
</refs>

<obstacles>
</obstacles>

<log>
2026-09-16 — implemented.

Core:
- `SkillKit.MarkBundled(string sourcePath)` — mirrors `MarkImported` guard/trim, sets `SourceKind = Bundled`, explicitly nulls `ImportedAtUtc` (postcondition holds even when re-stamping a previously imported kit; pinned by test)
- `SkillKit.ClearSource()` — throws `KitImportException.BundledSourceProtected(Id)` when `SourceKind == Bundled`; imported/manual paths untouched
- `KitImportFailureKind.BundledSourceProtected = 8`; factory `KitImportException.BundledSourceProtected(Guid kitId)` with message "Kit '{kitId}' is bundled; its source is protected and cannot be cleared." (house vocabulary: statement; clarification)

Infrastructure:
- `KitImporter.CommitAsync` gained `bool bundled = false` before `ct`; threaded into `CommitKitAsync`, which stamps origin via `MarkBundled` instead of `MarkImported` — entries, hashes, SkillFiles, transactions, failure handling identical
- Single existing call site (`KitImportApi.Import`) updated to `CommitAsync(request.Path, selections, bundled: false, ct)` — named arg keeps endpoint semantics explicit; no endpoint exposes bundled in this task
- `ClearSourceAsync` needs no extra code: the entity-level throw fires before `SaveChangesAsync`, so the typed failure propagates and nothing is persisted

Host:
- `KitImportErrors.Map`: `BundledSourceProtected` added to the 409 Conflict arm (title = kind name, detail = message)

Tests (9 new cases, all passing):
- Core (`EntityFactoryTests`/`SkillKitTests`): MarkBundled stamps + trims + null stamp (incl. re-stamp over imported), empty-path Theory, ClearSource refusal keeps origin
- Infra (`KitImporterTests`): bundled commit stamps Bundled/path/null-import with full entry+hash linking; ClearSourceAsync on bundled kit throws typed failure, kit survives unchanged
- Api (`KitImportApiTests`): bundled kit seeded via scoped `OvermindDbContext` (no bundled endpoint exists yet — service-level only per anti-scope); clear-source returns 409 with title `BundledSourceProtected` and explanatory detail; GET confirms origin survives

Verification: `dotnet build Overmind.sln` — 0 warnings, 0 errors. `dotnet test Overmind.sln` — Core 100, Infrastructure 187, Api 159, Mcp 59 passed; 0 failed, 0 skipped (505 total).
</log>
