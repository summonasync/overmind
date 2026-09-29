---
feature: 007
task: 03
slug: product-strategist
status: done
review: light
created: 2026-09-29
depends_on: 01
brief: Agent definitions ×4 with runtime-correct frontmatter
---

<goal>Create the product-strategist agent definition for all four runtimes with runtime-correct frontmatter.</goal>
<scope>
- `.claude/agents/product-strategist.md`: `model: opus`; skill path `~/.claude/skills/product-strategist/SKILL.md`.
- `.kimi-code/agents/product-strategist.md`: `whenToUse:` field (no model); path `~/.kimi-code/...`.
- `.qwen/agents/product-strategist.md`: `model:` pin identical to `.qwen/agents/sprint-planner.md`'s pin; path `~/.qwen/...`.
- `.gemini/antigravity/agents/product-strategist.md`: mirror the format of `.gemini/antigravity/agents/sprint-planner.md` (read it first); path `~/.gemini/antigravity/...`.
- Body follows the sibling agent's shape (read your skill file first / inherited rules / final-message handoff).
- Single-line description mentions the product planning lane; double-quote it (SYNC-STRUCTURE rule — quoted form is what sprint-planner uses on every side).
</scope>
<anti-scope>No skill files, no SKILLFLOW, no README.</anti-scope>
<constraints>Per-runtime deltas limited to: model field vs whenToUse field, home path. Everything else mirrors the sibling agent per runtime.</constraints>
<acceptance-criteria>
- [x] 4 agent files exist at the exact paths above
- [x] claude: `model: opus`; kimi: `whenToUse` present, no model; qwen: model pin equals sprint-planner's qwen pin; gemini: fields match its sprint-planner agent format
- [x] Each references its own runtime home skill path
- [x] Frontmatter diff vs the sibling sprint-planner agent per runtime shows ONLY role-name/description/whenToUse deltas
- [x] Any single-line description containing `: ` is double-quoted
</acceptance-criteria>
<refs>Task 01 (skill the agents point at); spec 364635bb; SYNC-STRUCTURE.md frontmatter convention.</refs>
<obstacles>None.</obstacles>

<log>
- 2026-09-29 [agent] — writing 4 agent files; sibling formats inspected (gemini uses whenToUse+subagents, qwen pin qwen3.8-flash)
- 2026-09-29 [agent] — 4 agents written; frontmatter diffs vs siblings show only role deltas; gemini subagents = role-specific handoffs (sprint-planner, feature-orchestrator)
</log>
