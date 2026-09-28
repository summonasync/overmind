---
feature: 004
task: 04
slug: missing-language-coders
status: done
review: light
created: 2026-09-27
brief: Port node trio ×3 with pins
---

<goal>Port the node trio to .claude, .gemini/antigravity, .qwen with per-runtime conventions.</goal>
<scope>Port the three skills (bodies identical to master modulo self-paths) + three agents: .claude agents fold trigger into one quoted description + model: sonnet; .qwen fold into description + model: qwen3.8-flash (corrected 2026-09-28 by user — was qwen3.7-plus); .gemini keep whenToUse, role wording, no pin. No registration edits (T7 owns SYNC-STRUCTURE/contract text).</scope>
<anti-scope>No .kimi-code edits; no registration files; no BACKLOG//audits/.</anti-scope>
<constraints>YAML rules per runtime; NO git commit.</constraints>
<acceptance-criteria>
- [x] Counts 26 agents / 29 skills on all four sides
- [x] Pins verified (claude sonnet, qwen qwen3.8-flash per user correction 2026-09-28); YAML grep #7 empty</acceptance-criteria>
<refs>Spec §Phase A; Plan T4; blocked by T3.</refs>
<obstacles></obstacles>
<log>
- 2026-09-28 [agent] — взята в работу исполнителем
- 2026-09-28 [agent] — USER CORRECTION 2026-09-28: qwen pin is NOT qwen3.7-plus — one uniform qwen pin qwen3.8-flash applied to all three new .qwen agents; AC text updated accordingly. Port done, 18 files: skills (byte-identical to master, diff empty — master bodies carry no ~/.kimi-code self-path) .claude/skills/node-{coder,code-reviewer,architect}/SKILL.md + same under .qwen/skills/ and .gemini/antigravity/skills/; agents .claude/agents/node-*.md (folded single-line description, model: sonnet; reviewer tools Read/Grep/Glob/Bash/WebSearch/FetchURL), .qwen/agents/node-*.md (same fold, model: qwen3.8-flash; reviewer tools read_file/grep_search/glob/run_shell_command/web_fetch), .gemini/antigravity/agents/node-*.md (description + separate whenToUse, no model pin; reviewer tools same as gemini web twin). Verification: counts 26 agents / 29 skills on all four sides; YAML grep #7 (grep -rnE '^(description|whenToUse): [^"|>-].*: ' over all four agents dirs) empty; all 9 frontmatters parse as YAML with key sets matching their siblings; cross-runtime leakage greps on new files empty; node trio visible in live runtime skill list. First status move duplicated the <log> section — normalized via one full upsert (same order 4, single <log>). No .kimi-code/registration/BACKLOG//audits/ edits, no git actions.
- 2026-09-28 [agent] — порт завершён, все AC проверены и закрыты — на ревью
- 2026-09-28 [agent] — Orchestrator review passed: counts 26/29 ×4, node trio live in runtime, pins verified (claude sonnet ×3, qwen qwen3.8-flash ×3 per user's mid-flight correction — applied and logged with provenance, gemini role wording ×3), YAML grep empty, byte-identical skill bodies.
</log>