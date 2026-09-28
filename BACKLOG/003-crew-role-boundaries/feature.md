---
feature: 003
slug: crew-role-boundaries
status: done
priority: p1
version: 1
created: 2026-09-27
---

# Feature 003 — Crew role boundaries: feature-orchestrator rename, sprint-planner, stack drift fix

## Goal

Close the role/routing collisions found by the 2026-09-27 `.claude/` collision audit and repair the library-stack drift between the crew contract (CLAUDE.md / AGENTS.md / GEMINI.md / QWEN.md) and the coder skills. Every change lands on all four runtimes in one commit per SYNC-STRUCTURE.md (quadruple-edit rule).

## Audit findings (2026-09-27)

- **C1 (stack drift):** `dotnet-coder` SKILL.md (×4) mandates MediatR / MassTransit / FluentAssertions and ".NET 9/10+, C# 13+" while the contract pins `Mediator` (martinothamar), Wolverine, Mapperly/manual mapping, Shouldly, .NET 10+, C# 14+. Same drift suspected in `dotnet-code-reviewer`, `dotnet-init`, `maui-coder` (all mention FluentAssertions in .claude).
- **C2 (naming collision):** `task-orchestrator` is feature-level by content (board, pinned feature, spec/plan gates, dispatch over task artifacts) but task-named — agents working a board TASK misroute to it as a "task orchestrator".
- **C3 (planning routing):** `dotnet-architect` / `next-js-architect` triggers claim "plan features" — intercept feature-planning routing that belongs to the orchestrator's gated ceremony.
- **C4 (reviewer independence):** coder skills list "review" in their triggers and `dotnet-coder` carries a self-review "Post-Task Review Protocol" — conflicts with Coder → Reviewer separation.
- **C5 (model pin):** `.claude/agents/overmind-ceremony.md` has no explicit `model:` — violates the explicit-model rule for orchestration agents.
- **C6 (doc vs reality):** `overmind-ceremony` SKILL.md states "There is NO BACKLOG.md index anymore" while `BACKLOG.md` exists at repo root as a marker-delimited materialized index; a hand edit (`[~]`→`[x]`) sits in the working tree on top of the mirror.

## Decisions (user-approved 2026-09-27)

1. Rename `task-orchestrator` → `feature-orchestrator` (skill + agent + ~25 cross-references × 4 runtimes).
2. New `sprint-planner` skill + agent: board-level planning — task queue, priorities, feature deps, feature creation routing, sprint slicing. Phase-end handoff to `audit-orchestrator`.
3. `BACKLOG.md` stays as the materialized index; fix the ceremony doc instead; revert the hand edit and re-materialize.
4. Coders keep their names; role = single-task implementator. Descriptions sharpened, self-review protocol replaced by reviewer handoff.
5. Architects = Phase 1 consultants; triggers cleaned of "plan features".

## Non-goals

- No renaming of coder/reviewer skills.
- No changes to review depth rules, gate order, or the coding-challenge pipeline.
- No runtime enforcement changes (hooks, settings, permission configs).

## Refs

- Findings trace: conversation audit 2026-09-27 (this feature is its execution contract).
- `SYNC-STRUCTURE.md` — quadruple-edit contract, pre-commit checklist, count parity (22 agents + 25 skills → must become 23 + 26 on every side).

<log>
- 2026-09-27 [auto] — artifact created → inProgress
- 2026-09-27 [auto] — all tasks done + spec + plan validated → review
- 2026-09-27 [auto] — task created while in review — reopened for the new work
- 2026-09-27 [auto] — all tasks done + spec + plan validated → review
- 2026-09-27 [agent] — User accepted in chat: «коммит, пуш, поехали дальше 005» (2026-09-28). Review passed: 12/12 tasks done via gated pipeline, result artifact 54a9e4aa, SYNC-STRUCTURE checklist 12/12 green. Feature closes done; deploy follows in 005.
</log>
