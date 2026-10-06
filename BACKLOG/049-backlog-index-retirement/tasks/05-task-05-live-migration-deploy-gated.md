---
feature: 049
task: 05
slug: backlog-index-retirement
status: todo
review: light
created: 2026-10-06
depends_on: 01, 02, 03
brief: DEPLOY-GATED live migration: Host restart on the new build, one pass untracks+strips both index files, ledger orphan sweep, hand pointer replacement. Blocked on explicit human deploy go — the main session schedules it.
---

# Task 05 — LIVE migration (DEPLOY-GATED)

<goal>
The two live tracked index rows (this repo + crew kit project) migrate on the NEW build, and repo-root BACKLOG.md becomes the static pointer file (user decision: POINTER).
</goal>

<scope>
1. Restart the dev Overmind Host on the fresh build — the pass runs in-process; the old binary would still write the index.
2. Trigger one pass (`planning_materialize` or a board mutation) on this repo's project: BACKLOG.md untracked, block stripped, hand prose kept; `planning_materialize_status` shows no index among trackedFiles/written. The crew kit project (`src/OvermindCrewKit/BACKLOG.md` -> tombstone header only) rides the same machinery on its next pass.
3. Deferred-ledger orphan sweep BEFORE the pointer edit: enumerate open (non-`[x]`, non-tombstoned) lines of the hand content; verify each against board features/entries; promote orphans via `planning_create_feature`/`write_entry`. Known candidates: macOS `.pkg` installer half, P0 `overmind.localhost` browser-proxy check, zero-byte `overmind.db` boot crash.
4. Hand-replace `BACKLOG.md` with the static pointer (git-tracked edit): `# BACKLOG` + `<removed 2026-10-06 — generated index retired (049); board is the only state copy>` + bullets: `BACKLOG/<feature-slug>/` and `ENTRIES/<type>/` are read-only materialized mirrors (MCP-less sub-agents read here), rewritten by the materializer, never hand-edited + closing line: any status found in a file is stale by definition — check the board.
</scope>

<anti-scope>
Any code or test change; forcing a refused pass without the human's call; touching deployed kit copies outside the repo.
</anti-scope>

<constraints>
DEPLOY GATE: blocked on an explicit human deploy go — do NOT restart or deploy anything; the main session schedules this task with the human. If the live pass refuses on a drifted block (someone hand-edited the index since the last write), do NOT force silently — report the diff; force is the human's call (kept-foreign warning path). No push; the pointer edit is a hand, reviewable git change.
</constraints>

<acceptance-criteria>
- [ ] Human deploy go recorded (notification reply or explicit instruction) BEFORE any restart/deploy.
- [ ] Live pass untracks and strips both index files; trackedFiles contain no BACKLOG.md.
- [ ] Ledger orphans have board homes (or an explicit obstacles note why one stayed).
- [ ] Pointer file in place; `git diff BACKLOG.md` shows exactly strip + pointer.
</acceptance-criteria>

<refs>
spec cc4c4392-566e-4ff3-a84a-ff6aa61914fd (fate = POINTER, human flags); plan cf343934-d939-482d-9dd2-36dc8e41287b step 4; 033 (force path); 116 (kit sync is separate and human-owned).
</refs>

<obstacles>
None at creation.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan step 4; split out as the deploy-gated task per coordinator directive (live host serves MCP sessions)
</log>