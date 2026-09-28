---
feature: 003
task: 01
slug: crew-role-boundaries
status: done
review: light
created: 2026-09-27
brief: feature-orchestrator rename on master .kimi-code: git mv + frontmatter + all cross-refs
---

<goal>Rename task-orchestrator → feature-orchestrator on the master side .kimi-code: skill dir, agent file, frontmatter, and every cross-reference.</goal>
<scope>
- git mv .kimi-code/skills/task-orchestrator → .kimi-code/skills/feature-orchestrator
- git mv .kimi-code/agents/task-orchestrator.md → .kimi-code/agents/feature-orchestrator.md
- SKILL.md frontmatter: name: feature-orchestrator; description/whenToUse rewritten: «Owns the pipeline INSIDE one feature: intake → spec gate → plan gate → task breakdown → coder dispatch → review → fix loop → result. Use when the user asks to implement a feature or a complex board bug.» Body title → «# Feature Orchestrator Skill»; rest of body unchanged (audit/challenge handoffs stay).
- agents/feature-orchestrator.md: name + description («Orchestrates the feature pipeline on the Overmind board: spec/plan gates → tasks → coder dispatch → review»), self-reference string and path ~/.kimi-code/skills/feature-orchestrator/SKILL.md.
- .kimi-code/SKILLFLOW.md: rows 0.0/0.1/0.2/Tasks — owner → feature-orchestrator.
- .kimi-code/skills/overmind-ceremony/SKILL.md: whenToUse sentence + «Relationship to Other Skills» section → feature-orchestrator.
- next-step/routing lines in 10 skills: dotnet-coder, dotnet-init, dotnet-architect, next-js-architect, next-js-coder, maui-coder, documentation-writer, fast-fixer-coder, audit-orchestrator, coding-challenge-orchestrator.
- .kimi-code/AGENTS.md: pipeline line («This file and task-orchestrator define the pipeline») → feature-orchestrator.
- .kimi-code/agents/overmind-ceremony.md: subagents list entry → feature-orchestrator.
- SYNC-STRUCTURE.md (repo root): 2 file-mapping rows + frontmatter-affected set mention → feature-orchestrator.
- manifest.json (repo root): rename the skill-dir entry and agent entry (keep mode fields).
</scope>
<anti-scope>No content changes beyond naming/description; NO edits under .claude, .gemini, .qwen (that is T2); no new skills; no edits under BACKLOG/ or audits/.</anti-scope>
<constraints>git mv only, no copies; NO git commit; English-only file text; keep kimi-side wording conventions.</constraints>
<acceptance-criteria>
- [x] grep -rn "task-orchestrator" .kimi-code/ manifest.json SYNC-STRUCTURE.md → 0 hits
- [x] .kimi-code/skills/feature-orchestrator/SKILL.md and .kimi-code/agents/feature-orchestrator.md exist; old paths gone
- [x] SKILLFLOW.md main-route table names feature-orchestrator in all 4 planning rows
- [x] manifest.json parses as valid JSON
</acceptance-criteria>
<refs>Spec f0fdb479 §1; Plan 0f42f5fa T1. Port side depends on this task (T2).</refs>
<obstacles>
- manifest.json quirk: scope said to "rename the skill-dir entry and agent entry", but manifest v2 mirrors whole directories only (`".kimi-code/agents"` and `".kimi-code/skills"` carry the mode fields) — there are no per-skill/per-agent entries and the string "task-orchestrator" never appeared in the file. The git mv inside those directories requires no manifest edit; file left untouched, still valid JSON (python3 -m json.tool).
- SYNC-STRUCTURE.md file-mapping rows now reference feature-orchestrator paths on all four sides (.kimi-code/.claude/.gemini/.qwen), but this task's scope covers only the .kimi-code side — the other three sides keep their old paths on disk until T2 runs.
</obstacles>
<log></log>

<log>
- 2026-09-27 [agent] — взята в работу исполнителем
- 2026-09-27 [agent] — added obstacles section: manifest.json has no per-skill entries (dir-level mirror), no edit needed; SYNC-STRUCTURE rows reference not-yet-ported sides per anti-scope
- 2026-09-27 [agent] — Renamed task-orchestrator → feature-orchestrator on the .kimi-code side: git mv of skills/task-orchestrator/ and agents/task-orchestrator.md; SKILL.md frontmatter (name, description/whenToUse per scope wording) + body title; agent file name/description/self-reference path; 27 cross-reference replacements across SKILLFLOW.md (rows 0.0/0.1/0.2/Tasks), AGENTS.md, agents/overmind-ceremony.md, skills/overmind-ceremony, and 10 routing skills (dotnet-coder, dotnet-init, dotnet-architect, next-js-architect, next-js-coder, maui-coder, documentation-writer, fast-fixer-coder, audit-orchestrator, coding-challenge-orchestrator), SYNC-STRUCTURE.md (3 lines). manifest.json untouched — dir-level entries only, no per-skill rows (see obstacles). Verification: grep -rn "task-orchestrator" .kimi-code/ manifest.json SYNC-STRUCTURE.md → 0 hits; new paths exist, old gone; SKILLFLOW all 4 planning rows name feature-orchestrator; manifest.json valid JSON. 17 files changed, no commits.
- 2026-09-27 [agent] — scope executed, all 4 AC checks green (grep 0 hits, paths renamed, SKILLFLOW 4 rows, manifest valid JSON) — moved to review
- 2026-09-27 [agent] — Orchestrator review passed: grep task-orchestrator = 0, new paths present / old gone, SKILLFLOW rows 21-24 updated, manifest valid JSON. Spec-compliance + quality OK. Both obstacles recorded (manifest v2 mirrors dirs — no entry edit needed; port sides pending T2).
</log>
