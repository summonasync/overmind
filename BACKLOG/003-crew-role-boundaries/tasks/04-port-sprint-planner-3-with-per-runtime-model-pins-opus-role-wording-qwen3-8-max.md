---
feature: 003
task: 04
slug: crew-role-boundaries
status: done
review: light
created: 2026-09-27
brief: Port sprint-planner ×3 with per-runtime model pins (opus / role wording / qwen3.8-max)
---

<goal>Port sprint-planner to .claude, .gemini/antigravity, .qwen with per-runtime conventions.</goal>
<scope>.claude: skills/sprint-planner/SKILL.md (same body, paths ~/.claude/...) + agents/sprint-planner.md with model: opus. .gemini/antigravity: same, keep whenToUse key, role-based model wording, no model pin. .qwen: same, model: qwen3.8-max, canonical Qwen tool names if a tools list is present, whenToUse folded into description with YAML quoting. Registration (manifest/SYNC-STRUCTURE/SKILLFLOW) was done in T3 — port files only.</scope>
<anti-scope>No .kimi-code edits; no manifest/SYNC-STRUCTURE changes.</anti-scope>
<constraints>Translation tables; frontmatter YAML validity on every side; NO git commit.</constraints>
<acceptance-criteria>
- [x] Count parity 23 agents + 26 skills on .claude, .gemini/antigravity, .qwen
- [x] grep -rnE '^(description|whenToUse): [^"|&gt;-].*: ' across all four agents dirs → empty
- [x] Port bodies differ from master only in expected translation deltas (spot-check diff)
</acceptance-criteria>
<refs>Spec f0fdb479 §2; Plan T4; blocked by T3 (master output).</refs>
<obstacles></obstacles>
<log></log>

<log>
- 2026-09-27 [agent] — взята в работу исполнителем
- 2026-09-27 [agent] — Port done, 6 files. Skills: .claude/skills/sprint-planner/SKILL.md, .gemini/antigravity/skills/sprint-planner/SKILL.md, .qwen/skills/sprint-planner/SKILL.md — verbatim copies of master (master body contains no ~/.kimi-code self-path, so no path translation was needed); diff vs master = empty. Agents: .claude/agents/sprint-planner.md (model: opus, folded quoted description), .qwen/agents/sprint-planner.md (model: qwen3.8-max, same shape), .gemini/antigravity/agents/sprint-planner.md (no model pin; description = role only per gemini sibling convention, trigger moved into whenToUse; subagents: feature-orchestrator, audit-orchestrator — the two handoff targets from the skill's routing table, matching the gemini orchestrator convention). Verification: counts 23 agents + 26 skills on all four sides (parity); YAML grep '^(description|whenToUse): [^"|>-].*: ' across all four agents dirs = empty; frontmatter of all 6 files parses as YAML with key sets matching their siblings; agent body diffs vs master show only the skill-path line plus frontmatter adaptation. No manifest.json / SYNC-STRUCTURE.md edits, no git actions.
- 2026-09-27 [agent] — исполнение завершено, все 3 AC проверены и затиканы — передаю на review
- 2026-09-27 [agent] — Orchestrator review passed: parity 23/26 on all four sides, YAML grep empty, port SKILL.md byte-identical to master, agent frontmatter verified (claude opus / qwen qwen3.8-max / gemini role wording + subagents handoff targets). Runtime live-confirms sprint-planner visible.
</log>
