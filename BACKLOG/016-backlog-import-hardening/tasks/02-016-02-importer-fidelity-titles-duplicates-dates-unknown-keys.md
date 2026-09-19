---
feature: 016
task: 02
slug: backlog-import-hardening
status: done
review: light
created: 2026-09-19
---

# 016/02 — Importer fidelity (titles, duplicates, dates, unknown keys)

<goal>
The backlog importer stops silently losing data: feature/task titles strip both prefix separator forms, duplicate task order numbers are loud skips, missing created tokens fall back to file mtime, and every unknown frontmatter key round-trips (stored on the artifact, registered in the key registry, listed in the report).
</goal>

<context>
Feature 016, second task (registry from 01 is available). Dogfood findings A1/A2/A4/A5 from the MindlashStudio import (repro: /mnt/Ubuntu-Dev/Projects/AI-Tools/MindlashStudio, 3 features / 37 tasks). The executing subagent reads ONLY this file.

Importer code lives in src/Overmind.Infrastructure/Planning/: BacklogImporter.cs (engine), BacklogMarkdown.cs (frontmatter/section parsing), BacklogNaming.cs (slugs/titles — ResolveTitle lives here or nearby), BacklogImportReport.cs (report model), BacklogFeatureImportResult.cs.
</context>

<scope>
- Title strip (A2): ResolveTitle strips BOTH «Feature 001 — X» (em-dash) and «Feature 001: X» (colon) forms (and the task-file equivalents); pinned by tests for both separators.
- Duplicate order numbers (A4): two task files with the same NN prefix in one feature → first (alphabetical) imports, losers get a report entry (skipped, reason: duplicate order) — never silent last-wins. Test with 01-a.md + 01-b.md.
- created fallback (A5): explicit created token → source File.GetLastWriteTimeUtc → import day; files that fell back are listed in the report. Test: file without created token gets its mtime.
- Unknown frontmatter keys (A1, per spec v2 — nothing is dropped):
  - PlanningArtifact gains an additive preserved-frontmatter column (JSON map string, nullable) — EF migration `dotnet ef migrations add AddArtifactPreservedFrontmatter --project src/Overmind.Infrastructure`
  - Importer: keys it maps structurally (status/review/version/created/feature/task/slug/validated) behave as today; ALL other keys are stored into the preserved map verbatim (name → raw value string) and registered via the task-01 registry (GetOrRegisterAsync)
  - Report: newKeys list ({name, color}) aggregated per run + per-file skipped-key entries are GONE (nothing is skipped anymore); keys that fell into preserved storage are listed per file (transparency, not as errors)
  - Round-trip: when the materializer emits a feature/spec/plan/task file (planning materializer, not kits), preserved keys are written back into frontmatter. NOTE: the materializer-side emit half may live in task 03 if the files overlap heavily — if you touch PlanningMaterializer here, say so in the log; otherwise leave emit to 03 and store-only here
</scope>

<anti-scope>
- No filename pinning / pre-seed (task 03), no CLI changes (task 04)
- No mapping of foreign keys into our model (depends_on stays preserved text, not structured refs)
- No UI work
</anti-scope>

<constraints>
- .NET 10 / C# 14; no new NuGet packages; no comments; match BacklogMarkdown's existing parsing style
- JSON serialization of the preserved map: keys in ordinal-sorted order (stable bytes — phantom drift otherwise)
- dotnet build + dotnet test green (baseline 677 + new + task-01's)
</constraints>

<acceptance-criteria>
- [ ] Both separator forms stripped (tests)
- [ ] Duplicate NN → loser skipped with report entry (test); no silent overwrite
- [ ] mtime fallback works and is reported (test)
- [ ] Unknown keys preserved on the artifact (JSON column) + registered with color + newKeys in report (tests)
- [ ] No frontmatter key is silently dropped anymore (test asserting report completeness)
- [ ] dotnet build + test green
</acceptance-criteria>

<refs>
- @src/Overmind.Infrastructure/Planning/BacklogImporter.cs, BacklogMarkdown.cs, BacklogNaming.cs, BacklogImportReport.cs
- @tests/Overmind.Infrastructure.Tests — existing importer test files (follow their fixture style)
- tasks/01-key-registry.md — dependency (registry service)
- tasks/03-name-pin-preseed.md — materializer-side emit of preserved keys lands here if not done in 02
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-19 — Created from validated plan 016
- 2026-09-19 — Started: importer fidelity (coder dispatched)
- 2026-09-19 — Coder done: title strip, loud duplicates, mtime fallback, preserved frontmatter + registry wiring; 719/719 green; light review dispatched
- 2026-09-19 — Light review PASS-WITH-NITS (0 blocker/important, 6 nit), audits/review-016-02.md; NIT-4 (corrupt preserved JSON crashes materialize) fixed in-tree, 725/725 green
</log>
