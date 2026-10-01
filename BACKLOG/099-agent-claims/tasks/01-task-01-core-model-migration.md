---
feature: 099
task: 01
slug: agent-claims
status: done
review: light
created: 2026-09-30
brief: AgentSession entity + claim fields on PlanningFeature (IsCurrent out) + Project settings + claim service matrix + EF migration + full unit coverage
---

# Task 01 — Core model + migration

<goal>
Claim data model, AgentSession entity, per-project settings, and claim/unclaim/take-over/adopt service logic with session id as a plain parameter — no HTTP, no MCP, no web. Everything testable in isolation.
</goal>

<scope>
- `src/Overmind.Core/Entities/AgentSession.cs` (new): Id, Alias, Vendor, StartedAt, LastSeenAt, EndedAt (DateTime?). Factory + End/Touch domain methods.
- `src/Overmind.Core/Entities/PlanningFeature.cs`: remove IsCurrent + SetCurrent; add ClaimedByAlias, ClaimedByVendor, ClaimedAt, ClaimSessionId (nullable); domain methods Claim(alias, vendor, sessionId, now), Release(), and whatever take-over needs. Alias/vendor capped at 32 chars.
- `src/Overmind.Core/Entities/Project.cs`: ClaimStaleThresholdMinutes (int, default 15, 0 = stale off) + AgentNamingRules (string?, null = generic hint) + a setter following the SetPlanningWorkflow shape.
- `src/Overmind.Infrastructure/Persistence/OvermindDbContext.cs`: DbSet + configurations for AgentSession, claim columns, new Project fields.
- `src/Overmind.Infrastructure/Planning/` (PlanningFeatureService or a new claim service beside it): claim/unclaim logic with explicit sessionId parameter — the spec's full matrix: done→409; live foreign session→409 naming holder; live same alias+vendor different session→409; stale/ended foreign→take-over logged; ended same alias+vendor→auto-adopt logged; holder done-close→auto-release; derived state alive|stale|ended (threshold from project, 0 disables).
- EF migration: drop IsCurrent, AgentSessions table, claim columns, two Project fields.
- Tests in Overmind.Core.Tests / Overmind.Infrastructure.Tests (xUnit + Shouldly + NSubstitute) covering the full rules matrix.
</scope>

<anti-scope>
No HTTP endpoints, no MCP tools, no session header plumbing, no web changes, no docs.
</anti-scope>

<constraints>
English-only, no comments by default, no auto-commits, match existing entity/service idioms in the repo. Migration down() must cleanly reverse the new objects.
</constraints>

<acceptance-criteria>
- [x] `dotnet build` 0 errors 0 warnings on the solution
- [x] New tests cover: claim free feature; claim done→409; claim live foreign→409 naming holder; live same alias+vendor→409; take-over stale foreign→ok+logged; adopt ended same alias+vendor→ok+logged; threshold 0 disables stale; holder closes done→claim released; unclaim non-holder→409
- [x] Full `dotnet test` green
- [x] Migration exists, drops IsCurrent, adds AgentSessions + claim columns + 2 Project fields
</acceptance-criteria>

<refs>
- Spec: artifact 0e774399-c7c4-488b-a647-b6996621ca29 (feature 099-agent-claims) — Decisions + Data model + Session lifecycle sections are the contract.
</refs>

<obstacles>
- Compile-consequence removals pulled forward from tasks 02/03 (forced by AC1 "0 errors on the solution" once IsCurrent died): POST /current endpoints + SetCurrent handlers in PlanningFeaturesApi/AgentPlanningApi, planning_set_current MCP tool, OvermindApiClient.SetPlanningFeatureCurrent, SetPlanningFeatureCurrentRequest, IsCurrent in board/queue payloads + mapper, onboard CurrentSlug, the dependency-tool descriptions mentioning set_current, and the pin tests across Api/Mcp/Infra/Core suites. GET /current stays (it is planning_current_task's slug-based backend, spec keeps it). Onboard temporarily loses currentSlug until task 03 joins claim state into payloads.
- EF Core SQLite translates model DropColumn into a temp-table rebuild that runs at the END of the migration (after any raw Sql operation — EF warns to "move these operations to a subsequent migration") and silently drops the table's triggers + reassigns rowids. This hit BOTH directions of AddAgentClaims: Up() (caught by the suite, 10 FTS failures) and Down() (review blocker, verified 9→6 triggers after downgrade). Fix in both directions inside the single migration: every PlanningFeatures column drop is native raw SQL ALTER TABLE ... DROP COLUMN (no rebuild, triggers and rowids survive); the two Projects drops stay model-generated (Projects carries no triggers). Pinned by PlanningFtsMigrationTests.Migrate_AgentClaimsDownUp_FtsTriggersSurvive; scratch-DB probe up → down one step → up holds 9 triggers with identical names.
- Spec gaps resolved at service level (409 wording follows the spec's "claim held by Dan by qwen (alive)"): holder re-claim of its own live claim is an idempotent no-op; stale claim re-claim by the same session silently refreshes ClaimedAt (no log spam); claim get-or-creates the AgentSession row (task 01 has no register endpoint yet — task 02 adds it) and touches LastSeenAt on claim/adopt/take-over; a claim whose holder session row is missing is treated as ended (safe for legacy data); adopt identity match is Ordinal, not case-insensitive (strict reading of "alias+vendor equals").
- Claim events land in the feature body log via a new PlanningFeature.AppendLog (no version bump — claim noise would otherwise churn artifact versions in BACKLOG mirrors).
- Done-close auto-release wired into PlanningFeatureService (SetStatusAsync + ui-door UpdateAsync): release must fire on the existing status-move paths; the human door close releases too (invariant: done implies unclaimed).
- Review nits routed for closeout (accepted, not fixed here): (1) NormalizeIdentity logic is duplicated between PlanningFeature and AgentSession — extract a shared agent-identity normalizer; (2) same-session stale re-claim silently rewrites the feature's claim identity from the call args without touching the AgentSession row — alias/vendor drift between feature claim fields and the session row is possible. Route to microbatch or fix in the closeout task.
- Repo trivia for future script tooling: several pre-existing raw-SQL migrations (e.g. 20260915120341_UniqueKitName) omit the trailing semicolon inside migrationBuilder.Sql blocks — harmless for EF runtime (per-operation execution) but breaks whole-script consumers (dotnet ef migrations script output + external runners). Route to microbatch only if script-based tooling lands.
- dotnet format --verify-no-changes flags only the pre-existing Migrations block-namespace style (identical across all sibling migrations); zero format findings outside Migrations. One latent CS8604 in PlanningApiTests (pre-existing on main, surfaces only on --no-incremental) fixed with a null-forgiving operator to keep AC1 true.
- Full-suite runs flaked twice under parallel load (1 Core + 1 Mcp timing-sensitive failure); both suites pass standalone and the control full runs were green (1719 then 1720 passed / 0 failed after the downgrade fix + new pinning test).
</obstacles>

<log>
- 2026-09-30 [agent] — dispatched to dotnet-coder (sonnet), session of orchestrator 099 wave
- 2026-09-30 [agent] — obstacles recorded: compile-forced pin removals from tasks 02/03, native DROP COLUMN to spare FTS triggers, resolved spec gaps (idempotent re-claim, silent stale refresh, session get-or-create, missing-row treated as ended), AppendLog without version bump, done-close release wiring
- 2026-09-30 [agent] — Executed by dotnet-coder. Core: AgentSession entity + AgentSessionLiveness, PlanningFeature claim fields + Claim/Release/AppendLog (IsCurrent/SetCurrent out), Project.ClaimStaleThresholdMinutes/AgentNamingRules + SetClaimSettings, FeatureClaimRefused/FeatureClaimHeld codes. Infrastructure: FeatureClaimService (claim/unclaim/EnsureHolder, full matrix), done-close auto-release in PlanningFeatureService, DbContext config. Migration AddAgentClaims via dotnet-ef (native raw DROP COLUMN — see obstacles). Pin surface removed across Host/Mcp as a compile consequence (tasks 02/03 keep the wiring). Tests: AgentSessionTests (12), FeatureClaimServiceTests (21), claim tests in PlanningFeatureTests (7), ProjectTests claim settings (4). Gates: dotnet build --no-incremental 0 errors 0 warnings; dotnet test green — Core 251 / Infra 551 / Api 511 / Mcp 406 = 1719 passed, 0 failed.
- 2026-09-30 [agent] — task 01 executed to review: build 0/0, full suite 1719 passed / 0 failed, migration AddAgentClaims in place, all 4 ACs ticked; ready for dotnet-code-reviewer (light review per plan)
- 2026-10-01 [agent] — 2026-10-01 [agent] — reviewed by dotnet-code-reviewer: FAIL — 1 BLOCKER (AddAgentClaims.Down() drops feature FTS triggers via EF rebuild; verified 9→6 triggers), 4 nits; matrix/entities/tests clean, build 0/0, 1719/0 re-verified; report audits/review-099-01.md. Fix loop dispatched back to the coder.
- 2026-10-01 [agent] — review blocker fixed: Down() now drops the four PlanningFeatures claim columns via native raw ALTER TABLE DROP COLUMN (no EF rebuild — FTS triggers survive downgrade), mirroring the Up() fix; new pinning test PlanningFtsMigrationTests.Migrate_AgentClaimsDownUp_FtsTriggersSurvive; scratch-DB probe up → down → up holds 9 triggers; review nits (NormalizeIdentity duplication, same-session stale re-claim identity drift) recorded in obstacles for closeout routing
- 2026-10-01 [agent] — 2026-10-01 [agent] — fix round 1 re-reviewed: PASS-WITH-NITS. Blocker closed exactly as prescribed (raw ALTER TABLE DROP COLUMN in Down(), pinning test fails red on the old code: 3≠6 triggers on first assert). Delta clean, filtered run 3/3, build 0/0. Report updated: audits/review-099-01.md. Remaining nits routed to tasks 03/04 + closeout.
- 2026-10-01 [agent] — review PASS-WITH-NITS after fix round 1 (audits/review-099-01.md): blocker closed + pinned by test, AC 4/4, 1720/1720 suite. Closing as done.
</log>