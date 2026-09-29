---
feature: 007
slug: product-strategist
status: validated
created: 2026-09-29
validated: 2026-09-29
---

# Spec 007 — Product strategist role: product-level planning crew skill

## Problem

The crew covers delivery top-to-bottom EXCEPT the product level. `sprint-planner` is logistics over what is already on the board (queue order, bands, dependencies, sprints). `feature-orchestrator` owns one feature's pipeline. `solution-architect` owns technical strategy. Nobody owns: WHERE the product is heading, WHAT is missing, WHAT should be killed or redone, and turning that into board features. Today the user plays CPO in their head — not scalable, not durable, not visible to agents.

## Role summary

`product-strategist` — the CEO/CPO lane of the crew. A planning role, sibling of `sprint-planner`. Takes a product (any project with an overmind board, including this crew repo itself), figures out where it stands against the user's goals, and materializes the direction as board features and priority proposals.

## Responsibilities

### 1. Product state analysis
- Inputs: overmind board (`planning_list_features` full board, per-feature reads), project docs (CONTEXT.md, TECHNICAL-SOLUTIONS.md, README, STRUCTURE.md where present), `audits/` reports, repo structure.
- Output: a compact state picture — what the product does today, done/in-flight/draft/stale features, doc-vs-reality drift. Presented in chat; a dated snapshot MAY be persisted as a tagged `note` entry when the user wants it kept.

### 2. Goals & direction dialogue
- Socratic questioning (technique from `overmind-ceremony`): the user owns the vision; the strategist structures it, challenges it, resolves contradictions. ONE question at a time.
- Durable storage: overmind entries — `decision` (direction choice + rationale) and `note` (vision/state snapshot), tagged per product (e.g. `product-vision`, `product-roadmap`). Entries are DB-only (disk materialization not shipped, board ref 051) — read via MCP, never hand-written files.

### 3. Gap analysis
- Goals vs state: missing capabilities, stale/abandoned features, rework candidates, over-engineering worth cutting.
- Output: structured gap list with rationale, presented to the user. No board writes from this step alone.

### 4. Roadmap → board
- Roadmap = ordered initiatives with value/effort rationale.
- Materialization: `planning_create_feature` per initiative (durable what/why in the body, `NNN-slug` numbering from the board sequence) and priority band proposals. Cross-band moves require explicit in-session user approval — same guardrail as `sprint-planner`.
- Messy raw strategy inputs → route to `feature-orchestrator` (intake) instead of creating a feature with an unclear what/why.

## Board rights and boundaries

| Owns | Proposes (user approves) | Never touches |
|---|---|---|
| Feature creation from roadmap initiatives | Cross-band priority moves | Queue mechanics: in-band order, dependencies, sprint/phase slicing, pinning (`sprint-planner`) |
| Priority band proposals for new/existing features | Feature deletion is UI-only — never promised | Task artifacts, spec/plan gates, coder dispatch, review (`feature-orchestrator`) |
| Vision/goals/roadmap as overmind entries | | Code, code review, system audits (`audit-orchestrator`) |
| | | Ceremony mechanics (`overmind-ceremony` defers TO it, not from it) |

Refusal = handoff, same pattern as `sprint-planner`: «Это не моё: <why>. Роутинг: <skill/agent>».

## Deliverables (file-level)

1. `.claude/skills/product-strategist/SKILL.md` — full role contract in the `sprint-planner` family format: Hard Requirement (overmind onboard, STOP without MCP), Scope (the 4 responsibilities with tool names), Anti-Scope (routing table), Workflow, Red Flags, Communication (Russian to user / English in artifacts / output contract).
2. Skill mirrored to `.kimi-code/skills/product-strategist/SKILL.md`, `.gemini/antigravity/skills/product-strategist/SKILL.md`, `.qwen/skills/product-strategist/SKILL.md` (skill bodies are identical across runtimes — verified: sprint-planner diff is empty).
3. `.claude/agents/product-strategist.md` with `model: opus` (orchestration lane, matches `sprint-planner`); mirrors: kimi (`whenToUse:` field, `~/.kimi-code/` path), qwen (`model:` pin matching the sibling sprint-planner qwen pin, `~/.qwen/` path), gemini (matching the sibling sprint-planner agent format there).
4. SKILLFLOW.md on all four sides: new Alternative Route «Product strategy» — `product-strategist` upstream of `sprint-planner` (strategy decides WHAT/WHY enters the board; logistics keeps order/deps/sprints).
5. `sprint-planner/SKILL.md` on all four sides: boundary clarification — strategy (features, priority proposals, vision entries) vs logistics (queue mechanics). No contradictions between the two skill files.
6. README.md: roster mentions `product-strategist`; agent/skill counts corrected to actual directory counts (currently stale: README says 22 agents / 24 skills, actual is 34 / 42).

## Explicit decisions (user-confirmed 2026-09-29)

- Name: `product-strategist` (fits the `-planner`/`-orchestrator`/`-architect` family).
- Board rights: the role writes itself (creates features, proposes bands); NOT advisory-only.
- Goals/vision storage: overmind entries, not a separate markdown file.
- Agent model on Claude side: `opus`.

## Out of scope

- No queue mechanics, no inner-feature pipeline work, no code/reviews/audits.
- No new MCP tooling or entries materialization (board refs 051/052 stay as-is).
- No market/competitor research infrastructure — WebSearch the runtimes already expose is enough.
- No changes to `feature-orchestrator` / `overmind-ceremony` / `audit-orchestrator` skill files (their deferral statements already hold).

## Acceptance criteria

- [ ] `.claude/skills/product-strategist/SKILL.md` exists: frontmatter `name: product-strategist`, block-scalar `description`, `type: prompt`, `whenToUse`; body sections Hard Requirement / Scope / Anti-Scope + routing table / Workflow / Red Flags / Communication with the output contract.
- [ ] Skill body is semantically identical in all four runtime skill dirs (kimi/claude identical; qwen/gemini frontmatter per open-SKILL standard, as sprint-planner does).
- [ ] Agent file exists in all four agents dirs with runtime-correct fields: claude `model: opus`; kimi `whenToUse`; qwen model pin equals sprint-planner's qwen pin; gemini matches its sprint-planner agent format; each references its own home skill path.
- [ ] SKILLFLOW.md ×4 contains the «Product strategy» alternative route naming `product-strategist`, its upstream position vs `sprint-planner`, and handoffs.
- [ ] `sprint-planner/SKILL.md` ×4 states the strategy/logistics split consistently with the new skill's boundary table.
- [ ] README.md roster names `product-strategist`; stated agent/skill counts equal actual counts of `.claude/agents/*.md` and `.claude/skills/*` directories.
- [ ] Frontmatter YAML-safety per SYNC-STRUCTURE: single-line description values containing `: ` are double-quoted.
- [ ] No edits outside the deliverable list (verified by `git status` / `git diff --stat` at review).

<log>
- 2026-09-29 [agent] spec drafted from user-confirmed decisions (name, board-write rights, entries storage, opus lane)
- 2026-09-29 [agent] — validated with human proof: notification 6411e94b-7a53-4cfd-a3e3-6403c8629584 replied 2026-09-29 14:17:26 UTC
</log>