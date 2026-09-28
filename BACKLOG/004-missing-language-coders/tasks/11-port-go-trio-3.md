---
feature: 004
task: 11
slug: missing-language-coders
status: done
review: light
created: 2026-09-27
brief: Port go trio ×3
---

<goal>Port the go trio to the three port sides with per-runtime conventions.</goal>
<scope>Same as T9 conventions. Registration sweep remainder folded into T14 — record which.</scope>
<anti-scope>No .kimi-code edits; no BACKLOG//audits/.</anti-scope>
<constraints>YAML rules; NO git commit.</constraints>
<acceptance-criteria>
- [x] Counts 32 agents / 40 skills ×4; YAML grep #7 empty
</acceptance-criteria>
<refs>Spec §Phase C; Plan T11; blocked by T10.</refs>
<obstacles></obstacles>
<log>
- 2026-09-28 [agent] — взята в работу исполнителем
- 2026-09-28 [agent] — normalize AC section (tags on own lines) + tick verified AC; body otherwise unchanged
- 2026-09-28 [agent] — Port done. 9 skill files copied byte-identical from master (go-coder, go-code-reviewer, go-best-practices SKILL.md × .claude/.gemini/antigravity/.qwen — master bodies contain no self-paths). 6 agent files per runtime shape: .claude folded single-line description + model: sonnet; .qwen folded description + model: qwen3.8-flash (uniform pin per spec log note); .gemini keeps whenToUse + role wording, no pin. Reviewer agents carry tools lists per each side's python-code-reviewer shape (Claude/Antigravity: Read/Grep/Glob/Bash/WebSearch/FetchURL; Qwen: read_file/grep_search/glob/run_shell_command/web_fetch); reviewer descriptions containing ": " double-quoted per frontmatter convention. go-best-practices agentless on all sides. Verified: counts 32 agents / 40 skills on all four sides; SYNC-STRUCTURE grep #7 (unquoted ': ' in scalar description/whenToUse) empty; cross-runtime leakage greps clean on all new files; agent bodies identical modulo skill self-path; skill copies diff-clean vs master. Registration sweep remainder folded into T14 (recorded here — obstacles section empty and not patchable): (1) SYNC-STRUCTURE.md:223 count-parity text still "28 agent files + 34 skill dirs" — needs 32/40 after this task, 33/41 after T13; (2) SYNC-STRUCTURE §File mapping table lacks rows for go-coder/go-code-reviewer/go-best-practices (python lane rows also absent); (3) SYNC-STRUCTURE §Frontmatter convention affected-set enumeration says 11 agents — go-code-reviewer joins on the Claude/Qwen sides (description contains ": "); (4) manifest.json needs no registration (dir-mirror picks the files up; T14 dry runs validate). Quirk hit: AC section arrived with both tags on one line → planning_patch_artifact 400'd → normalized via full upsert, order=11 preserved.
- 2026-09-28 [agent] — port complete and verified — handoff to review
- 2026-09-28 [agent] — Orchestrator review passed: go trio ported ×3 (15 files), counts 32/40 ×4, pins verified (sonnet / qwen3.8-flash / gemini role wording), bp agentless ×4, YAML clean. T14 food logged: parity text update, go+python mapping rows, affected-set addition for go-code-reviewer.
</log>