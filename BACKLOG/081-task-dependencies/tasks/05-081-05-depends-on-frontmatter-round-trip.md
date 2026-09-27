---
feature: 081
task: 05
slug: task-dependencies
status: done
review: light
created: 2026-09-27
brief: depends_on frontmatter: materializer write + importer read, byte-stable round-trip
---

# 081/05 — depends_on frontmatter round-trip

<goal>
Task dependency edges survive the disk mirror: depends_on key on task files (blockers-first canonical order) written by the materializer and read back by the importer — byte-stable, 030 bar.
</goal>

<context>
Executor reads ONLY this file. Prerequisite: task 02 done. 030 precedent (copy the mechanics): depends_on registry-driven frontmatter key with sorted-ordinal canonical projection, blockers-first import ordering, additive import with linked:/unlinked: report entries, byte-stable round-trip into a fresh DB. Key files: materializer frontmatter registry (find via grep depends_on on Infrastructure), BacklogImporter task parsing.
</context>

<scope>
- Materializer: task files gain depends_on: [orders] (sorted ascending, only when edges exist) — canonical projection, byte-stable with edges and without.
- BacklogImporter: task frontmatter depends_on maps to edges (resolved after ALL task files of the feature are loaded — orders known); unknown order → skipped: report entry (not a hard fail); report carries linked:/unlinked: like 030.
- Tests (Infrastructure.Tests): round-trip import→materialize→re-import byte-stable; depends_on absent when no edges; blockers-first ordering; unknown order report entry.
</scope>

<anti-scope>
- No Host/Mcp/web; no spec/plan/result frontmatter changes.
</anti-scope>

<constraints>
- No comments; English; NO git commits.
- dotnet build 0W/0E; full suite green.
- Diff: Infrastructure materializer/importer + Infrastructure.Tests.
</constraints>

<acceptance-criteria>
- [x] Task files carry depends_on: [orders] (sorted, only with edges); byte-stable round-trip.
- [x] Importer maps depends_on to edges; unknown order → skipped: report (no hard fail).
- [x] Report entries linked:/unlinked: per 030 precedent.
- [x] dotnet build 0W/0E + full suite green.
</acceptance-criteria>

<refs>
- 030 depends_on mechanics (registry key, blockers-first, linked:/unlinked: reports)
- BacklogImporter task parsing + materializer frontmatter registry
</refs>

<obstacles>
<obstacles>
- 2026-09-28 — PlanningFeatureService ctor gained TaskDependencyService, so Host AddBacklogImportPipeline (CLI-only registration path) needed one DI line `AddScoped<TaskDependencyService>()`; without it the import CLI exits 1. Anti-scope "no Host" read as no Host feature work — mandatory DI wiring, mirrors the task 02 registration in AddOvermind.
- 2026-09-28 — Existing tests used `depends_on` on task files as the canonical PRESERVED-unknown-key example (BacklogImporterTests Import_UnknownFrontmatterKeys_PreservedOnArtifactAndListedPerFile, BacklogImportMaterializationTests Import_PreservedFrontmatterKeys_RoundTripThroughMaterializedFile); both rewritten to use `epic: demo` since depends_on is now a parsed structural key for tasks. FrontmatterKeyRegistry seeded palette untouched.
- 2026-09-28 — Edge restore must run AFTER the first SaveChangesAsync inside the import transaction: freshly restored task artifacts are invisible to the siblings query before the save. RestoreFromImportAsync stages edges additively (existing edges survive, import is additive like 030) and reports a cycle as an unlinked note instead of failing the import.
</obstacles>
</obstacles>

<log>
- 2026-09-27 — created from validated plan ca4721f1; dispatch after task 02; parallel-safe with tasks 03-04.
- 2026-09-27 [agent] — implemented: materializer writes depends_on (sorted asc, only with edges) via TaskDocument + ThenInclude(BlockedBy); importer parses task depends_on as structural key, edges restored additively in-transaction after first save (TaskDependencyService.RestoreFromImportAsync); linked:/unlinked: report lines per 030. Host pipeline got the required TaskDependencyService DI line (see obstacles). Build 0W/0E; full suite green 1614 tests (Core 225, Infrastructure 535, Api 470, Mcp 384), +7 new tests.
- 2026-09-27 [agent] — implementation complete, moved to review by executor (dotnet-coder): 4/4 AC ticked, build 0W/0E, full suite 1614 green incl. 7 new depends_on tests
- 2026-09-27 [agent] — light review PASS-WITH-NITS (1 WARNING microbatch + 1 NIT + 1 OBS) — audits/review-081-05.md; независимые гейты: build 0W/0E, Infrastructure 535/535 + точечные 114/114; SaveChanges-квирк (restore после первого save) подтверждён кодом как необходимый; DI-строка accept как обязательный wiring; done by orchestrator after review
</log>