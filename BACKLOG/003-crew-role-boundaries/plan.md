---
feature: 003
slug: crew-role-boundaries
status: validated
created: 2026-09-27
validated: 2026-09-27
---

# Plan 003 — Crew role boundaries: feature-orchestrator rename, sprint-planner, stack drift fix

Master side: `.kimi-code` (canonical per SYNC-STRUCTURE translation direction). Master task → port task ×3 (`.claude`, `.gemini/antigravity`, `.qwen`). File-grouped: a port never starts before its master lands. Model pins per side convention: kimi = config.toml pool (no per-agent pin), claude = `model:` field, qwen = `model:` pin, antigravity = role-based wording.

## T1 Rename — master (.kimi-code)
`git mv skills/task-orchestrator skills/feature-orchestrator`; `git mv agents/task-orchestrator.md agents/feature-orchestrator.md`. Rewrite frontmatter (name/description/whenToUse → feature-scoped trigger) + body title; keep audit/challenge handoffs. Update: SKILLFLOW.md rows 0.0/0.1/0.2/Tasks; overmind-ceremony SKILL.md (whenToUse sentence + Relationship section); cross-ref next-step lines in dotnet-coder, dotnet-init, dotnet-architect, next-js-architect, next-js-coder, maui-coder, documentation-writer, fast-fixer-coder, audit-orchestrator, coding-challenge-orchestrator; AGENTS.md pipeline line; `agents/overmind-ceremony.md` subagents list; SYNC-STRUCTURE.md (2 mapping rows + frontmatter-affected set); manifest.json entries.
Verify: `grep -rn "task-orchestrator" .kimi-code ../manifest.json ../SYNC-STRUCTURE.md` → 0 hits.

## T2 Port rename ×3
Same file set per side: skill dir + agent, SKILLFLOW.md, overmind-ceremony SKILL.md, 10 skill cross-refs, contract file (CLAUDE.md / GEMINI.md / QWEN.md), overmind-ceremony agent (claude: no subagents key — description only; gemini: subagents list), manifest-independent (manifest is root-level, done in T1).
Verify: per-side grep → 0 hits.

## T3 sprint-planner — master (.kimi-code)
New `skills/sprint-planner/SKILL.md` (scope: `planning_task_queue` triage, band moves via `planning_move_feature`, deps via `planning_add/remove_feature_dependency`, feature creation + intake routing, sprint slicing, phase-end handoff → audit-orchestrator; anti-scope: no feature pipeline, no code, no review; mechanics defer to overmind-ceremony; output contract) + `agents/sprint-planner.md` (kimi shape, no model pin) + manifest.json entries + SYNC-STRUCTURE.md mapping rows + count-parity line 22/25→23/26 + SKILLFLOW.md route «Board planning (sprint)».
Verify: `ls .kimi-code/agents | wc -l` = 23, skills = 26.

## T4 Port sprint-planner ×3
.claude: agent `model: opus`. .gemini/antigravity: whenToUse key kept, role wording. .qwen: `model: qwen3.8-max`, canonical tool names, whenToUse folded into description (frontmatter YAML rules!).
Verify: count parity 23/26 on all four sides; YAML grep from SYNC-STRUCTURE checklist #7 empty.

## T5 Stack drift — master (.kimi-code)
dotnet-coder SKILL.md: CQRS → Mediator (martinothamar); Messaging → Wolverine (MassTransit legacy-only); Testing → Shouldly; mapping → Mapperly/manual; .NET 10+ / C# 14+; description + architecture-rules lines reworded. Sweep: dotnet-code-reviewer (banned libs = violations in NEW code, legacy allowed), dotnet-init, maui-coder, dotnet-best-practices (BP-IDs).
Verify: `grep -n "MediatR\|MassTransit\|FluentAssertions\|AutoMapper" .kimi-code/skills/dotnet-coder/SKILL.md` → only legacy-clause lines.

## T6 Port stack drift ×3
Verify: same grep per side.

## T7 Role sharpening + architect cleanup — master (.kimi-code)
dotnet-coder / next-js-coder / maui-coder: drop "review" from frontmatter description + whenToUse; dotnet-coder «Post-Task Review Protocol» → reviewer-handoff rule (Complex → separate reviewer agent via feature-orchestrator Phase 3); check the other two coders for self-review sections. dotnet-architect + next-js-architect: strip «plan features» from description/whenToUse, add «consulted BY feature-orchestrator (Phase 1)», fix next-step lines.
Verify: `grep -in "review" .kimi-code/skills/dotnet-coder/SKILL.md` shows no self-review protocol; architect frontmatter has no planning claims.

## T8 Port role sharpening ×3 + model pins
Same edits per side; plus `.claude/agents/overmind-ceremony.md` add `model: opus`; `.qwen/agents/overmind-ceremony.md` add `model: qwen3.8-max`; gemini/kimi — verify convention, document gap if not applicable.
Verify: `grep -L '^model:' .claude/agents/*.md` → empty.

## T9 BACKLOG.md reconciliation ×4
overmind-ceremony SKILL.md ×4: replace «There is NO BACKLOG.md index anymore» with materialized-index rule. Working tree: revert hand edits in `BACKLOG.md` + `BACKLOG/002-superpowers-gap-closure/feature.md`, then `planning_materialize` → clean pass.
Verify: materialize report = no refusals, no drift.

## T10 Full pre-commit checklist (SYNC-STRUCTURE)
5 contamination greps; count parity 23/26 ×4; YAML frontmatter grep #7; `install.sh --dry-run` + `uninstall.sh --dry-run`; repo-wide `grep -rn "task-orchestrator"` excluding audits/ and BACKLOG/ → 0. Report every result.

<log></log>

<log>
- 2026-09-27 [agent] — validated with human proof: notification ee46122f-6a2a-4776-939a-d831a2007206 replied 2026-09-27 19:59:45 UTC
</log>
