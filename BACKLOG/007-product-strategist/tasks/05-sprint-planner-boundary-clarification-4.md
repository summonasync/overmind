---
feature: 007
task: 05
slug: product-strategist
status: done
review: light
created: 2026-09-29
depends_on: 01
brief: sprint-planner boundary clarification ×4
---

<goal>Clarify the strategy/logistics boundary in the sprint-planner skill on all four sides.</goal>
<scope>
- `.claude/skills/sprint-planner/SKILL.md` + mirrors in `.kimi-code/`, `.gemini/antigravity/`, `.qwen/` skills trees.
- Intro repositioned: sprint-planner owns the queue (order/deps/slicing/pinning) among existing features; WHAT should exist on the board and WHY is `product-strategist`'s lane (product level).
- Routing table gains a row: product vision / roadmap / gap analysis → `product-strategist`.
- Edit the claude copy, then copy to the three mirrors so all four stay byte-identical.
</scope>
<anti-scope>No other sprint-planner content changed; no product-strategist file touched.</anti-scope>
<constraints>Minimal diff — two insertions; do not renumber or rewrite existing sections.</constraints>
<acceptance-criteria>
- [x] All four sprint-planner SKILL.md state the strategy/logistics split consistently with the product-strategist skill's boundary table
- [x] Routing table contains the product-strategist row
- [x] The four mirrors remain byte-identical to each other
- [x] Diff is limited to the boundary line + routing row
</acceptance-criteria>
<refs>Task 01 (boundary table source); spec 364635bb; plan 25e90bc1.</refs>
<obstacles>None.</obstacles>

<log>
- 2026-09-29 [agent] — boundary clarification: intro line repositioned + routing row; claude copy first, then mirrors
- 2026-09-29 [agent] — boundary line + routing row in claude copy, mirrored byte-identical ×3; git diff confirms +5/-2 scope
</log>
