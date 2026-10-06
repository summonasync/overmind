---
feature: 049
task: 04
slug: backlog-index-retirement
status: todo
review: light
created: 2026-10-06
brief: Docs sweep: README/CONTEXT/STRUCTURE/AGENTS + new ADR in TECHNICAL-SOLUTIONS + crew kit x8 sentence swap; CONTEXT:131 tombstone untouched; FEATURELIST/ENTRIES/BACKLOG trees untouched.
---

# Task 04 — Docs sweep (in-repo living surface)

<goal>
Every living in-repo doc describes board-only state; the retirement lands as a new ADR; the crew kit x8 carries the no-index rule.
</goal>

<scope>
- `README.md`: lines 29, 58 ("live index" wording), 109 (repo layout row) — the board is the index; BACKLOG/ tree holds artifacts.
- `CONTEXT.md`: line 52 (materializer description — drop the managed block, add retirement + legacy strip), line 149 (Materialization bullet — index maintenance out, block-strip stays as legacy cleanup, "block-only for BACKLOG.md" hash note moves to legacy context), line 151 (declared losses — the index no longer exists), line 170 (hardening-nits home -> board/microbatch). Line 131 sits inside a `<removed>` tombstone comment — DO NOT touch.
- `STRUCTURE.md`: lines 3, 8 (tree), 15 (FEATURELIST description), 42, 100 (reading order).
- `AGENTS.md`: line 36 (work tracking line).
- `TECHNICAL-SOLUTIONS.md`: NEW ADR — "BACKLOG.md index retired: the board is the only state copy" (field case, trade-offs, supersedes the index pointers in earlier ADR consequence lines ~24/92/101/108, which stay untouched per ADR immutability).
- Crew kit x8: `.claude/CLAUDE.md` (~181), `.qwen/QWEN.md` (~182), `.gemini/GEMINI.md` (~179), `.kimi-code/AGENTS.md` (~190) — swap the "BACKLOG.md at the project root is a materialized index..." sentence for: no index file exists; board = UI/MCP; BACKLOG/ tree = materialized contracts. `overmind-ceremony/SKILL.md` x4 ports (`.claude`, `.kimi-code`, `.qwen`, `.gemini/antigravity`, ~line 40) — same swap, byte-identical across ports.
- Untouched: FEATURELIST/* (dated evidence snapshots), ENTRIES/, all BACKLOG/ trees, deployed kit copies outside the repo.
</scope>

<anti-scope>
Code and tests (tasks 01–03); materialized trees; FEATURELIST; other projects' docs; the global ~/.claude deployment (human act at result validation).
</anti-scope>

<constraints>
English in repo docs; keep each file's voice and formatting; CONTEXT:131 tombstone comment must survive byte-identical.
</constraints>

<acceptance-criteria>
- [ ] All listed files updated; CONTEXT:131 untouched.
- [ ] New ADR present and numbered per the file's convention.
- [ ] Kit sentence swap verified identical across 4 contract files and 4 SKILL ports (grep hit sets equal).
</acceptance-criteria>

<refs>
spec cc4c4392-566e-4ff3-a84a-ff6aa61914fd; plan cf343934-d939-482d-9dd2-36dc8e41287b step 5; 116-crewkit-in-repo (kit sync is the deploy path, human-owned).
</refs>

<obstacles>
None at creation.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan step 5
</log>