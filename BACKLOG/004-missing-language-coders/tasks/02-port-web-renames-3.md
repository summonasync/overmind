---
feature: 004
task: 02
slug: missing-language-coders
status: done
review: light
created: 2026-09-27
brief: Port web-* renames ×3
---

<goal>Port the web-* renames from master to .claude, .gemini/antigravity, .qwen.</goal>
<scope>Per side: git mv of the three skill dirs + three agent files; same frontmatter/body broadening as T1 with runtime conventions (.claude folded quoted descriptions; .gemini keeps whenToUse; .qwen folds whenToUse into description, keeps model pins of the agent files); agent stubs cleaned incl. residual «and planning»; dangling refs: SKILLFLOW.md ×3 rows, CLAUDE.md / GEMINI.md / QWEN.md mentions, feature-orchestrator SKILL.md Phase 2 line ×3.</scope>
<anti-scope>No .kimi-code edits; no new skills; no BACKLOG//audits/.</anti-scope>
<constraints>SYNC-STRUCTURE translation tables; YAML frontmatter rules; NO git commit.</constraints>
<acceptance-criteria>
- [x] grep -rn "next-js-" .claude .gemini .qwen → 0 hits
- [x] web trio present as skill+agent on all three sides; counts 23/26 unchanged
- [x] SYNC-STRUCTURE #7 YAML grep empty
</acceptance-criteria>
<refs>Spec 49cb498f §Phase A; Plan d9dc7e30 T2; blocked by T1 (master output).</refs>
<obstacles>
- Executor created a duplicate task artifact (3159541d-ae55-4857-866f-3f38a1d983bd, order 15) by upserting without order — the normalize attempt targeted a new artifact instead of this one. Marked blocked with explanation; needs UI/orchestrator cleanup.
- AC patch quirk: <acceptance-criteria> had no newline after the opening tag, patches 400ed; normalized via this full upsert.
</obstacles>
<log>
- 2026-09-27 [agent] — взята в работу исполнителем
- 2026-09-27 [agent] — normalize acceptance-criteria formatting via full upsert (order=2, in-place update); record duplicate-artifact obstacle
- 2026-09-27 [agent] — AC1 verified: grep -rn "next-js-" .claude .gemini .qwen returns zero hits across all three port sides
- 2026-09-27 [agent] — AC2 verified: web-coder/web-architect/web-code-reviewer exist as skill dir + agent file on .claude, .gemini/antigravity, .qwen; agent/skill counts 23/26 on all four sides
- 2026-09-27 [agent] — AC3 verified: grep -rnE '^(description|whenToUse): [^\"|&gt;-].*: ' over all four agents dirs is empty; all 9 new agent frontmatters also pass a strict PyYAML parse
- 2026-09-27 [agent] — Port complete on all three sides (.claude, .gemini/antigravity, .qwen): 18 git mv renames (3 skill dirs + 3 agent files per side); SKILL.md bodies = master verbatim (verified byte-identical); agent stubs per runtime convention (.claude folded single-line descriptions + sonnet pin; .gemini keeps whenToUse; .qwen folded + qwen3.7-plus pin + canonical qwen tools); residual «and planning» removed from web-architect stubs; node-coder / js-platform-coder non-ownership routing present in every port web-coder (agent desc, skill description, whenToUse). Dangling refs cleaned per side: SKILLFLOW.md rows 27-28 (per-side model wording kept), feature-orchestrator SKILL.md lines 80/87/106/116/167 (== master), ai-obfuscator 372, architect-review 16, coding-challenge-orchestrator 38/43, fast-fixer-coder 51, frontend-designer 277, naming-helper 60, gemini orchestrator subagents lists ×3. CLAUDE.md/GEMINI.md/QWEN.md had no next-js-* identifiers — nothing to change there. Verifications: next-js- grep 0 hits; counts 23/26 ×4 sides; SYNC #7 YAML grep empty; PyYAML parse OK on 9 new frontmatters; runtime leakage greps empty on all three sides. NO git commit (per constraints).
- 2026-09-27 [agent] — Executor done — all 3 ACs ticked with verification evidence, summary in log. Moving to review. Known open item: duplicate task 3159541d (order 15) needs UI cleanup; working tree also carries T1's uncommitted master changes and other features' edits — untouched by this task.
- 2026-09-27 [agent] — Orchestrator review passed: next-js- residue 0 on all port sides, counts 23/26 ×4, web trio present, routing lines in port web-coders, YAML grep empty. Runtime live-confirms web-coder/web-architect/web-code-reviewer agent types. Executor's accidental duplicate task 3159541d marked blocked as junk — UI deletion pending (user action, MCP cannot delete tasks).
</log>