---
feature: 007
task: 04
slug: product-strategist
status: done
review: light
created: 2026-09-29
depends_on: 01
brief: SKILLFLOW «Product strategy» route ×4
---

<goal>Add the «Product strategy» route to SKILLFLOW.md on all four sides.</goal>
<scope>
- `.claude/SKILLFLOW.md`, `.kimi-code/SKILLFLOW.md`, `.gemini/antigravity/SKILLFLOW.md`, `.qwen/SKILLFLOW.md`.
- New Alternative Routes block «Product strategy (product-level planning):» — same style as the existing «Board planning (sprint)» block: product-strategist, product-level ONLY (state analysis, goal dialogue → overmind entries, gap analysis, roadmap → `planning_create_feature` + priority proposals, cross-band on explicit user approval); upstream of sprint-planner (strategy decides WHAT and WHY enters the board; logistics keeps order, dependencies and sprint slicing); handoffs to sprint-planner (queue) and feature-orchestrator (execution).
- Identical text on all four sides (SKILLFLOW has no runtime deltas).
</scope>
<anti-scope>No edits to other routes; no Main Route changes; no other files.</anti-scope>
<constraints>Route text byte-identical across the four files; placed next to the sprint-planner alternative route.</constraints>
<acceptance-criteria>
- [x] All four SKILLFLOW.md contain the Product strategy route naming product-strategist
- [x] Route text identical on all four sides (diff of the added block across sides is empty)
- [x] No other route text changed
</acceptance-criteria>
<refs>Task 01 (role definition); spec 364635bb; .claude/SKILLFLOW.md Alternative Routes section.</refs>
<obstacles>None.</obstacles>

<log>
- 2026-09-29 [agent] — adding Product strategy route to 4 SKILLFLOW files; sides edited separately (model-name lines differ), added block identical
- 2026-09-29 [agent] — route added to all 4 sides, added block md5-identical (7cead075), no other route text touched
</log>
