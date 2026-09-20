---
feature: 021
slug: task-brief
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Spec 021 — Task brief description visible in the tasks table

<decisions>
- Brief is an explicit, optional scalar field on PlanningArtifact (string?, max 500 chars) — NOT auto-derived from <goal> (user decision 2026-09-20). Agents and humans write a meaningful one-liner; empty brief simply does not render.
- Valid only for kind=task: the MCP tool and the Host API accept `brief` on task artifacts; for spec/plan/result it is a 400. Explicit contract beats silent ignore.
- Update semantics: `brief: null` (absent) = leave unchanged; `brief: ""` = clear. Keeps the body-only update DTO honest now that it gains a second optional field.
- No versioning: PlanningArtifactVersion snapshots Body only; a brief change bumps UpdatedAt but creates NO body version and requires NO logNote — brief is display metadata, not ceremony contract (user decision 2026-09-20).
- Round-trip: `brief` becomes a STRUCTURAL frontmatter key in materialized task files (typed, like `review`/`created`) — not a preserved-JSON key, not an XML body section. Otherwise materialize-on + re-import would silently drop or untype it.
- UI: TaskCard renders the brief as muted multi-line text under the title (CSS line-clamp ~3 lines); empty brief renders nothing (no placeholder dash — kanban cards stay clean). Editing happens ONLY in TaskEditor (input above the body editor), on both create and edit (user decisions 2026-09-20).
</decisions>

<open-questions>
- none
</open-questions>

<requirements>
Core + Infrastructure:
- PlanningArtifact gains nullable `Brief` (string?) with a SetBrief-style mutation following the existing SetReview pattern; EF migration adding a nullable TEXT column (template: 20260919172235_AddArtifactPreservedFrontmatter.cs).
- PlanningMaterializer TaskDocument frontmatter emits `brief:` when non-null; BacklogImporter adds `brief` to StructuralKeys and reads it in TryParseTaskAsync; BacklogArtifactImport carries Brief; PlanningFeatureService.ImportAsync restores and diffs it (absent key → null, never a change).
- Kind guard in PlanningArtifactService upsert: brief present on a non-task artifact → typed 400 (InvalidInput), both doors.

Host API + MCP (additive-only):
- CreatePlanningArtifactRequest + `Brief?`; UpdatePlanningArtifactRequest + `Brief?` (null = untouched, "" = clear); PlanningArtifactResponse + `brief` (null for non-tasks).
- FluentValidation: Brief MaximumLength(500) when present.
- MCP planning_upsert_artifact gains optional `brief` param (task kind only, same 400 rule); tool description documents it in one clause.
- Update the pinned contract test PlanningToolContractTests (exact posted-JSON key set).

Web:
- types.ts PlanningArtifactResponse + `brief?: string | null`; create/update value types carry it.
- TaskCard: muted multi-line brief under the heading, line-clamp 3; no rendering when empty.
- TaskEditor: Brief input above the body editor, submitted on create and edit; invalidation unchanged (existing board+feature keys).

Tests:
- Api.Tests: create with brief, update set/clear/untouched semantics, 400 on brief for non-task kinds, response shape.
- Infrastructure.Tests: round-trip materialize → import preserves brief; old files without brief import as null without change detection.
- Mcp.Tests: posted JSON includes brief when provided; 400 path surfaces the typed error.
</requirements>

<acceptance-ideas>
- A task created via MCP with `brief: "Wire the polling hook"` shows that text (up to 3 lines) under its card title in the kanban; a task without brief renders no extra line.
- Editing the brief in TaskEditor persists and clears correctly ("" clears); body version count unchanged after a brief-only edit.
- materialize → wipe feature → re-import restores the same brief (typed field, not preserved-JSON).
- planning_upsert_artifact with brief on a spec artifact → 400 with a clear message.
- dotnet test green incl. new tests; MCP contract test updated, tool surface otherwise unchanged (additive).
</acceptance-ideas>
