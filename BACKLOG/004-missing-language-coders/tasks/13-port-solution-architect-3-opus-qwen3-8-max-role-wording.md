---
feature: 004
task: 13
slug: missing-language-coders
status: done
review: light
created: 2026-09-27
brief: Port solution-architect ×3 (opus / qwen3.8-max / role wording)
---

<goal>Port solution-architect to the three port sides with per-runtime model pins.</goal>
<scope>Skill identical modulo self-paths; agents: .claude model: opus; .qwen model: qwen3.8-max; .gemini strongest-reasoning role wording; kimi untouched. Registration remainder folded into T14 — record which.</scope>
<anti-scope>No .kimi-code edits; no BACKLOG//audits/.</anti-scope>
<constraints>YAML rules; NO git commit.</constraints>
<acceptance-criteria>
- [x] Counts 33 agents / 41 skills ×4; pins verified; YAML grep #7 empty
</acceptance-criteria>
<refs>Spec §Phase D; Plan T13; blocked by T12.</refs>
<obstacles></obstacles>
<log>
- 2026-09-28 [agent] — взята в работу исполнителем
- 2026-09-28 [agent] — Pin correction applied: dispatcher hedged about a deliberate qwen3.8-max exception for solution-architect; verified against spec log — user's 2026-09-28 correction is absolute (qwen3.8-flash everywhere, no 3.8-max). Executing with model: qwen3.8-flash on the .qwen side; .claude stays model: opus per spec §Phase D (correction touched qwen only).
- 2026-09-28 [agent] — normalize AC section (tags on own lines) + tick verified AC; body otherwise unchanged
- 2026-09-28 [agent] — normalize AC section (tags on own lines) + tick verified AC; body otherwise unchanged
- 2026-09-28 [agent] — Port done. 3 skill files copied byte-identical from master (SKILL.md × .claude/.gemini/antigravity/.qwen — master body contains no self-paths). 3 agent files per runtime shape: .claude folded single-line quoted description (trigger folded: "Use when solution-wide architecture decisions or cross-stack architectural conflicts arise") + model: opus; .qwen same folded description + model: qwen3.8-flash (uniform pin per spec/plan log notes — dispatcher's qwen3.8-max hedge void, see correction entry above); .gemini keeps whenToUse + master description (quoted, contains ': ') + no model pin, role wording carries the strongest-reasoning tier (senior-most authority lives in the skill body; no literal pin exists on any gemini agent). Verified: counts 33 agents / 41 skills ×4 (was 32/40); skill copies diff-clean vs master; agent bodies identical modulo self-path; canonical YAML grep #7 (SYNC-STRUCTURE:237) empty; pins grepped; cross-runtime leakage greps clean on all new files (only self-side opus pin present on .claude). Registration remainder folded into T14 (recorded here — obstacles section empty and not patchable): (1) SYNC-STRUCTURE.md:223 count-parity text still "28 agent files + 34 skill dirs" (go/python increments also pending) — needs 33/41; (2) SYNC-STRUCTURE §File mapping table lacks rows for solution-architect agent+skill (go/python rows also absent); (3) solution-architect absent from SKILLFLOW.md routes and root contract files (CLAUDE.md/AGENTS.md/GEMINI.md/QWEN.md) and from feature-orchestrator consult/delegation lines; (4) manifest.json needs no registration (dir-mirror picks the files up; T14 dry runs validate). Quirk hit as expected: AC section arrived with both tags on one line → planning_patch_artifact 400'd ("no acceptance-criteria items") → normalized via full upsert, order=13 preserved.
- 2026-09-28 [agent] — port complete and verified — handoff to review
- 2026-09-28 [agent] — Orchestrator review passed: solution-architect ported ×3 (6 files), counts 33/41 ×4 (target crew size reached), pins verified (claude opus / qwen3.8-flash per absolute user correction, correctly overriding my prompt's hedge; gemini role wording), skill bodies byte-identical, YAML clean. T14 food: parity text, solution-architect + go/python mapping rows, SKILLFLOW/contract/delegation registration for solution-architect, affected-set additions.
</log>