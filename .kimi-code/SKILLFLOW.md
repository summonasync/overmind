# SKILLFLOW.md — Workflow Graph

Every skill reads this file before suggesting its «next step» (see Skill output contract in AGENTS.md).
The graph suggests — the user decides. Skills NEVER auto-proceed to the next step.

## Main Route (Complex feature)

```
0.0 requirements-intake ──► 0.1 spec gate ──► 0.2 plan gate ──► tasks ──► 2 implement ──► 3 review ──► 4 fix? ──► 5 arch gate
     (any-format docs            (USER              (USER            per       (coder per       (2-stage:        (loop back      (end of
      → requirements.md)          APPROVES)          APPROVES)        task       task file)       spec + code)     to 2)          phase only)
```

| Step | Skill / Owner | Artifacts | Gate |
|------|---------------|-----------|------|
| 0.0 Intake | task-orchestrator (intake mode) | `TASKS/nnn-slug/requirements.md` | User confirms requirements are complete |
| 0.1 Spec | `superpowers:brainstorming` via task-orchestrator | `TASKS/nnn-slug/feature.md` | **USER APPROVAL — frozen after** |
| 0.2 Plan | `superpowers:writing-plans` via task-orchestrator | `TASKS/nnn-slug/plan.md` | **USER APPROVAL — frozen after** |
| Tasks | task-orchestrator | `TASKS/nnn-slug/tasks/NN-*.md` + BACKLOG.md index row | — |
| 2 Implement | `dotnet-coder` / `next-js-coder` / `maui-coder` (K2.7) | code + `<obstacles>`/`<log>` in task file | `nuget-licensing-guard` before any NuGet change |
| 3 Review | `dotnet-code-reviewer` / `next-js-coder-reviewer` (K2.7); depth from task frontmatter `review:` — `light` (default) = code reviewer, `hard` = + `architect-review` (Light checklist) | `review-NNN-NN.md` | BLOCKER → Phase 4; Security BLOCKER → STOP, human only |
| 4 Fix loop | coder ← review comments | updated code | back to Phase 3 |
| 5 Arch gate | `architect-review` Hard checklist (end of sprint phase / big refactor only) | `architect-review-YYYY-MM-DD.md` + ADR in `TECHNICAL-SOLUTIONS.md` | — |

## Alternative Routes

**Quick fix (bug):**
QA/bug report arrives AS a task file (AGENTS.md rule 10: goal = expected behavior, acceptance = fix verification) → `superpowers:systematic-debugging` (root cause first) → coder fix → `superpowers:verification-before-completion` → done. No spec/plan gates. Complex bugs (>3 files) still get Phase 3 review.

**Simple task (<3 files, single layer):**
coder directly → done. No folder, BACKLOG.md one-liner only.

**Docs-only:**
`documentation-writer` — skips Phase 3 code review, uses its own verification checklist.

**Coding challenge / тестовое задание:**
`coding-challenge-orchestrator` — replaces this entire flow with its fixed pipeline.

**System-wide audit (sprint retro, phase end):**
`audit-orchestrator` — outside the feature flow; outputs `audit-YYYY-MM-DD.md`.

**Self-healing CI/CD (failing pipeline):**
`pipeline-doctor` — log-reader (K2.7) → fix (K2.7) → trigger pipeline → watch → repeat, **max 3 iterations, then STOP and report**. Review of the final fix by a separate agent. Transport: `gh` CLI for cloud GitHub (token scope = security perimeter, never widen it for the agent); `glab`/REST for on-prem Git — transport is swappable, the loop is not. Tell the agent explicitly when the codebase is available locally, or it will read files through the API.

## Hard Rules (always on, any route)

- No auto-commits, ever — `git commit`/`git push` only on explicit user request.
- Sub-agents: `Kimi K2.7` (default) / `Kimi K3` (orchestration, audit, detection, obfuscation) only. `primary` alias forbidden.
- Executing sub-agent reads ONLY its task file. feature.md/plan.md belong to orchestrator + reviewer.
- Frozen artifacts (approved feature.md/plan.md) change only via re-validation + version bump. Removed content gets a `<removed>` tombstone, never silent deletion (context-clash defense).
- Cloud CLI mutations (`gcloud`/`az`/`aws`) are never autonomous — user approval every time; infra goes through IaC.
- Every skill ends with the output contract: Done → Next step → Alternatives → STOP.
