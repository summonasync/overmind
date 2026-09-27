---
feature: 005
task: 03
slug: global-deploy-rename-validation
status: done
review: light
created: 2026-09-27
brief: Validation sweep: orphan cleanup (qwen), stale grep, presence/counts/freshness ×4
---

<goal>Prove the rename left no stale artifacts in any home and complete it by removing merge-mode orphans.</goal>
<scope>(a) Orphan sweep: ~/.qwen/skills/task-orchestrator and ~/.qwen/agents/task-orchestrator.md are EXPECTED orphans (merge mode never deletes removed crew content) — verify existence, then remove both exact paths (crew-owned; user-approved scope). Check other qwen crew dirs for any other stale crew entries; non-crew content (e.g. bailian-*) stays untouched. Mirror homes: verify task-orchestrator is already gone. (b) Stale grep: grep -ri "task-orchestrator" across the four homes → expect 0 hits after cleanup; investigate and report any hit before touching it. (c) Presence: feature-orchestrator + sprint-planner as skill dir + agent file in all four homes (8/8). (d) Counts: 23 agents / 26 skills per home. (e) Freshness: diff home SKILLFLOW.md and contract files vs repo copies → identical. Record everything verbatim in the log.</scope>
<anti-scope>No non-crew content touched; no repo changes; no settings/hooks files.</anti-scope>
<constraints>Orphan removal is the documented completion of the 003 rename (SYNC-STRUCTURE merge-mode limitation); NO git commit.</constraints>
<acceptance-criteria>
- [x] Orphans removed, exact paths listed; qwen non-crew content untouched
- [x] grep -ri task-orchestrator across homes → 0 hits
- [x] Presence 8/8; counts 23/26 ×4; SKILLFLOW/contract diffs clean
</acceptance-criteria>
<refs>Feature body; 003 result 54a9e4aa.</refs>
<obstacles></obstacles>
<log>
- 2026-09-27 [agent] — Validation sweep started: qwen orphan cleanup + stale grep + presence/counts/freshness across homes.
- 2026-09-27 [agent] — Normalized acceptance-criteria to multiline list so AC items parse before ticking.
- 2026-09-27 [agent] — (a) Orphan sweep:
- Verified existence: /home/johan-boolman/.qwen/skills/task-orchestrator (dir) — present; /home/johan-boolman/.qwen/agents/task-orchestrator.md (807 bytes, Sep 24 18:31) — present.
- Removed exactly these two paths (rm -rf dir, rm -f file), rc=0; re-check: both "No such file or directory" — GONE.
- qwen crew dirs vs repo after cleanup: skills — exact match (no extras, no missing); agents — only EXTRA: python-coder.md (non-crew, left untouched); hooks — IDENTICAL to repo (post-edit-verify.ps1, post-edit-verify.sh).
- Mirror homes: task-orchestrator already gone (kimi no/no, claude no/no) — rsync --delete removed them during T2 deploy.
(b) Stale grep -ri "task-orchestrator":
- Crew-installed content scope (skills/, agents/, hooks/, SKILLFLOW.md, contract files across all homes): 0 hits. Positive control: grep finds feature-orchestrator/sprint-planner files (not blind).
- Raw home-wide counts (runtime data only): ~/.kimi-code 340 files, ~/.claude 679 files, ~/.qwen 61 files; ~/.gemini absent — nothing to grep.
- Hit locations are runtime/transcript data of other tools, NOT crew installs: ~/.claude/backups/.claude.json.backup.*, ~/.claude/file-history/*, ~/.claude/jobs/*/tmp/deploy-*/overmind-*/host/bundled/crew/* (pre-rename crew snapshots in Overmind job tmp), ~/.claude/jobs/*/timeline.jsonl + state.json (incl. THIS job's own d0ea4437 runtime), ~/.kimi-code/sessions/*/*, ~/.qwen/tmp/* outputs/checkpoints, ~/.qwen/usage/token-usage-2026-09.jsonl. Investigated, reported, NOTHING touched (anti-scope: non-crew content untouchable; touching job runtime would break it).
OBSTACLE: gemini home absent (structural, carried from T2) — see (c)/(d).
(c) Presence (skill dir + agent file), per home: kimi feature-orchestrator=1 sprint-planner=1; claude 1/1; qwen 1/1; ~/.gemini/antigravity 0/0 (home absent). Total 6/8.
(d) Counts: ~/.kimi-code skills=26 agents=23; ~/.claude skills=26 agents=23; ~/.qwen skills=26 agents=24 (24 = 23 crew + python-coder.md non-crew); gemini/antigravity — home absent. 23/26 met in all 3 existing homes (qwen agents 23 crew + 1 foreign).
(e) Freshness vs repo (manifest-driven, correct dest paths): kimi agents/skills dirs IDENTICAL, AGENTS.md/SKILLFLOW.md/SOCIAL_DICTIONARY.md/config.toml IDENTICAL; claude agents/skills IDENTICAL, CLAUDE.md/SKILLFLOW.md/SOCIAL_DICTIONARY.md IDENTICAL; qwen skills/hooks dirs IDENTICAL, QWEN.md/SKILLFLOW.md/settings-snippet.jsonc/SOCIAL_DICTIONARY.md IDENTICAL; qwen agents DIFFERS only by non-crew python-coder.md (expected merge extra); gemini/antigravity SKIPPED (no home).
- 2026-09-27 [agent] — Validation sweep complete: qwen orphans removed (2 exact paths), crew-content grep clean (raw hits are runtime data of other tools — reported, untouched), presence 6/8 and counts 23/26 in all 3 existing homes, all mirrored files/dirs identical to repo (qwen agents +1 non-crew python-coder.md). gemini/antigravity structurally N/A: ~/.gemini absent on this machine, installer skipped by design — open item for the user.
- 2026-09-27 [agent] — Orchestrator review passed: my own home sweep confirms counts (26/23, 26/23, 26/24 +1 foreign), task-orchestrator absent in all 3 homes, presence 6/6, foreign python-coder.md untouched, positive control honest (raw grep hits are foreign runtime data, documented not touched).
</log>