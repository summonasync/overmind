---
feature: 021
slug: task-brief
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Plan 021 — Task brief description visible in the tasks table

<approach>
One additive scalar through the existing three doors, in dependency order. The contract lands first (entity + migration + Host/MCP surfaces + tests) because both later tasks consume the typed field. The disk round-trip (materializer frontmatter + importer structural key) is its own task — it is the part that silently breaks if rushed, and it has its own test seam (PlanningRoundTripTests). The web task rides the finished contract and closes the feature out with a live UI dogfood. No new endpoints, no new tools — brief rides the existing upsert/update/response shapes everywhere.
</approach>

<contract>
- PlanningArtifact.Brief: string? — set via a SetBrief mutation (SetReview pattern); nullable TEXT column, EF migration modeled on 20260919172235_AddArtifactPreservedFrontmatter.cs.
- Create: POST artifacts gains optional `brief` (task kind only; brief on spec/plan/result → 400 InvalidInput via PlanningErrors). Update: PUT artifact gains optional `brief` — null/absent = untouched, empty string = cleared. Response PlanningArtifactResponse gains `brief` (null for non-tasks).
- FluentValidation: Brief MaximumLength(500) when present (create + update validators).
- MCP planning_upsert_artifact: optional `brief` parameter, posted in the same JSON body; kind guard identical (400 surfaces the typed error to the agent).
- Materialized task frontmatter: `brief: <value>` emitted when non-null; `brief` added to BacklogImporter StructuralKeys (so it never lands in preserved-JSON); TryParseTaskAsync reads it; BacklogArtifactImport.Brief; ImportAsync restores + diffs (absent → null, no change flagged).
- Web wire: PlanningArtifactResponse TS mirror + `brief?: string | null`; create/update payload types carry brief; no query-key changes (existing board+feature invalidation).
</contract>

<tasks>
- [ ] 01-brief-backend-contract — entity Brief + SetBrief, EF migration, PlanningArtifactService kind-guard + brief apply, Host DTOs (create/update/response) + FluentValidation, MCP upsert brief param, contract + Api tests → tasks/01-brief-backend-contract.md
- [ ] 02-brief-roundtrip — materializer TaskDocument emits brief, importer StructuralKeys + TryParseTaskAsync + BacklogArtifactImport.Brief, ImportAsync restore/diff, round-trip + old-file tests → tasks/02-brief-roundtrip.md
- [ ] 03-brief-frontend-closeout — types.ts mirror, TaskCard muted multi-line brief (line-clamp 3, empty renders nothing), TaskEditor brief input on create+edit, tsc/lint green, live UI dogfood, result artifact → tasks/03-brief-frontend-closeout.md
</tasks>

<risks>
- UpdateArtifact today is body-only in the web client too (TaskEditor submits body alone) — task 03 must keep sending brief=null-vs-empty correctly: editing an unrelated field must NOT clear an existing brief (null = untouched). The TS update type makes brief optional; the editor sends it only when the brief input is dirty, or always with the loaded value — decide in-task, pin with a test/manual check.
- The MCP contract test pins the exact posted-JSON key set (PlanningToolContractTests) — updating it is part of task 01, not an afterthought; a missed update fails the suite loudly (good).
- Import diff: MatchesImportedBody compares body only; brief needs its own equality check in ImportAsync — a missing check means brief edits on disk never register as changes (silent drift). Task 02 pins it with a test.
- Existing materialized repos (this one) carry task files without `brief` — importer must treat absence as null without flagging drift; covered by the old-file test.
</risks>

<verification>
- dotnet build Overmind.sln + dotnet test Overmind.sln green incl. new tests (baseline: current suite count, record it in the task log).
- Round-trip: materialize a feature with briefed tasks → wipe → re-import → brief values identical, typed field (not preserved-JSON); a task file without brief imports null, no change flagged.
- Kind guard: brief on spec/plan/result via both Host and MCP → 400 with a clear message.
- Web: npx tsc --noEmit + npm run lint green; kanban card shows the brief (≤3 lines) under the title, hides cleanly when empty; TaskEditor create+edit+clear round-trips.
- Live dogfood: create a task with brief via MCP in this project, see it in the UI without reload dance.
</verification>
