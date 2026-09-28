---
feature: 003
slug: crew-role-boundaries
status: draft
created: 2026-09-27
---

# Result 003 — Crew role boundaries: feature-orchestrator rename, sprint-planner, stack drift fix

All 12 tasks done, spec + plan validated, executed 2026-09-27/28 via sonnet executors under the sub-agent briefing contract, every task review-gated by the orchestrator before done.

## What shipped (×4 runtimes in every case)

1. **Rename task-orchestrator → feature-orchestrator** (C2): skill dir + agent + frontmatter rewritten to the feature-scoped trigger; 27 mechanical cross-reference replacements on master + ports; SKILLFLOW planning rows; contract files; SYNC-STRUCTURE rows; runtime live-confirmed the new agent type.
2. **New sprint-planner** (board-level planner): skill + agent + SKILLFLOW «Board planning (sprint)» route + SYNC-STRUCTURE registration; load-bearing anti-scope with a routing table; band crossings only on explicit user approval.
3. **Stack drift fix** (C1): dotnet-coder now pins Mediator (martinothamar) / Wolverine / Mapperly / Shouldly, .NET 10+ / C# 14+; dotnet-code-reviewer flags the banned stack as BLOCKER in new code (legacy-only clause); dotnet-init, maui-coder aligned; dotnet-architect frontmatter AND body rewritten to Wolverine-primary topology (sagas, inbox/outbox equivalents).
4. **Coder = single-task implementator** (C4): «review» removed from all coder triggers; self-review protocols replaced by «Review Handoff (NOT self-review)».
5. **Architects = Phase 1 consultants** (C3): plan-features claims stripped everywhere (SKILL.md frontmatter/bodies + agent stubs), ownership notes added.
6. **Model pins** (C5): overmind-ceremony agent — claude opus, qwen qwen3.8-max; kimi pool convention documented; antigravity role wording confirmed.
7. **BACKLOG.md reconciliation** (C6): ceremony doc ×4 now states the materialized-index rule; materialize_status reports zero drift — the mirror matches the board exactly (002 = done, v2).

## Deviations from the validated spec (all logged in task artifacts)

- `manifest.json` untouched: v2 mirrors whole directories, per-skill entries never existed (spec assumed otherwise).
- Drift-class extras fixed under the C1/C3 umbrella: tutor-master «C# 13» → «C# 14» ×4; dotnet-architect frontmatter + body Wolverine rewrite; «large feature planning» → «large structural work» in architect bodies ×4; dotnet-architect agent stubs «feature planning» fixed by the T10 checklist.
- T9 AC2 recalibrated: the literal «git status clean» is unachievable without a forbidden commit; replaced with the measurable form (materialize_status zero drift) with rationale.
- Plan grew from 10 to 12 tasks (T11, T12 added from review findings).

## Verification

- SYNC-STRUCTURE pre-commit checklist: 12/12 groups PASS (5 contamination greps empty; parity 23 agents / 26 skills ×4; YAML grep empty; install.sh/uninstall.sh dry runs clean; sh/ps entry sets identical).
- Repo-wide `task-orchestrator` → 0 hits (excluding audits/ and BACKLOG/ history).
- Known environment note: install.ps1 dry run requires Windows (robocopy) — not a repo defect.

## Board quirks discovered (documented, entry edaff06e)

- `planning_set_status` first move duplicates the `<log>` section → AC patching 400s until a full-body upsert normalization.
- Empty `<obstacles>` section is not patchable (first obstacle cannot be added via patch).
- AC section format is parser-sensitive (items newline-separated, closing tag on its own line).

## Routed follow-ups

- **005-global-deploy-rename-validation**: deploy to the four homes + merge-mode orphan sweep (~/.qwen task-orchestrator WILL orphan) + presence/freshness checks. Precondition: human commit of 003.
- **004-missing-language-coders**: spec validated and frozen; blocked by 005.

<log></log>