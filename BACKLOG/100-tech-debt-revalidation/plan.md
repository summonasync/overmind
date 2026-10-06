---
feature: 100
slug: tech-debt-revalidation
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Plan 100 — Tech debt: full structural re-validation of the codebase (god-files, seams, FSD)

<goal>
Define how the hard architect-review runs: executor, inputs, ordered procedure, output contract, findings flow-back and verification. Consumed by the orchestrator in phase C — after spec (2086fff7-a874-46b0-8169-a3411449a677) and this plan are validated in the consolidated human sitting.
</goal>

## Executor
- architect-review sub-agent, HARD mode — one dispatch over the whole solution.
- Model: opus (audit of long-context tasks per Sub-Agent Rules — the sweep spans four C# projects plus web/src in a single context).
- Toolset: read-only — squeezr (repo_map, symbol_lookup, code_search, find_references), Read/Grep/Glob, git log for wave context. Writable path: audits/ only.

## Inputs
- Solution tree: src/ (Overmind.Core, Overmind.Host, Overmind.Infrastructure, Overmind.Mcp), tests/ (four test projects), web/src (FSD). OvermindCrewKit/ is kit distribution, not C#.
- Three named files, line counts as of 2026-10-06: src/Overmind.Mcp/OvermindPlanningTools.cs (1354), src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs (915), src/Overmind.Infrastructure/Planning/BacklogImporter.cs (703).
- web/src FSD layout: app, pages, widgets, features, shared; entities/ absent.
- Baseline: audits/architect-review-2026-09-30-090.md, including its rationale for keeping OvermindPlanningTools.cs single-class (MCP registration).
- Landed wave context: 86e8eed (test 125), 2fa454c (refactor 125 microbatch), 04104b9 (feat 125 microbatch).
- Rules enforced by the audit: CLAUDE.md "Avoid God-Services", STRUCTURE.md conventions.

## Procedure
1. Orientation — repo_map over the workspace, index_status freshness check (a stale index is noted in the report's methodology section), csproj dependency graph.
2. God-file deep dive — per named file: responsibility inventory by method groups, coupling via find_references, internal duplication, extraction candidates; verdict split-partials / extract-services / keep-as-is.
3. src sweep — dependency direction, seam quality between Mcp / Infrastructure / Host, Host DI registration shape, Infrastructure EF usage patterns.
4. web sweep — FSD cross-layer import violations, RSC/client boundary drift as structural signal, BFF seam shape, entities/-layer question.
5. Outlier scan — file/class size distribution, multi-responsibility classes, naming drift, duplication hotspots.
6. Classification — every finding gets severity + file:line + evidence + proposed action; P0s get follow-up sketches on the spot.
7. Report write, coverage self-check, handoff list back to the orchestrator.

## Output contract
- File: audits/architect-review-<run-date>-100.md, sections in order:
  1. Executive summary — verdict counts by severity, top-3 risks.
  2. Methodology — tools, mode, coverage, index freshness note.
  3. Findings — grouped blocker / warning / suggestion; each with file:line, evidence, proposed action.
  4. God-file verdicts — the three named files plus any new outliers the sweep surfaces.
  5. Follow-up map — P0 to feature sketches (title + one-paragraph goal + file map); cosmetic to numbered microbatch ledger items.
  6. Coverage checklist — every named area ticked clean-or-findings.
  7. Scope context — wave commits listed as context; explicit statement that no landed decision was reopened.

## Findings flow-back
- P0 blockers → planning_create_feature per finding, ON USER REQUEST after the human reads the report. This feature never auto-creates features; the report's sketches are the raw material.
- Cosmetic warnings/suggestions → crew microbatch ledger append (next free numbers), closed later in batches.
- Structural warnings (large refactors) → follow-up features sized by the report's file map.
- The audit itself performs NO board mutations and NO source edits.

## Verification
- Audit completion check: three god-files + src dependency direction + DI shape + EF usage + web FSD + outlier scan — each area present in the report with findings or an explicit clean verdict (the report's own coverage checklist, self-verified before handoff).
- Spec AC cross-map: each spec acceptance item lands in a named report section; the orchestrator verifies the map before reporting the task done.
- git status after the run: only audits/ modified — anything else is a spec violation (blocker).
- After delivery: dated log line appended to the feature log by the orchestrator.

<refs>
- Spec artifact 2086fff7-a874-46b0-8169-a3411449a677 (this feature) — the audit contract this plan executes.
- 099-agent-claims — at-rest precondition, satisfied (wave landed).
- audits/architect-review-2026-09-30-090.md — comparison baseline.
</refs>

<obstacles>
None at draft time. Phase C dispatch happens after the consolidated validation sitting; re-check the tree-at-rest condition immediately before the run.
</obstacles>

<log>
- 2026-10-06 [agent] — plan draft written by feature-orchestrator (batch mode; no task artifacts — task breakdown waits for phase C after validation).
- 2026-10-06 [agent] — validated with human proof: notification d7458044-6fbf-4908-974e-ecaccfa54a79 replied 2026-10-06 13:05:13 UTC
</log>