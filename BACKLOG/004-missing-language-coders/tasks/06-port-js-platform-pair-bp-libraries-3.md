---
feature: 004
task: 06
slug: missing-language-coders
status: done
review: light
created: 2026-09-27
brief: Port js-platform pair + bp libraries ×3
---

<goal>Port the js-platform pair + three best-practices libraries to the three port sides.</goal>
<scope>Same as T4 conventions: skills identical modulo self-paths; .claude agents folded description + model: sonnet; .qwen folded + model: qwen3.7-plus; .gemini whenToUse + role wording. Best-practices skills have no agent files on any side.</scope>
<anti-scope>No .kimi-code edits; no registration files; no BACKLOG//audits/.</anti-scope>
<constraints>YAML rules; NO git commit.</constraints>
<acceptance-criteria>
- [x] Counts 28 agents / 34 skills on all four sides
- [x] YAML grep #7 empty; bp skills agentless ×4</acceptance-criteria>
<refs>Spec §Phase A; Plan T6; blocked by T5.</refs>
<obstacles>
- Scope line pinned .qwen to qwen3.7-plus; executed with qwen3.8-flash per user correction 2026-09-28 (spec + plan log notes): uniform qwen3.8-flash pins, mirrored .qwen/agents/node-coder.md.
- <acceptance-criteria> section was authored without a newline after the opening tag, so acText patching found no items (400). ACs ticked via this full normalizing upsert (same order=6); duplicated empty <log> section from the first status move collapsed back to one.
</obstacles>
<log>
- 2026-09-28 [agent] — взята в работу исполнителем
- 2026-09-28 [agent] — T6 executed: 5 skill bodies (js-platform-coder, js-platform-code-reviewer, web-best-practices, node-best-practices, js-platform-best-practices) copied verbatim master→.claude/.gemini/antigravity/.qwen (diff-identical, 15 files); 6 agent files authored per side shapes (.claude folded desc + sonnet; .qwen folded desc + qwen3.8-flash, reviewer tools read_file/grep_search/glob/run_shell_command/web_fetch; .gemini whenToUse + role wording, no pin, reviewer tools Read/Grep/Glob/Bash/WebSearch/FetchURL). Verified: 28 agents / 34 skills ×4; YAML grep #7 empty; bp skills agentless ×4; self-paths correct per side; SYNC contamination greps still clean.
- 2026-09-28 [agent] — work finished, all ACs verified — moved to review
- 2026-09-28 [agent] — Orchestrator review passed: 21 files ported (5 skills verbatim + 2 agents per side), parity 28/34 ×4, pins verified (sonnet / qwen3.8-flash per user correction / gemini role wording), bp agentless ×4, self-paths correct, YAML clean. Executor independently applied the qwen pin correction from the spec log — provenance chain works.
</log>