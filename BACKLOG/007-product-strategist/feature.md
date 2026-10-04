---
feature: 007
slug: product-strategist
status: done
priority: p1
version: 2
created: 2026-09-29
---

# Feature 007 — Product strategist role: product-level planning crew skill

## Goal

The crew has executors (coders), reviewers, architects, a delivery planner (`sprint-planner`) and an inside-feature pipeline owner (`feature-orchestrator`) — but nobody owns the PRODUCT level: where the product is heading, what is missing, what should be killed or redone. The user currently plays CPO in their head. This feature adds a `product-strategist` crew role (skill + agent) that closes the CEO/CPO lane: product state analysis, goal-setting dialogue, gap analysis, roadmap — materialized directly on the board.

## Requirements

- New skill `.claude/skills/product-strategist/SKILL.md` + agent `.claude/agents/product-strategist.md`, mirrored across all four runtimes per SYNC-STRUCTURE.md.
- Role scope: (1) product state analysis — board (features/statuses), project docs, `audits/`; (2) goal & direction dialogue with the user, durable outcomes stored as overmind entries (note = vision/state snapshot, decision = direction choice); (3) gap analysis — missing / stale / rework candidates; (4) roadmap → board: creates features with durable what/why, proposes priority bands (cross-band moves still require explicit user approval).
- Board-write rights: the role creates features and proposes priorities itself; queue mechanics (in-band ordering, dependencies, sprint slicing, pinning) stay with `sprint-planner`.
- Boundaries: never codes, never reviews, never touches task artifacts, never works a feature's inner pipeline, never duplicates ceremony mechanics (defers to `overmind-ceremony`).
- SKILLFLOW.md gets a product-strategy alternative route; `sprint-planner` skill gets a boundary clarification (strategy vs logistics split); README roster updated (role list + agent/skill counts reflecting actual numbers — counts are stale: README says 22/24, actual is 34 agents / 42 skills).

## Non-goals

- No queue mechanics: in-band ordering, dependencies, sprint/phase slicing remain `sprint-planner`'s.
- No spec/plan/task work inside features (`feature-orchestrator`'s lane).
- No code, no code reviews, no system audits (`audit-orchestrator`'s lane).
- No new tooling — research uses WebSearch the runtimes already expose.

<log>
- 2026-09-29 [auto] — artifact created → inProgress
- 2026-09-29 [auto] — all tasks done + spec + plan validated → review
- 2026-09-29 [ui] — status moved to 'done' via ui
</log>
