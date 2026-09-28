# SKILLFLOW.md — Workflow Graph

Every skill reads this file before suggesting its «next step» (see Skill output contract in AGENTS.md).
The graph suggests — the user decides. Skills NEVER auto-proceed to the next step.

**All planning state lives on the Overmind board (MCP).** Ceremony mechanics (probe, lifecycle,
gates, sub-agent briefing, capability matrix) are owned by the `overmind-ceremony` skill.
`BACKLOG/` on disk is a read-only materialized mirror — never hand-edit it.

## Main Route (Complex feature)

```
0.0 requirements-intake (OPTIONAL) ──► 0.1 spec gate ──► 0.2 plan gate ──► tasks ──► 2 implement ──► 3 review ──► 4 fix? ──► 5 arch gate
     (messy raw inputs            (USER              (USER            per       (coder per       (2-stage:        (loop back      (end of
      → intake notes)              APPROVES →         APPROVES →       task       task artifact)   spec + code)     to 2)          phase only)
                                   planning_validate) planning_validate)
```

| Step | Skill / Owner | Artifacts (Overmind board) | Gate |
|------|---------------|-----------|------|
| 0.0 Intake (OPTIONAL, trigger-based — only for messy raw inputs) | feature-orchestrator (intake mode) | notes in feature body / entry | User confirms requirements are complete |
| 0.1 Spec | feature-orchestrator + Socratic questioning (`overmind-ceremony` techniques) | spec artifact (`planning_upsert_artifact`) | **USER APPROVAL → `planning_validate` — frozen after** |
| 0.2 Plan | feature-orchestrator + plan decomposition (`overmind-ceremony` techniques) | plan artifact | **USER APPROVAL → `planning_validate` — frozen after** |
| Tasks | feature-orchestrator | task artifacts with `<goal>/<scope>/<anti-scope>/<constraints>/<acceptance-criteria>/<refs>/<obstacles>/<log>` | — |

The feature is created on the board (`planning_create_feature`) when it enters the backlog, **before Phase 0**.
| 2 Implement | `dotnet-coder` / `next-js-coder` / `maui-coder` (strong execution model) | code + `<obstacles>`/`<log>` patched into the task artifact | `nuget-licensing-guard` before any NuGet change |
| 3 Review | `dotnet-code-reviewer` / `next-js-code-reviewer` (strong execution model); depth from the task's `review:` flag — `light` (default) = code reviewer, `hard` = + `architect-review` (Light checklist) | `audits/review-NNN-NN.md` | BLOCKER → Phase 4; Security BLOCKER → STOP, human only |
| 4 Fix loop | coder ← review comments | updated code | back to Phase 3 |
| 5 Arch gate | `architect-review` Hard checklist (end of sprint phase / big refactor only) | `audits/architect-review-YYYY-MM-DD.md` + ADR in `TECHNICAL-SOLUTIONS.md` | — |

## Alternative Routes

**Quick fix (bug):**
QA/bug report arrives AS a task artifact on the board (goal = expected behavior, acceptance = fix verification) → root-cause-first debugging (`overmind-ceremony` techniques — no fix without reproduced root cause) → coder fix → verification-before-completion → `planning_set_status(done)`. No spec/plan gates. Complex bugs (>3 files) still get Phase 3 review.

**Simple task (<3 files, single layer):**
Board feature with a single task artifact, no spec/plan (same shape the Overmind project itself dogfoods) → coder directly → done.

**Fast fix (cosmetic micro-change):**
`fast-fixer-coder` — 1 file, ≤10 changed lines, cosmetic only (icons, labels, styles, wording, typos in UI strings). Zero paperwork: no board feature, no task artifact, no review gate. The scope gate is absolute — anything wider is REFUSED and handed off to the matching coder; forbidden zones (deps, config, secrets, migrations, security) are never touched. Defects are bugs → the quick-fix route above, never fast-fixer.

**Docs-only:**
`documentation-writer` — skips Phase 3 code review, uses its own verification checklist.

**Coding challenge / тестовое задание:**
`coding-challenge-orchestrator` — replaces this entire flow with its fixed pipeline.

**System-wide audit (sprint retro, phase end):**
`audit-orchestrator` — outside the feature flow; reads state from the board; outputs `audits/audit-YYYY-MM-DD.md`.

**Self-healing CI/CD (failing pipeline):**
`pipeline-doctor` — log-reader (strong execution model) → fix (strong execution model) → trigger pipeline → watch → repeat, **max 3 iterations, then STOP and report**. Review of the final fix by a separate agent. Transport: `gh` CLI for cloud GitHub (token scope = security perimeter, never widen it for the agent); `glab`/REST for on-prem Git — transport is swappable, the loop is not. Tell the agent explicitly when the codebase is available locally, or it will read files through the API.

## Hard Rules (always on, any route)

- No auto-commits, ever — `git commit`/`git push` only on explicit user request.
- Sub-agents: `strong execution model` (default) / `strongest reasoning model` (orchestration, audit, detection, obfuscation) only, selected in Agent Manager. Never inherit the main conversation's model for delegated work.
- Executing sub-agent reads ONLY its task artifact (via `planning_read_artifact` — never from disk when MCP is available). spec/plan artifacts belong to orchestrator + reviewer; the feature body is readable by all.
- Frozen artifacts (validated spec/plan) change only via `planning_patch_artifact` with `logNote` or re-validation with the user. Removed content gets a `<removed>` tombstone, never silent deletion (context-clash defense).
- Overmind MCP is a hard requirement for planning ceremonies — no MCP → STOP, no file-based fallback.
- Cloud CLI mutations (`gcloud`/`az`/`aws`) are never autonomous — user approval every time; infra goes through IaC.
- Every skill ends with the output contract: Done → Next step → Alternatives → STOP.
