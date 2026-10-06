---
feature: 049
task: 04
slug: backlog-index-retirement
status: done
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
- [x] All listed files updated; CONTEXT:131 untouched.
- [x] New ADR present and numbered per the file's convention.
- [x] Kit sentence swap verified identical across 4 contract files and 4 SKILL ports (grep hit sets equal).
</acceptance-criteria>

<refs>
spec cc4c4392-566e-4ff3-a84a-ff6aa61914fd; plan cf343934-d939-482d-9dd2-36dc8e41287b step 5; 116-crewkit-in-repo (kit sync is the deploy path, human-owned).
</refs>

<obstacles>
- One dispatcher commit was impossible as stated: the crew kit (`src/OvermindCrewKit`) is an embedded git repo gitignored by the parent (`/mnt/Ubuntu-Dev/Projects/AI-Tools/Overmind/.gitignore:406`), so the work landed as two same-subject commits — parent `8a0f9fc` (README, CONTEXT, STRUCTURE, AGENTS, TECHNICAL-SOLUTIONS) and kit `c79e072` (x8). No push either side.
- `.claude/skills/overmind-ceremony/SKILL.md` carried uncommitted lane-109 hunks (feature-done gating) in the working tree; staged surgically via a HEAD+own-hunk blob (`git hash-object` + `update-index --cacheinfo`) — the cached diff contains only the 049 bullet swap, lane-109 hunks remain unstaged and untouched.
- CONTEXT.md:131 tombstone comment verified untouched (absent from `git diff`); TECHNICAL-SOLUTIONS.md historical ADR consequence lines 24/92/101/108 with stale index pointers left as-is per ADR immutability — the new ADR's Status line names them as superseded.
- Docs describe the decided end-state per the task; the live migration itself (pointer replacement, row untrack) is task 05, deploy-gated — until it runs, this repo's `BACKLOG.md` still holds generated content.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan step 5
- 2026-10-06 [agent] — 049/04 docs round started
- 2026-10-06 [agent] — Docs round done: parent 8a0f9fc (README x3 spots, CONTEXT x4, STRUCTURE x5, AGENTS x1, new ADR "BACKLOG.md index retired: the board is the only state copy (049) — 2026-10-06" in TECHNICAL-SOLUTIONS.md) + kit c79e072 (x8 sentence swap, md5-identical across 4 contracts + 4 SKILL ports). CONTEXT:131 tombstone byte-untouched; grep sweep: living docs carry only intended end-state mentions, residues confined to audits/FEATURELIST/BACKLOG/ENTRIES trees, task-01..03 code/tests and the kit's own historical trees. 3/3 AC ticked.
- 2026-10-06 [agent] — docs round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 1 SUGGESTION (TECHNICAL-SOLUTIONS «Newest first» header vs append-at-end practice — pre-existing, filed to ledger #261). All 8 kit files md5-verified across commit blobs, CONTEXT:131 tombstone byte-untouched, ADR supersession correct without editing immutable lines, end-state consistency confirmed across 13 docs. Commits 8a0f9fc + kit c79e072.
</log>