---
feature: 002
task: 07
slug: kit-library
status: done
review: light
created: 2026-09-14
---

# 002/07 — Kit import API slice

<goal>
Four endpoints expose the import pipeline: `POST /api/kits/import/scan` (preview), `POST /api/kits/import` (commit selections), `POST /api/kits/{id}/reimport`, `POST /api/kits/{id}/clear-source`. All failures surface as RFC 7807 ProblemDetails via one mapper; handlers stay thin vertical slices over `KitScanner`/`KitImporter`.
</goal>

<context>
House API conventions (mirror existing slices in `src/Overmind.Host/Api/`): one static class per bounded context with a `Map<ClassName>` extension, JSON camelCase with enums as camelCase strings, FluentValidation for request DTOs, `HttpErrors`/`PlanningErrors`-style helpers for failure translation (add `KitImportErrors.Map` for `KitImportException` kinds → 400/404/409), registration in `Program.cs` next to the other `Map*Api` calls. Handlers resolve inputs and call the services from tasks 03/04/06 — no business logic in handlers. Executor reads ONLY this file.
</context>

<scope>
- `src/Overmind.Host/Api/KitImportApi.cs` — four endpoints above
- Contracts in `src/Overmind.Host/Api/Contracts/`: scan request `{ path }` → preview DTO (candidates with suggested names, skills/platforms, gates/guards, issues, name-collision flag, re-import match kit id, truncated flag); import request `{ path, kits: [{ relativePath, name }] }` → per-kit report; reimport → update report (added/updated/kept-local/conflicted/removed counts + per-entry outcomes); clear-source → updated kit response
- `src/Overmind.Host/Api/Validation/` — validators (non-empty path, non-empty selections, non-empty kit names)
- `KitImportErrors` mapper: duplicate name → 409, re-import-required → 409, no-source / source-missing → 409, kit-not-found → 404, validation → 400
- **Review carry-overs from 002/04 (`audits/review-002-04.md`):**
  - IMPORTANT-1 — delete guard for the synthetic "Kit Library" project: `ProjectsApi.Delete` currently cascades kit-owned entries away if the user deletes it. Add a guard: deleting the library project while it owns kit-linked entries → 409 with explanatory detail (or hide it from delete flows — pick one, record in `<log>`)
  - NIT-1 — library-project lookup is case-sensitive; make it consistent with the scanner's OrdinalIgnoreCase semantics
  - NIT-2 — no unique index on `SkillKits.Name` (race window on commit); add the index + map the violation to the duplicate-name kind (check how planning handles unique-race retries first)
  - NIT-4 — validate `selection.Name` in the import request validator (non-empty, trim, length cap)
  - From 002/06 review (`audits/review-002-06.md`): NIT-1 — reimport report must distinguish "removed + row deleted" from "removed link retained (cross-kit guard)" in the API DTO; NIT-6 — read the artifact's residual transaction notes before finalizing the reimport endpoint contract
- DI registration for `KitScanner`/`KitImporter` (Infrastructure `DependencyInjection.cs`)
- `Program.cs` — `app.MapKitImportApi()`
- Tests: `tests/Overmind.Api.Tests/KitImportApiTests.cs` — scan happy path + flags, import commit + partial failure isolation, reimport report shape, clear-source, 400/404/409 mappings
</scope>

<anti-scope>
- No service-layer logic changes (tasks 03/04/06 own that); if a service gap appears, note it in `<obstacles>` instead of patching around
- No web UI (task 08)
- No MCP adapter changes — import is Host/UI-only
- No auth/CORS changes; loopback boundary stays as-is
</anti-scope>

<constraints>
- No new NuGet packages
- `dotnet build Overmind.sln` + `dotnet test tests/Overmind.Api.Tests` green
- Contracts additive-only toward the MCP adapter (new endpoints, no changes to existing DTOs the adapter reads)
- No comments in code; no auto-commits
</constraints>

<acceptance-criteria>
- [x] All four endpoints respond per contract against a real fixture folder (Api.Tests)
- [x] Failure kinds map to the right status codes with explanatory `detail`
- [x] Import with one failing selection returns per-kit reports (success + failure) in one response
- [x] Full suite green (`dotnet test Overmind.sln`)
</acceptance-criteria>

<refs>
- @../spec.md — D9, D10, R4–R10 (reviewer reads, not executor)
- @../plan.md — task 07 row
- tasks/03-kit-scanner.md, tasks/04-kit-importer.md, tasks/06-kit-reimport.md — services behind the endpoints
- tasks/08-web-import-ui.md — consumes these contracts
</refs>

<obstacles>
- `KitImporter.ClearSourceAsync` returns the kit without `Include(SkillKitEntries)`, so the raw entity's nav collection is empty and a naive `KitMapper.ToResponse` would report zero entries. Not patched in the service (anti-scope): the `ClearSource` handler reloads the kit with `Include` from its own `OvermindDbContext` (tracked instance — same row, loaded nav) before mapping the response.
</obstacles>

<log>
- 2026-09-15 — done. Four endpoints in `src/Overmind.Host/Api/KitImportApi.cs` (`POST /api/kits/import/scan`, `POST /api/kits/import`, `POST /api/kits/{id}/reimport`, `POST /api/kits/{id}/clear-source`), thin handlers over `KitScanner`/`KitImporter`; contracts `Api/Contracts/KitImportContracts.cs`, validators `Api/Validation/KitImportValidators.cs`, mapper `Api/Mapping/KitImportMapper.cs`, error translator `Api/KitImportErrors.Map` (KitNotFound→404; DuplicateName/ReImportRequired/NoSource/SourceMissing→409 with `Kind.ToString()` title like `PlanningErrors`; PathMissing/NotADirectory/CandidateNotFound→400; default 500); DI in Host `AddOvermind` (no Infrastructure DependencyInjection.cs exists — house convention registers everything in Host); `Program.cs` maps `MapKitImportApi`. Carry-overs: **IMPORTANT-1** chose the 409 guard (not hiding): `ProjectsApi.Delete` refuses while the project owns entries linked into any kit — covers the library project and the pre-existing user-project hole; unlinking unblocks delete. **NIT-1** library lookup case-insensitive via `ToLower()` SQL translation in a shared `FindLibraryProjectIdAsync` (initial lookup + raced re-query; constant "Kit Library" is ASCII so SQLite `lower()` folds it exactly). **NIT-2** unique index on `SkillKits.Name` (BINARY collation — same posture as `Project.Name`; in-memory OrdinalIgnoreCase check stays the primary guard, index closes the exact-name commit race; migration `20260915120341_UniqueKitName`) + `CommitKitAsync` maps `DbUpdateException` UNIQUE-on-`SkillKits.Name` to a per-kit `DuplicateName` failure — consulted planning's race handling (`PlanningServiceGuard.IsUniqueViolation`, `EnsureLibraryProjectAsync` detach-requery): no retry, a lost name race is reported because the name is user-chosen; the shared `DetachAdded` sweep now also runs on that path so a later selection in the same request isn't poisoned. **NIT-4** `KitImportRequestValidator` enforces selection names non-empty-after-trim + max 200 (mirrors `CreateKitRequestValidator`); through the API the name is always explicit — the importer's empty-name→candidate fallback remains for direct service use only. **002/06 NIT-1** `KitReimportEntryResult.EntryRowRetained` set from the cross-kit `shared` check (report entry moved after the check), projected into the API DTO. **002/06 NIT-6** (residual notes in tasks/06 `<log>`/`<obstacles>`): raw `IOException`/`UnauthorizedAccessException` from TOCTOU re-reads mapped at the reimport endpoint → 500 `Kit import failed` ProblemDetails; the scoped request DbContext dies with the request so no poisoned tracker carries over. **NIT-5** conscious accept: library project still created before the selection loop (stable idempotent owner for retries). Contract note: candidate `name` doubles as the suggested kit name; the import request's `relativePath` echoes the candidate name (service matches `CandidateName` Ordinal). Verification: `dotnet build Overmind.sln` 0 errors/warnings; `dotnet test tests/Overmind.Api.Tests` 152/152 (134 + 18 new in `KitImportApiTests`); `dotnet test Overmind.sln` 479/479 (Core 94 / Infra 174 / Api 152 / Mcp 59); scoped `dotnet format whitespace --verify-no-changes` clean on all 12 touched files.
- 2026-09-15 — contract fix from `audits/review-002-08.md` "Special assignment": import selections now match scan candidates by RELATIVE PATH (spec D10), not by candidate name. `KitScanCandidate` carries `RelativePath` (root candidate `""`, top-level folder = folder name), set in `KitScanner.BuildCandidateAsync`, projected into the scan preview DTO as `candidates[].relativePath`; `KitImportSelection.CandidateName` renamed to `RelativePath` and `KitImporter.CommitKitAsync` matches `c.RelativePath` Ordinal (unknown path → per-kit `candidateNotFound`); the empty-name fallback now uses the resolved candidate's `Name`. `KitImportRequestValidator` allows an empty `relativePath` (root candidate) but rejects a missing one (`NotNull`). Kills the ambiguity edge where the root basename equals a top-level folder name (previously the root candidate won `FirstOrDefault` silently). BREAKING for the web UI: the wizard's workaround `relativePath: candidate.name` now fails `candidateNotFound` for the root candidate — the frontend must echo `candidates[].relativePath` instead (task 08 follow-up, web code untouched here). New API tests: root+folder scan relativePaths, root import via `""`, candidate-name-as-path → `candidateNotFound`, ambiguous `dupe`/`dupe` names resolving to distinct candidates by path; scanner tests pin `RelativePath` for root and folder candidates. Verification: `dotnet build Overmind.sln` 0 errors/0 warnings; `dotnet test Overmind.sln` 486/486 (Core 94 / Infra 175 / Api 158 / Mcp 59); `dotnet format whitespace --verify-no-changes` clean on all touched files.
- 2026-09-15 — review fixes for `audits/review-002-07.md` IMPORTANT-1/IMPORTANT-2. **IMPORTANT-1**: migration `20260915120341_UniqueKitName` dedupes before `CreateIndex` — one raw-SQL statement: a materialized CTE ranks each duplicate group by `rowid` (BINARY collation, matching the index), keeps the lowest `rowid` intact, and renames later rows to `Name || ' (' || n || ')'` (n = 2, 3, ... by `rowid` order); `AS MATERIALIZED` (SQLite >= 3.35; EF 10 bundle is newer) forces one-shot evaluation so the UPDATE cannot observe its own renames mid-statement. Tested against the real migration runner, not just the SQL text: `UniqueKitNameMigrationTests` migrates a file DB to `20260914151947_SkillFileAgnosticPlatform`, seeds `alpha`x3 / `beta`x2 / `gamma`, then migrates to latest — asserts final names `alpha`/`alpha (2)`/`alpha (3)`/`beta`/`beta (2)`/`gamma`, both first-inserted keepers keep their names, and a post-migration duplicate insert still throws. Residual (accepted): a pre-existing kit literally named like a computed rename (e.g. `foo (2)` alongside duplicate `foo`s) can still collide and fail the index creation loudly — no silent data loss, pathological for this tool. **IMPORTANT-2**: `KitsApi.Create`/`Update` map `DbUpdateException` UNIQUE-on-`SkillKits.Name` to 409 ProblemDetails (title `DuplicateName`, detail "Kit name '...' is already taken." — same shape the importer reports) via a new shared `UniqueViolation.IsOn` helper in `Overmind.Infrastructure/Persistence`; `KitImporter` now consumes the same helper instead of its private copy. Api tests pin both endpoints: 409 + title + detail on duplicate create, 409 + kit unchanged on rename-to-taken. Also silenced two CS8604 warnings in `KitImportApiTests` (`KitId!.Value`, `gates!.Single`) so the build gate stays at 0 warnings. Verification: `dotnet build Overmind.sln` 0 errors/0 warnings; `dotnet test Overmind.sln` 482/482 (Core 94 / Infra 175 / Api 154 / Mcp 59); scoped `dotnet format whitespace --verify-no-changes` clean on all 8 touched files.
</log>
