---
feature: 081
task: 01
slug: task-dependencies
status: done
review: light
created: 2026-09-27
brief: PlanningTaskDependency entity + EF config + hand-written migration + SQLite round-trip tests
---

# 081/01 — Core: PlanningTaskDependency entity + persistence

<goal>
The data foundation for task-level edges: a link-table entity mirroring PlanningFeatureDependency (030) one level down, plus EF mapping and migration.
</goal>

<context>
Executor reads ONLY this file. Template: src/Overmind.Core/Entities/PlanningFeatureDependency.cs (private ctor, internal Create, navs). EF config location: find where PlanningFeatureDependency is configured (Infrastructure Persistence) and follow. Target entity: src/Overmind.Core/Entities/PlanningTaskDependency.cs — BlockedById → the blocked task artifact, BlockingId → the blocker; both PlanningArtifact (task kind). Migration: migrations live in the Host persistence project — follow the latest migration naming/date pattern (NOTE: dotnet-ef CLI is broken in WSL per 072 field report — hand-write the migration class following the latest existing one, and pin schema correctness with a test on real SQLite, the 072 workaround).
</context>

<scope>
- src/Overmind.Core/Entities/PlanningTaskDependency.cs — 030 clone: BlockedById/BlockingId + task navs + internal Create; unique (BlockedById, BlockingId) enforced at EF level.
- PlanningArtifact (task): collection nav for outgoing edges (BlockedBy edges) — minimal, service-managed; no domain invariant logic here (service owns graphs, 030 pattern).
- EF configuration + hand-written migration following the latest existing migration class (update the migrations snapshot class by hand following its structure).
- Tests: real-SQLite round-trip (entity persists, unique pair enforced, cascade on artifact delete) — Infrastructure.Tests or Core.Tests per existing placement of EF tests.
</scope>

<anti-scope>
- No service/handler/MCP/web changes (later tasks). No closure semantics (task 02).
</anti-scope>

<constraints>
- No comments; English; no new packages; NO git commits.
- dotnet build 0W/0E; full dotnet test green (baseline 1561).
- Diff: Core entity + Infrastructure persistence/migration + tests.
</constraints>

<acceptance-criteria>
- [x] PlanningTaskDependency entity + EF config + hand-written migration compile and apply on a fresh SQLite.
- [x] Unique (BlockedById, BlockingId) pair enforced at DB level.
- [x] Cascade delete removes edges when either task artifact is deleted.
- [x] dotnet build 0W/0E + full suite green.
</acceptance-criteria>

<refs>
- src/Overmind.Core/Entities/PlanningFeatureDependency.cs (template)
- Latest migration class (find by date/name pattern)
- 072 field report: dotnet-ef CLI broken in WSL — hand-write migrations, pin with SQLite test
</refs>

<obstacles>
- Scope keeps graph mutation out of the domain (service owns edges, task 02), so tests construct links via internal PlanningTaskDependency.Create. Core.csproj gained <InternalsVisibleTo Include="Overmind.Infrastructure.Tests" /> (same pattern Infrastructure.csproj already uses) — not a package, test-visibility only.
- Migration hand-written per 072 field report (dotnet-ef CLI dead in WSL): 20260927000000_AddPlanningTaskDependencies + Designer generated from the edited model snapshot; correctness pinned by PlanningTaskDependencyMigrationTests on real SQLite — staged Migrate("20260925123000_AddProjectLastMaterializationRun") then full Migrate() (EF PendingModelChangesWarning would fail the run on any snapshot/model drift).
</obstacles>

<log>
- 2026-09-27 — created from validated plan ca4721f1; dispatch first.
- 2026-09-27 [agent] — task executed: entity + EF config + hand-written migration + SQLite tests; quirks recorded
- 2026-09-27 [agent] — 081/01 executed: PlanningTaskDependency entity (030 clone), PlanningArtifact.BlockedBy nav, inline EF config + DbSet in OvermindDbContext, hand-written migration 20260927000000_AddPlanningTaskDependencies (+Designer, snapshot in sync), 5 SQLite tests in PlanningTaskDependencyMigrationTests. dotnet build 0W/0E; full dotnet test 1566/1566 green (baseline 1561 + 5). All 4 AC ticked.
- 2026-09-27 [agent] — 081/01 done by executor, moving to review: build 0W/0E, full suite 1566 green, 4/4 AC ticked, migration pinned by real-SQLite tests (072 workaround). Not marking done — review gate.
- 2026-09-27 [agent] — light review PASS-WITH-NITS (2 NIT microbatch + 1 OBSERVATION → docs-sweep 07) — audits/review-081-01.md; независимые гейты: build 0W/0E, 1566/1566; миграция Designer==снапшот байт-в-байт, staged Migrate + raw-INSERT UNIQUE + каскады с обоих концов на реальном SQLite; done by orchestrator after review
</log>