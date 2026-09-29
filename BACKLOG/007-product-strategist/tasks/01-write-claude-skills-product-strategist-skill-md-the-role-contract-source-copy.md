---
feature: 007
task: 01
slug: product-strategist
status: done
review: light
created: 2026-09-29
brief: Write .claude/skills/product-strategist/SKILL.md — the role contract (source copy)
---

<goal>Create `.claude/skills/product-strategist/SKILL.md` — the product-strategist role contract, single source of truth for the role (Claude side is the family's canonical copy).</goal>
<scope>
- Exactly one new file: `.claude/skills/product-strategist/SKILL.md`.
- Frontmatter: `name: product-strategist`, `type: prompt`, block-scalar `description` and `whenToUse` (multi-line `|` scalars).
- Body in the sprint-planner family order: `# Product Strategist Skill` intro (CEO/CPO lane; user owns the vision; sprint-planner = logistics, feature-orchestrator = inside-feature), `## Hard Requirement (start here)` (overmind_onboard first, STOP without MCP, defer mechanics to overmind-ceremony, STOP lines authoritative), `## Scope (product-level planning ONLY)` with 4 numbered responsibilities: 1 product state analysis (board + docs + audits, optional dated snapshot as tagged note entry, entries are DB-only via MCP), 2 goals & direction (Socratic, ONE question at a time; durable outcomes as overmind entries: `decision` for direction choice + rationale, `note` for vision/state snapshot, tags like `product-vision`/`product-roadmap`), 3 gap analysis (missing / stale / rework / over-engineering to cut; structured list with rationale; NO board writes from analysis alone), 4 roadmap → board (`planning_create_feature` with durable what/why, `NNN-slug` numbering from board sequence; in-band moves may be proposed freely; ACROSS bands `planning_set_feature_priority` ONLY on explicit in-session user approval and the feature re-enters at the band TAIL — say so before moving; messy raw strategy inputs → `feature-orchestrator` intake, not a feature), then `## Board rights vs sprint-planner` (owns / proposes / never table), `## Anti-Scope (ABSOLUTE — checked before any board write)` with refusal pattern «Это не моё: <why>. Роутинг: <skill/agent>.» and routing table (queue/sprint slicing → sprint-planner; implement/spec/plan/tasks/review → feature-orchestrator; messy raw requirements → feature-orchestrator intake; tech strategy → solution-architect; audits/retro → audit-orchestrator), `## Workflow` (7 steps: onboard → read ceremony+SKILLFLOW → state → goals → gaps → roadmap+create → report+handoff+output contract), `## Red Flags — STOP and hand off`, `## Communication` (Russian to the user, English in board artifacts, band-crossing questions are explicit questions, user owns the vision — challenge never override, output contract block).
</scope>
<anti-scope>No mirrors (tasks 02–03), no SKILLFLOW/sprint-planner/README edits, no code, no other formats.</anti-scope>
<constraints>Family format = sprint-planner SKILL.md structure and tone; MCP tool names verbatim; no corporate phrases.</constraints>
<acceptance-criteria>
- [x] File exists at `.claude/skills/product-strategist/SKILL.md` with frontmatter `name: product-strategist`, `type: prompt`
- [x] `description` and `whenToUse` are block scalars; no unquoted single-line YAML value contains `: `
- [x] All family sections present: Hard Requirement / Scope with 4 responsibilities / Board rights table / Anti-Scope + routing table / Workflow / Red Flags / Communication
- [x] Communication section contains the output contract (Done / Next step / Alternatives, then STOP)
- [x] Boundary table matches spec: owns (feature creation, band proposals, entries), proposes (cross-band moves), never (queue mechanics, task artifacts, code/review/audit, ceremony mechanics)
</acceptance-criteria>
<refs>Spec artifact 364635bb-6bcb-453b-b62a-b684cae28673; plan 25e90bc1-8917-4cbe-8631-07dead1a7353; format reference: .claude/skills/sprint-planner/SKILL.md; boundary source: feature body + spec «Board rights and boundaries».</refs>
<obstacles>None.</obstacles>

<log>
- 2026-09-29 [agent] — picked up by main orchestrator — markdown crew-file work, no coder lane applies (docs-style task)
- 2026-09-29 [agent] — SKILL.md written and verified: frontmatter ok, 7 family sections, output contract, boundary table; all 5 AC ticked
</log>
