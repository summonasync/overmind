---
feature: 003
task: 02
slug: crew-role-boundaries
status: done
review: light
created: 2026-09-27
brief: Port rename ×3: .claude, .gemini/antigravity, .qwen
---

<goal>Port the feature-orchestrator rename from .kimi-code to .claude, .gemini/antigravity, .qwen.</goal>
<scope>Per side: git mv skills/task-orchestrator → skills/feature-orchestrator; git mv agents/task-orchestrator.md → agents/feature-orchestrator.md; same frontmatter/description rewrite as T1, runtime-appropriate: .claude single-line quoted description; .gemini keeps whenToUse key; .qwen folds whenToUse into description (frontmatter YAML quoting rule). Then: SKILLFLOW.md rows ×3; overmind-ceremony SKILL.md ×3 (whenToUse sentence + Relationship section); 10 skill cross-ref lines ×3 (same list as T1); contract files CLAUDE.md / GEMINI.md / QWEN.md pipeline line; agents/overmind-ceremony.md on .gemini (subagents list entry) — .claude and .qwen overmind-ceremony agents have no subagents key.</scope>
<anti-scope>No .kimi-code edits; no new skills; no BACKLOG/ or audits/ edits.</anti-scope>
<constraints>Follow SYNC-STRUCTURE translation tables (Kimi→Claude, Kimi→Antigravity, Kimi→Qwen); Qwen YAML rule for values containing «: »; NO git commit.</constraints>
<acceptance-criteria>
- [x] grep -rn "task-orchestrator" .claude .gemini .qwen → 0 hits
- [x] feature-orchestrator skill + agent exist on all three sides
- [x] SYNC-STRUCTURE checklist #7 YAML grep stays empty
</acceptance-criteria>
<refs>Spec f0fdb479 §1; Plan T2; blocked by T1 (master output).</refs>
<obstacles></obstacles>
<log></log>

<log>
- 2026-09-27 [agent] — взята в работу исполнителем
- 2026-09-27 [agent] — Port complete on all three sides (.claude, .gemini/antigravity, .qwen): git mv skill dir + agent file (6 renames); SKILL.md = master T1 copy + runtime obfuscation word restored ("via Opus" / "via strongest reasoning model" / "via qwen3.8-max", master's "via K3" not carried); agent frontmatter rewritten with master wording — .claude/.qwen fold whenToUse into quoted single-line description (runtime convention, style ref fast-fixer-coder.md; no separate whenToUse key exists on those sides — brief's "whenToUse also single-line quoted" implemented as the fold), .gemini keeps separate whenToUse + subagents list; model pins opus/qwen3.8-max untouched. Mechanical replace in 43 files: SKILLFLOW rows 0.0/0.1/0.2/Tasks ×3, overmind-ceremony SKILL.md lines 12+173 ×3, 10 skill cross-ref files ×3, contract pipeline line CLAUDE.md/GEMINI.md/QWEN.md, .gemini agents/overmind-ceremony.md subagents entry. .claude/.qwen overmind-ceremony agents verified to have no subagents key — skip confirmed. Verification: grep -rn "task-orchestrator" .claude .gemini .qwen → 0 hits; skill+agent exist on all three sides; SYNC-STRUCTURE #7 YAML grep empty; count parity 22 agents / 25 skills on all four sides; leakage grep on new SKILL.md dirs clean; port SKILL.md vs master diff = model word only. No .kimi-code/manifest/SYNC-STRUCTURE/BACKLOG/audits touched. NO commit (per contract).
- 2026-09-27 [agent] — готово к ревью: rename портирован на .claude/.gemini/.qwen, все три AC проверены и тикнуты (0 хитов task-orchestrator, skill+agent на трёх сайтах, YAML-грип #7 пуст), исполнителем
- 2026-09-27 [agent] — Orchestrator review passed: 0 grep hits on all port sides, parity 22/25 intact, paths present ×3, model pins preserved (opus/qwen3.8-max), YAML grep empty. Fold-vs-whenToUse interpretation accepted — it matches the actual .claude agent convention. Runtime live-confirms new agent type visible.
</log>
