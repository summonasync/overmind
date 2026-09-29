---
feature: 007
slug: product-strategist
status: validated
created: 2026-09-29
validated: 2026-09-29
---

# Plan 007 — Product strategist role: product-level planning crew skill

> Spec: artifact `364635bb-6bcb-453b-b62a-b684cae28673` (validated, frozen).

Decomposition: 7 tasks, markdown-only (no code, no build steps). Task 01 is the source
contract; 02–06 depend on it as reference; 07 sweeps everything. 02–06 touch disjoint file
sets and may run in parallel per `overmind-ceremony` dispatch rules once 01 lands.

## Task 01 — Source skill contract (Claude side)
- Files: `.claude/skills/product-strategist/SKILL.md` (new)
- Content: full role contract per spec, in the `sprint-planner` family format — Hard
  Requirement (overmind onboard / STOP), Scope (4 responsibilities with MCP tool names),
  Anti-Scope + routing table, Workflow, Red Flags, Communication (Russian to user, English
  in artifacts, output contract).
- Verify: frontmatter (`name: product-strategist`, `type: prompt`, block-scalar
  `description`, `whenToUse`); all family-format section headers present via grep; no
  unquoted single-line YAML values containing `: `.

## Task 02 — Skill mirrors ×3
- Files: `.kimi-code/skills/product-strategist/SKILL.md`,
  `.gemini/antigravity/skills/product-strategist/SKILL.md`,
  `.qwen/skills/product-strategist/SKILL.md`
- Content: byte-identical copies of 01 (skill bodies are runtime-identical — verified on
  sprint-planner, diff empty).
- Verify: `diff` each mirror against 01 → empty.

## Task 03 — Agent files ×4
- Files: `.claude/agents/product-strategist.md` (`model: opus`, `~/.claude` path),
  `.kimi-code/agents/product-strategist.md` (`whenToUse`, `~/.kimi-code` path),
  `.qwen/agents/product-strategist.md` (`model:` pin = sprint-planner's qwen pin,
  `~/.qwen` path), `.gemini/antigravity/agents/product-strategist.md` (sibling format,
  `~/.gemini/antigravity` path).
- Content: mirror the sibling sprint-planner agent per runtime, adapted to
  product-strategist.
- Verify: frontmatter diff vs sibling sprint-planner agent per runtime shows ONLY
  role-name/description/path deltas; `: `-containing description values double-quoted.

## Task 04 — SKILLFLOW route ×4
- Files: `.claude/SKILLFLOW.md`, `.kimi-code/SKILLFLOW.md`,
  `.gemini/antigravity/SKILLFLOW.md`, `.qwen/SKILLFLOW.md`
- Content: Alternative Routes entry «Product strategy» — `product-strategist` upstream of
  `sprint-planner`: strategy decides WHAT/WHY enters the board (features, priority
  proposals, vision entries); logistics (in-band order, dependencies, sprint slicing,
  pinning) stays with sprint-planner; handoffs to sprint-planner / feature-orchestrator.
- Verify: `grep product-strategist` hits all four files with identical route text.

## Task 05 — sprint-planner boundary ×4
- Files: `.claude/skills/sprint-planner/SKILL.md` + 3 mirrors
- Content: boundary clarification in Scope — strategy vs logistics split; routing row for
  product vision / roadmap / gap analysis → `product-strategist`.
- Verify: mirrors byte-identical to claude side; no contradiction between the two skills'
  boundary statements.

## Task 06 — README roster
- Files: `README.md`
- Content: roster mentions `product-strategist`; agent/skill counts corrected to actual
  directory counts (computed at execution time).
- Verify: README counts == `ls .claude/agents/*.md | wc -l` and `ls -d .claude/skills/*/ | wc -l`.

## Task 07 — Validation sweep
- No new files.
- Content: SYNC-STRUCTURE checklist pass over the whole diff — four-side semantic identity,
  frontmatter YAML-safety, no stray files, counts re-check.
- Verify: `git diff --stat` matches the deliverable file list exactly; all greps green.

## Sequencing

`01 → (02, 03, 04, 05, 06 — parallel allowed, disjoint files) → 07`.

All tasks are markdown/docs — `review: light`.

<log>
- 2026-09-29 [agent] plan drafted after spec validation; 7 tasks, sequencing and parallel-dispatch note included
- 2026-09-29 [agent] — validated with human proof: notification ffcad0e9-5e40-4945-a4ea-12fbe1de911d replied 2026-09-29 14:20:07 UTC
</log>