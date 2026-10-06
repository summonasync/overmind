---
feature: 121
slug: alias-rules-editor
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Plan 121 — Alias rules: settings editor + stable personal alias in ceremony skill

<approach>
Four slices, one dependency: the Host claim-settings endpoint is the only writer and unblocks the web section; the MCP rendering and the ceremony-skill line are independent of both. The served block already exists (099) — no migration, no entity shape change, additive endpoint only.

Order: 01 (Host) ∥ 02 (MCP adapter) ∥ 04 (ceremony skill) can run in parallel; 03 (web) after 01. Slice 04 edits a file outside the repo — no build gates, read-back pin only.
</approach>

<contract>
- `GET /api/projects/{projectId:guid}/claim-settings` → `200 { claimStaleThresholdMinutes: int, agentNamingRules: string|null }`; unknown project → 404.
- `PUT /api/projects/{projectId:guid}/claim-settings` body `{ claimStaleThresholdMinutes: int, agentNamingRules: string|null }` → `200` echo after `SetClaimSettings`; validation: threshold >= 0, rules length <= 2000 → else 400 ValidationProblem; whitespace rules → stored as null.
- `overmind_onboard` output gains an identity line (defaults alias/vendor + naming rules verbatim); tool description names the agentIdentity block.
- Web: `ClaimSettingsResponse { claimStaleThresholdMinutes: number; agentNamingRules: string | null }`; read hook + mutation (invalidate on save); section mounted on the project detail after the planning workflow settings.
</contract>

## Task slices (become task artifacts after plan validation)

**01 — Host: claim-settings endpoint + validation + tests.**
- `src/Overmind.Host/Api/Contracts/ClaimSettingsContracts.cs` (new): `UpdateClaimSettingsRequest(int ClaimStaleThresholdMinutes, string? AgentNamingRules)`, `ClaimSettingsResponse(int ClaimStaleThresholdMinutes, string? AgentNamingRules)`.
- `src/Overmind.Host/Api/ClaimSettingsApi.cs` (new): `MapGroup("/api")` + `MapGet/MapPut("/projects/{projectId:guid}/claim-settings", …)` following PlanningWorkflowApi shape — resolve project or `HttpErrors.NotFound`; PUT calls `project.SetClaimSettings(request.ClaimStaleThresholdMinutes, request.AgentNamingRules)`, `SaveChangesAsync`, echo response.
- `src/Overmind.Core/Entities/Project.cs`: `public const int MaxNamingRulesLength = 2000;` + guard in `SetClaimSettings` (`ArgumentOutOfRangeException.ThrowIfGreaterThan`, mirrors SetManifest :71-79; existing callers pass short/null — safe).
- `src/Overmind.Host/Api/Validation/ProjectValidators.cs`: `UpdateClaimSettingsRequestValidator` — `InclusiveBetween(0, int.MaxValue)` with "0 disables stale detection" message; rules `MaximumLength(2000)`.
- `src/Overmind.Host/Program.cs`: `app.MapClaimSettingsApi();` next to `MapPlanningWorkflowApi()` (:165).
- Tests `tests/Overmind.Api.Tests/ClaimSettingsApiTests.cs` (new): PUT→GET echo; whitespace-only rules → GET null; threshold 0 stored; -1 → 400; 2001 chars → 400; unknown project → 404. Extend `AgentSessionLockApiTests.Onboard_NamingRules_ProjectValueAndGenericHint` with an API-driven leg: PUT rules → onboard agentIdentity carries them (SQL-seeded leg stays as the storage pin).
- `tests/Overmind.Core.Tests/Entities/EntityFactoryTests.cs`: >2000 chars throws (guard pin).
- Gates: `dotnet build`; targeted `dotnet test tests/Overmind.Api.Tests tests/Overmind.Core.Tests`.

**02 — MCP adapter: render agentIdentity in onboard output.**
- `src/Overmind.Mcp/OvermindOnboardingTools.cs`: new `AppendIdentity(payload, output)` called between `AppendValidation` and `AppendSemantic` (Format :81-84); renders one `Agent identity:` line (default alias/vendor + `planning_claim_feature` pointer) followed by the naming rules verbatim (may be multi-line); tolerant of a missing block (older hosts).
- Description (:22): extend the carried-blocks list with "an agentIdentity block (default alias/vendor and the project's naming rules for choosing your own stable alias)".
- Tests `tests/Overmind.Mcp.Tests/OnboardingToolContractTests.cs`: identity line rendered when the block is present (custom rules string asserted); no crash when absent; description key phrases per the existing pin style.
- Gates: `dotnet build`; `dotnet test tests/Overmind.Mcp.Tests`.

**03 — Web: claim settings section (blockedBy 01).**
- `web/src/features/projects/types.ts`: `ClaimSettingsResponse`.
- `web/src/features/projects/api.ts`: `getClaimSettings(projectId)`, `updateClaimSettings(projectId, body)`.
- `web/src/features/projects/queries.ts`: `projectKeys.claimSettings(projectId)` + `claimSettingsQueryOptions` (mirror planningKeys.workflow :18/:45).
- `web/src/features/projects/lib/use-claim-settings.ts` (new): read hook + `useClaimSettingsMutation` (invalidate on success) — mirror the workflow hooks in use-planning.ts.
- `web/src/features/projects/components/claim-settings-section.tsx` (new): textarea (maxLength 2000), synced-state pattern from workflow-settings.tsx :16-40, save Button, ErrorText; hint that agents read this text in the onboard agentIdentity block.
- `web/src/pages/projects-page/ui/projects-page.tsx`: mount `<ClaimSettingsSection projectId={detail.id} />` after `PlanningWorkflowSettings` (:106).
- `web/tests/claim-settings-section.test.mts` (new, mirror feature-number-settings.test.mts): renders the saved value; save posts the entered text.
- Gates (in web/): `npm run lint && npx tsc --noEmit`; `npm test`; `npm run build`.

**04 — Ceremony skill: stable-alias instruction (file outside the repo).**
- `/home/johan-boolman/.claude/skills/overmind-ceremony/SKILL.md`: extend the claim bullet block (:81-86) — pick a STABLE personal alias per the project's naming rules from the agentIdentity block of `overmind_onboard`; reuse the same alias across sessions so board history stays attributable; vendor = the runtime; two agents on the same runtime must not share an alias.
- Gates: read-back grep (stable alias + naming rules mentioned); no build/test impact; log the edit in the task artifact (repo commits will not contain it).

<verification>
Closeout after all slices: `dotnet build` (solution root) → serial full `dotnet test` (099 lesson: timing flakes under parallel load — isolated rerun plus one control full run) → web `npm run lint && npx tsc --noEmit` → `npm test` → `npm run build`. Round-trip dogfood: PUT rules via the API → `/api/onboard` agentIdentity shows them; clear rules → generic hint returns.
</verification>

<refs>
- Spec 121: dc030eba-1f10-4bf2-a68f-f9fea8534b97 (this feature).
- 099-agent-claims (infra origin, endpoint/test precedents); 119-adapter-version-awareness (onboard description pins).
- Composition: src/Overmind.Host/Program.cs:165/:174; endpoint pattern src/Overmind.Host/Api/PlanningWorkflowApi.cs; validators src/Overmind.Host/Api/Validation/ProjectValidators.cs; entity src/Overmind.Core/Entities/Project.cs:35/:64-69/:71-79; adapter src/Overmind.Mcp/OvermindOnboardingTools.cs:22/:49-89; web pattern web/src/features/planning/components/workflow-settings.tsx, web/src/features/planning/queries.ts:18/:45, mount point web/src/pages/projects-page/ui/projects-page.tsx:102-110; test precedents tests/Overmind.Api.Tests/AgentSessionLockApiTests.cs (Onboard_NamingRules_ProjectValueAndGenericHint), tests/Overmind.Mcp.Tests/OnboardingToolContractTests.cs, web/tests/feature-number-settings.test.mts.
</refs>

<obstacles>
- Slice 04's target file is outside the repo — checkpoint commits will not include it; the task log carries the proof instead.
- Web test runner is `node --test "tests/**/*.test.mts"` (npm test) — plain node:test, no vitest; match the .test.mts style.
- Onboard tool description is double-pinned (099 key phrases + 119 adapter text) — edit incrementally and run OnboardingToolContractTests first.
</obstacles>

<log>
- 2026-10-06 [agent] — plan drafted alongside the spec (batch mode); slices 01 ∥ 02 ∥ 04, 03 after 01; no task artifacts created yet — they follow after the human taps both documents in the consolidated sitting.
- 2026-10-06 [agent] — validated with human proof: notification 252273fe-4716-4a05-aa7b-3e8996384ab2 replied 2026-10-06 13:05:14 UTC
</log>