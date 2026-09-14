---
feature: 002
task: 02
slug: origin-model
status: done
review: hard
created: 2026-09-14
---

# 002/02 — Kit origin model + migration

<goal>
`SkillKit` knows where it came from: `SourceKind` (manual/imported/bundled), `SourcePath`, `ImportedAtUtc`. `SkillKitEntry` carries `ImportedContentHash` (nullable). One EF migration, additive API surface — existing kits are `manual` with nulls and every existing test stays green.
</goal>

<context>
Feature 002a imports kits from folders; origin fields power the library view (origin badge), re-import matching by `SourcePath`, and feature 003's bundled kits later. `ImportedContentHash` is the pivot of the three-way re-import diff (task 06). House rules: entities in Core with behavior on them (`SkillKit.Create`, `ProjectKit.Create` are the pattern); the Host↔MCP contract is additive-only; enums get explicit values and follow the persistence convention already used by the planning enums. Executor reads ONLY this file.
</context>

<scope>
- `src/Overmind.Core/Enums/KitSourceKind.cs` — new enum `KitSourceKind { Manual, Imported, Bundled }` with explicit values (check `src/Overmind.Core/Enums/` for the house style, e.g. planning enums)
- `src/Overmind.Core/Entities/SkillKit.cs` — add `SourceKind` (default `Manual`), `SourcePath` (nullable string), `ImportedAtUtc` (nullable DateTimeOffset — match the timestamp type used by other entities), plus `MarkImported(sourcePath)` and `ClearSource()` methods with invariants (manual kit ⇒ null `SourcePath`/`ImportedAtUtc`; `MarkImported` requires non-empty path)
- `src/Overmind.Core/Entities/SkillKitEntry.cs` — add nullable `ImportedContentHash`
- `src/Overmind.Infrastructure/Persistence/OvermindDbContext.cs` (+ entity configurations if split out) — map the new fields; check how planning enums are persisted and follow it
- One migration: `dotnet ef migrations add KitOrigin --project src/Overmind.Infrastructure`
- API additive surface: kit response contract + mapper (`src/Overmind.Host/Api/Contracts/`, `src/Overmind.Host/Api/Mapping/`) gain `sourceKind`, `sourcePath`, `importedAtUtc`
- Tests: Core.Tests for the new invariants; Api.Tests adjusted/extended so kit responses carry origin (existing tests must pass unchanged in behavior)
</scope>

<anti-scope>
- No scanner/importer/materializer logic (tasks 03–06)
- No new API endpoints (task 07)
- No web UI changes (task 08)
- No renaming/removal of any existing DTO field the MCP adapter reads (additive-only ADR)
</anti-scope>

<constraints>
- No new NuGet packages
- After code changes: `dotnet build Overmind.sln` must pass (known flake: `Internal CLR error 0x80131506` → `dotnet build-server shutdown` and retry)
- No comments in code; no auto-commits
</constraints>

<acceptance-criteria>
- [ ] `dotnet build Overmind.sln` — 0 errors
- [ ] `dotnet test Overmind.sln` — all green, incl. new Core.Tests covering `MarkImported`/`ClearSource` invariants
- [ ] Migration applies cleanly at Host startup on a fresh DB (Host boots, `/api/health` OK) and on an existing DB with kits (kits read back as `manual`, null path/date)
- [ ] Kit API responses contain `sourceKind`/`sourcePath`/`importedAtUtc`; MCP tests untouched and green
</acceptance-criteria>

<refs>
- @../spec.md — D7 (reviewer reads, not executor)
- @../plan.md — task 02 row
- @TECHNICAL-SOLUTIONS.md — ADR "Host↔MCP contract additive-only"
- tasks/03-kit-scanner.md, tasks/06-kit-reimport.md — consumers of these fields
</refs>

<obstacles>
- Task says `ImportedAtUtc` is "nullable DateTimeOffset — match the timestamp type used by other entities", but no entity in the codebase uses DateTimeOffset; every timestamp is `DateTime` with `DateTime.UtcNow` (PlanningArtifact, PlanningFeature, Project, etc.). Followed the house type: `DateTime? ImportedAtUtc`. Reviewer should confirm against spec D7.
- `OvermindDbContext.cs` needed NO changes: planning enums persist by convention as INTEGER (no HasConversion anywhere), and SkillKit/SkillKitEntry have no explicit configuration blocks — new fields map by convention. "Map the new fields; check how planning enums are persisted and follow it" resolves to convention mapping.
- Host serializes enums via `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` (DependencyInjection.cs:64-65), so `sourceKind` is the string `"manual"` on the wire; Api.Tests' local JsonOptions lack the converter, so KitJson asserts SourceKind as string.
- Existing-DB migration check done manually (no sqlite3 CLI): `dotnet ef database update 20260908173101_Planning --connection "Data Source=..."` to build a legacy-schema DB, seeded a kit via python3 stdlib sqlite3, booted Host headless with `Overmind__DatabasePath` override → KitOrigin applied, kit read back as `manual`/null/null, `/api/health` OK.
</obstacles>

<log>
2026-09-14 — Executed. Added KitSourceKind enum (explicit values), SourceKind/SourcePath/ImportedAtUtc on SkillKit with MarkImported/ClearSource invariants, ImportedContentHash on SkillKitEntry (private set — task 06 adds the writer), migration 20260914134204_KitOrigin (SourceKind INTEGER default 0 → existing rows become Manual), KitResponse+KitMapper gained sourceKind/sourcePath/importedAtUtc (appended, additive). Tests: 7 new cases in EntityFactoryTests.SkillKitTests (merged there per house pattern; a standalone SkillKitTests.cs collided with the existing class), KitApiTests gained Get_KitCarriesManualOriginDefaults + extended KitJson. Build 0 errors; dotnet test 412/412 green (Core 93, Infrastructure 125, Api 135, Mcp 59 — MCP untouched). Migration verified on legacy-schema DB with a pre-existing kit (reads back manual/null/null) and on fresh DBs via Api.Tests Host boots.
</log>
