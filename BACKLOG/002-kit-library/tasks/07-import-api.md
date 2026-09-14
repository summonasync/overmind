---
feature: 002
task: 07
slug: import-api
status: todo
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
- [ ] All four endpoints respond per contract against a real fixture folder (Api.Tests)
- [ ] Failure kinds map to the right status codes with explanatory `detail`
- [ ] Import with one failing selection returns per-kit reports (success + failure) in one response
- [ ] Full suite green (`dotnet test Overmind.sln`)
</acceptance-criteria>

<refs>
- @../spec.md — D9, D10, R4–R10 (reviewer reads, not executor)
- @../plan.md — task 07 row
- tasks/03-kit-scanner.md, tasks/04-kit-importer.md, tasks/06-kit-reimport.md — services behind the endpoints
- tasks/08-web-import-ui.md — consumes these contracts
</refs>

<obstacles>
</obstacles>

<log>
</log>
