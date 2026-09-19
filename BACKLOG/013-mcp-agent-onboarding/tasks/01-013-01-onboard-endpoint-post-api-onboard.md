---
feature: 013
task: 01
slug: mcp-agent-onboarding
status: done
review: light
created: 2026-09-19
---

# 013/01 — Onboard endpoint (POST /api/onboard)

<goal>
The host exposes an additive endpoint `POST /api/onboard` that resolves the caller's project from a reported cwd (or explicit projectId), auto-creates the project on a miss, and returns a rich onboarding payload: project id/name, created flag, board summary, and the ceremony quickstart text.
</goal>

<context>
Feature 013 (mcp-agent-onboarding): agents seeing the overmind MCP server must be able to onboard — discover/remember their project ID and get the ceremony quickstart — in one round-trip. This task is the host-side brain every other door (MCP tool, installer) talks to. Dogfood driver: Claude in Squeezr had overmind connected but never wrote to it because nobody told it its projectId.
</context>

<scope>
- New endpoint file `src/Overmind.Host/Api/OnboardApi.cs` (follow the AgentPlanningApi static-class pattern), mapped in `src/Overmind.Host/Program.cs` next to the other API maps: `POST /api/onboard`.
- Request contract `{ cwd: string, projectId?: guid }`; response `{ projectId, projectName, created, board: { countsByStatus, currentSlug }, quickstart }` — DTOs in `src/Overmind.Host/Api/Contracts/`.
- cwd normalization: Path.GetFullPath, trailing-slash trim, case-sensitive ordinal compare (linux host), best-effort symlink resolution. Match against `Project.DiskPath`.
- Explicit `projectId` in the request wins over cwd matching; unknown guid → 404 via HttpErrors.
- Miss → auto-create `Project` (name = directory name of cwd); on name collision with a different path, suffix `-2`, `-3`, … until unique (Project.Name has a unique index).
- Board summary: feature counts grouped by status + the currently pinned feature slug (reuse whatever PlanningPolicy/services already know about current-pinning).
- Quickstart text: single static constant on the host (e.g. `OnboardQuickstart.Text`) — the ONLY source; MCP tool and installer must never duplicate it. Content: which MCP tools in which order (onboard → list_features → current_task → upsert/set_status loop), plus the OVERMIND_PROJECT_ID escape hatch.
- Tests in `tests/Overmind.Api.Tests/` covering: match by DiskPath, trailing-slash normalization, auto-create with created:true, name-collision suffixing, board summary contents, quickstart present.
</scope>

<anti-scope>
- No MCP adapter changes (task 02), no installer changes (task 03).
- No auth on the endpoint — loopback is the security boundary, unchanged.
- No renames/removals of existing routes or DTO fields (additive-only contract).
- No UI work.
</anti-scope>

<constraints>
- .NET 10 / C# 14, minimal APIs, EF Core via OvermindDbContext; no new NuGet packages.
- No comments in code unless a decision is genuinely non-obvious; English-only identifiers/messages.
- Verify with `dotnet build Overmind.sln` + `dotnet test Overmind.sln --no-build` (baseline 735 green).
</constraints>

<acceptance-criteria>
- [ ] POST /api/onboard with a cwd matching an existing Project.DiskPath returns that project with created:false.
- [ ] Trailing-slash / non-normalized cwd still matches.
- [ ] Unknown cwd → project auto-created (name = dir name, created:true); second call returns created:false for the same cwd.
- [ ] Name collision with a different path → unique `-N` suffix, no unique-index violation.
- [ ] Explicit projectId overrides cwd; unknown projectId → 404.
- [ ] Response board summary matches PlanningFeatures table contents; currentSlug reflects the pinned feature (null when none).
- [ ] dotnet test green incl. new tests.
</acceptance-criteria>

<refs>
- Spec decisions: feature 013-mcp-agent-onboarding, spec artifact (validated 2026-09-19) — decisions/requirements sections.
- Pattern refs: `src/Overmind.Host/Api/AgentPlanningApi.cs`, `src/Overmind.Host/Api/ProjectsApi.cs`, `src/Overmind.Host/Api/HttpErrors.cs`, `src/Overmind.Infrastructure/Planning/PlanningPolicy.cs` (current-pin + validation-gate mechanics).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-19 — Execution started — handed to dotnet-coder subagent.
- 2026-09-19 — Coder done: endpoint + contracts + quickstart constant + 13 tests, suite 748/748 green. To review.
- 2026-09-19 — Review PASS WITH NOTES (audits/review-013-01.md); both notes fixed in-tree (OnboardRequestValidator + real planning_* tool names in quickstart), suite green. Done.
</log>