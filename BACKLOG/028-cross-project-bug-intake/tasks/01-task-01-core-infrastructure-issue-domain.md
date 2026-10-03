---
feature: 028
task: 01
slug: cross-project-bug-intake
status: done
review: light
created: 2026-10-02
brief: Issue/IssueAppend entities + IssueService (status machine, machine promotion composing task artifacts) + migration AddIssues + unit matrix
---

# Task 01 — Core + Infrastructure: Issue domain

<goal>
Issue + IssueAppend entities, IssueService with the full status machine and machine promotion, migration AddIssues — everything testable in isolation.
</goal>

<scope>
- src/Overmind.Core/Entities/Issue.cs: Id, ProjectId, Title, Body, Status, AuthorProjectId, CreatedAt, ClosedAt (nullable), PromotedToArtifactId (nullable); domain methods (Create, Append, transitions) with invariants inside.
- src/Overmind.Core/Entities/IssueAppend.cs: Id, IssueId, AuthorProjectId, Text, CreatedAt; append-only (no update/remove paths).
- src/Overmind.Core/Enums/IssueStatus.cs: Open, Triaged, Promoted, Closed.
- src/Overmind.Infrastructure/Planning/IssueService.cs: Create (authorProjectId validated against existing projects — 404 otherwise), Append (chronological, any project), SetStatus (forward chain open→triaged→promoted→closed; closed reachable from any non-closed; no reopen; source marker per 043), Promote (only from open/triaged): composes the task artifact through the existing planning artifact service — goal from title+body, scope from body template sections, obstacles from the append trail, refs with the issue id — sets PromotedToArtifactId + status in one SaveChanges.
- OvermindDbContext: DbSets + configurations.
- EF migration AddIssues (Issues + IssueAppends; follow the wave FTS-safe convention — raw DROP COLUMN in Down if any drop appears).
- Tests (xUnit + Shouldly): the full matrix — forward chain, close from any non-closed, no reopen, promote only from open/triaged, append-only invariant, promotion composes the task artifact and links both ways, author validation 404.
</scope>

<anti-scope>
No HTTP endpoints, no MCP tools, no web, no docs.
</anti-scope>

<constraints>
English-only, no comments by default, match entity/service idioms, no auto-commits, dotnet build after changes, full dotnet test with real numbers at the end.
</constraints>

<acceptance-criteria>
- [x] dotnet build 0 errors 0 warnings
- [x] Status matrix tests: forward chain, close-from-any, no-reopen, promote-only-from-open/triaged, source marker recorded
- [x] Append-only invariant test; append from any project lands with provenance
- [x] Promotion test: task artifact composed (goal/scope/obstacles/refs per the mapping), issue promoted with artifact link, same SaveChanges atomicity
- [x] Author validation: unknown authorProjectId → 404
- [x] Full dotnet test green; migration AddIssues exists and is FTS-safe
</acceptance-criteria>

<refs>
- Spec: artifact 1fd1454c-d026-4904-8e1b-70f92abdad51 (feature 028-cross-project-bug-intake) — Data model + Decisions sections are the contract.
</refs>

<obstacles>
- SetStatus semantics decision: direct SetStatus to `promoted` is refused (promoted is reached only through the promote path — spec: «promoted only from open/triaged via issues_promote»); same-status moves are a silent no-op (idempotent re-calls); closed refuses every target (no reopen). Error surface: new PlanningException.IssueInvalidTransition + MissingIssue factories, Kind InvalidStatus → 400, new PlanningFailureCode entries IssueInvalidTransition/MissingIssue.
- Atomicity of Promote: PlanningArtifactService.UpsertAsync saves internally, so the ONE-SaveChanges requirement is met the only way that preserves the existing artifact-creation path — an explicit transaction wrapping UpsertAsync + issue.Promote + a final SaveChangesAsync; PlanningServiceGuard.SyncMaterializationAsync runs after commit (repo invariant). No parallel artifact-creation path was built.
- Promotion resolves the feature by (issue.ProjectId, featureSlug) — an issue can only promote into a feature on its own board, foreign-board slugs 404.
- Issue body composition: goal = title + full body verbatim; scope = the recommended template sections (## Repro / ## Expected / ## Actual, case-insensitive) extracted when present, missing ones marked "(not provided)", absence of all three noted in the scope; obstacles = the chronological append trail (or "(no follow-ups)"); refs name the issue id, author project and board project.
- Issue/IssueAppend intentionally carry NO navigation collections — the append trail is queried by IssueId; keeps the append path a single DbSet.Add with no graph attach.
- Migration AddIssues creates the two tables with indexes (ProjectId+Status, AuthorProjectId, IssueId+CreatedAt); only CREATE statements — FTS-safe by construction (no drops, no table rebuilds).
</obstacles>

<log>
- 2026-10-03 [agent] — dispatched to dotnet-coder (same session — carries the wave-099 patterns: FTS-safe migrations, entity idioms), plan validated
- 2026-10-03 [agent] — Executed by dotnet-coder. Core: IssueStatus enum, Issue entity (status machine with frozen transition map — forward chain, closed-from-any, no reopen, direct-promotion refusal, same-status no-op; Promote gated to open/triaged; ComposeTaskBody with Repro/Expected/Actual section extraction, append-trail obstacles, issue refs), IssueAppend (append-only factory, no update/remove paths). Infrastructure: IssueService (Create with board+author project validation → 404, Append from any project with provenance, SetStatus with source marker, Promote composing the task through PlanningArtifactService.UpsertAsync inside an explicit transaction — issue link + artifact in one atomic commit — with materializer sync after commit), DbContext DbSets + configs (board cascade, author restrict, indexes), migration AddIssues (CREATE-only, FTS-safe). DI: IssueService registered. Tests: Core IssueTests 22 (defaults, guards, append provenance, full status matrix incl. no-reopen and direct-promotion refusal, promote gates, composition variants), Infra IssueServiceTests 10 (create 404 matrix, append provenance, status persistence + source marker, no-reopen, promote composition + two-way link + unknown feature + double-promote refusal). Gates: dotnet build --no-incremental 0 errors / 0 warnings; full suite 1807 passed / 0 failed (Core 274, Infra 563, Api 571, Mcp 419); format clean outside Migrations.
- 2026-10-03 [agent] — task 01 executed to review by dotnet-coder: Issue/IssueAppend/IssueStatus domain + IssueService (status machine, transactional promotion through PlanningArtifactService.UpsertAsync) + AddIssues migration (CREATE-only, FTS-safe) + full unit matrix (Core 22 + Infra 10 tests). Gates: dotnet build --no-incremental 0 errors / 0 warnings; full suite 1807 passed / 0 failed (Core 274, Infra 563, Api 571, Mcp 419); format clean outside Migrations.
- 2026-10-03 [agent] — 2026-10-03 [agent] — reviewed by dotnet-code-reviewer: FAIL — 1 BLOCKER (AppendAsync skips authorProjectId validation; spec: every append carries a validated author; one-line fix + 404 test), 2 nits (Create source param, ArgumentException mapping into 028/03 brief); status machine/atomicity/composition/migration verified clean, build 0/0, 1807/1807 equivalent re-verified (4 Mcp startup flakes isolated-green); report audits/review-028-01.md. Fix loop dispatched.
- 2026-10-03 [agent] — Fix round for review 028/01: BLOCKER — AppendAsync now validates authorProjectId (RequireProjectAsync → 404 MissingProject), a phantom project id can no longer land in the append trail; pinned by Append_UnknownAuthorProject_ThrowsNotFound, and the provenance test now appends from a real seeded foreign project (was a raw Guid that the new validation correctly rejected). NIT — CreateAsync takes a PlanningLogSource source parameter (default Agent) so the future UI door records Ui provenance; pinned by Create_UiSource_RecordedOnTheIssue. Also: new PlanningException.MissingIssue factory already in use by Append/Promote not-found paths. Gates: dotnet build --no-incremental 0 errors / 0 warnings; per-project sequential run — Core 274, Infra 565, Api 571, Mcp 419 = 1829 passed / 0 failed. Note: a parallel `dotnet test Overmind.sln` run flaked with TimeoutException on random Api tests (WebApplicationFactory startup under 4-host parallel load, machine-dependent); isolated per-project runs are stable and green, same phenomenon was recorded in wave 099.
- 2026-10-03 [agent] — 2026-10-03 [agent] — re-reviewed fix round 1 (028/01) by dotnet-code-reviewer: PASS — append author validation + mirror test, source provenance from doors, provenance test strengthened; build 0/0, issue suites 22/22 + 12/12; report audits/review-028-01.md.
- 2026-10-03 [agent] — delta review PASS (audits/review-028-01.md, fix round 1): append author validation + source provenance; AC 6/6; 1829/1829. Closing as done.
</log>