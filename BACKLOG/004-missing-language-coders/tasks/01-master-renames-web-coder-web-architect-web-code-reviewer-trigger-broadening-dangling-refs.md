---
feature: 004
task: 01
slug: missing-language-coders
status: done
review: light
created: 2026-09-27
brief: Master renames: web-coder/web-architect/web-code-reviewer + trigger broadening + dangling refs
---

<goal>Rename next-js-coder → web-coder, next-js-architect → web-architect, next-js-code-reviewer → web-code-reviewer on master .kimi-code, broadening triggers per the spec routing table.</goal>
<scope>- git mv skills/next-js-coder → web-coder, skills/next-js-architect → web-architect, skills/next-js-code-reviewer → web-code-reviewer; git mv agents/next-js-coder.md, agents/next-js-architect.md, agents/next-js-code-reviewer.md likewise.
- web-coder SKILL.md + agent: name/title updated; description/whenToUse broadened — owns Next.js/React FULL-STACK apps (UI + RSC + Server Actions + BFF + API routes), SPAs, UI component libraries; Next.js sections (FSD/RSC/BFF/React Query) stay at current depth as the primary framework; explicit non-ownership lines: standalone Node services → node-coder, Electron/RN → js-platform-coder.
- web-architect SKILL.md + agent stub: same broadening; clean the stub's residual «and planning» wording (T10 leftover); **Ownership:** consulted BY feature-orchestrator note preserved.
- web-code-reviewer SKILL.md + agent stub: checklist broadened to full-stack web (RSC boundaries, BFF compliance, TS strict, React Query — existing sections stay); reviewer-independence framing (separate agent).
- Dangling refs on master: feature-orchestrator SKILL.md Phase 2 delegation line (next-js-coder → web-coder; full routing table lands in T7); SKILLFLOW.md rows naming the old names; AGENTS.md mentions; SYNC-STRUCTURE.md file-mapping rows ×3 + frontmatter-affected set.
- manifest.json: verify it mirrors whole dirs (no per-skill entries — T1 of 003 finding); no edit unless surprise.</scope>
<anti-scope>No port sides; no new skills; no body rewrites beyond triggers/non-ownership lines; no BACKLOG//audits/.</anti-scope>
<constraints>git mv only; English-only; NO git commit.</constraints>
<acceptance-criteria>
- [x] grep -rn "next-js-" .kimi-code/ SYNC-STRUCTURE.md → 0 hits
- [x] web-coder/web-architect/web-code-reviewer exist as skill dir + agent on kimi; counts stay 23/26
- [x] Non-ownership lines present in web-coder; routing consistent with spec §Resolved domain model
</acceptance-criteria>
<refs>Spec 49cb498f §Phase A items 1–3 + domain model table; Plan d9dc7e30 T1; port side depends on this task (T2).</refs>
<obstacles></obstacles>
<log>
- 2026-09-27 [agent] — взята в работу исполнителем
- 2026-09-27 [agent] — normalize artifact body: duplicate <log> section after the inProgress move broke AC-section parsing (patch 400 "no acceptance-criteria items"); merged logs, AC list kept verbatim
- 2026-09-27 [agent] — second normalization: newline after <acceptance-criteria> opening tag, probing section parser
- 2026-09-27 [agent] — AC items moved to their own lines under the opening tag — probing the patch parser's section expectations
- 2026-09-27 [agent] — executed: 6 git mv renames (3 skill dirs + 3 agent files) with RM status preserved; web-coder/web-architect/web-code-reviewer frontmatter+bodies broadened (full-stack web + SPA + UI libs, Next.js sections kept at depth, non-ownership lines to node-coder / js-platform-coder incl. monorepo rule); web-architect stub «and planning» residual removed, Ownership note preserved verbatim; web-code-reviewer got scope+reviewer-independence framing; dangling refs cleaned beyond the scope-listed files because AC1 grep covers all of .kimi-code/ — feature-orchestrator SKILL.md (5 spots), SKILLFLOW.md rows 27–28, orchestrator agent stubs ×3 (subagents lists), fast-fixer-coder, architect-review, coding-challenge-orchestrator, frontend-designer, naming-helper, ai-obfuscator; SYNC-STRUCTURE.md affected set + 3 agent rows + 3 skill rows renamed in ALL FOUR columns. Verify: grep next-js- on .kimi-code/ + SYNC-STRUCTURE.md = 0 hits; counts 26 skills / 23 agents unchanged; AGENTS.md (root and kimi) has no next-js- mentions — nothing to edit; manifest.json confirmed whole-dir mirror entries, no edit. QUIRKS: (1) inProgress move duplicated <log>, and the patch parser additionally requires a newline after the <acceptance-criteria> opening tag — materialized single-line AC items are invisible to it (fixed via full upsert, 3 AC ticks then succeeded) — T2 executor beware; (2) transient inconsistency: SYNC-STRUCTURE rows now say web-* on port sides whose files are still next-js-* until T2 lands — forced by AC1 covering SYNC-STRUCTURE.md, T2 closes the gap. NO git commit per constraints.
- 2026-09-27 [agent] — work complete, all 3 AC verified (grep=0, trio present at 26/23, non-ownership routing matches spec §Resolved domain model) → review (light) per task flag
- 2026-09-27 [agent] — Orchestrator review passed: next-js- residue 0 on .kimi-code+SYNC-STRUCTURE, web trio present as 6 paths, counts 23/26 intact, non-ownership routing lines present in web-coder, architect stub planning residue 0. Quirks logged (AC parser needs newline after opening tag; SYNC-STRUCTURE rows transiently reference web-* on port sides — T2 closes).
</log>