---
feature: 049
task: 05
slug: backlog-index-retirement
status: done
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
- [x] Human deploy go recorded (notification reply or explicit instruction) BEFORE any restart/deploy.
- [x] Live pass untracks and strips both index files; trackedFiles contain no BACKLOG.md.
- [x] Ledger orphans have board homes (or an explicit obstacles note why one stayed).
- [x] Pointer file in place; `git diff BACKLOG.md` shows exactly strip + pointer.
</acceptance-criteria>

<refs>
spec cc4c4392-566e-4ff3-a84a-ff6aa61914fd (fate = POINTER, human flags); plan cf343934-d939-482d-9dd2-36dc8e41287b step 4; 033 (force path); 116 (kit sync is separate and human-owned).
</refs>

<obstacles>
- 2026-10-06 (049/05): orphan sweep disposition — 49 open lines of the hand-written ledger moved to the crew microbatch ledger as items #269-#317 (memory/overmind-microbatch-queue.md — spec-sanctioned microbatch-entry homes; full pre-retirement text preserved at `git show 2cce10c:BACKLOG.md`). Covered/resolved WITHOUT a ledger item: Order 007-embedded-agent (board feature 007, planned); L23+L26 IDE0161 (closed by microbatch #23, commit 0f4be6d); L27 router docs drift (ADR ban line gone, STRUCTURE.md:94/:123 now a coherent policy; residue = ledger #252); L34 Serilog (central pin present, Directory.Packages.props:20); L35 planning_update_feature (shipped by 083); L44 003/01 (NIT-1 shipped in 003/02 scope, NIT-2 fixed same change); L50 ClearSource-on-bundled (resolved: SkillKit.ClearSource throws BundledSourceProtected); L62 FTS5 planning bodies (shipped by 053); L63 diff UI (board feature 045, planned); L64/L67/L75/L77/L78/L79 (fixed/tombstoned informational); L81 (duplicate of L76, absorbed into #310).
- 2026-10-06 (049/05): kit-surface reality check — the task text assumed src/OvermindCrewKit/BACKLOG.md is the crew kit project's tracked index; actually the tracked row lives at Skills_dotNetCrew/BACKLOG.md (dotNetCrew project, targetDir Skills_dotNetCrew/BACKLOG, report paths are project-root-relative). Both migrated 2026-10-06: the dotNetCrew pass (18:33 UTC) stripped the tracked file to its tombstone header (row 85 -> 84, no refusal); the in-repo copy src/OvermindCrewKit/BACKLOG.md (the whole src/OvermindCrewKit/ dir is gitignored, .gitignore:406 — local working copy, so this strip is NOT a git change) hand-stripped to the same tombstone-header-only end state. A pre-existing one-blank-line divergence between the two copies (outside the block) left as-is.
- 2026-10-06 (049/05): pointer composition — the coordinating message said the hand-written prose is "preserved in the pointer file"; the validated contract (plan step 4, task scope 4) composes a SHORT pointer replacing all content, with the prose kept in git history. Followed the contract.
- 2026-10-06 (049/05): six non-scoped tracked index rows remain in OTHER projects' repos (MindlashStudio, Chadline, Eww-widgets, Bob.AI-daily-assistant, WoWMemeGenerator, Slurpy.AI — sqlite count over PlanningMaterializedFiles); each drops on that project's next pass via the same stale-row machinery. Out of this task's scope (other repos, anti-scope).
- 2026-10-06 (049/05): concurrent live session activity observed during the task (feature 127-project-tree-icons created and materialized; BACKLOG/036-agent-workflow-onboarding/tasks/04 modified) — untouched by this task, noted for diff review.
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan step 4; split out as the deploy-gated task per coordinator directive (live host serves MCP sessions)
- 2026-10-06 [agent] — 049/05 live migration — deploy done, executing pass + sweep + swap
- 2026-10-06 [agent] — AC1 tick — human deploy go given in-session by the user; 0.8.0 host deployed and restarted before execution (main session).
- 2026-10-06 [agent] — AC2 tick — Overmind pass 18:18 UTC stripped repo-root BACKLOG.md (prose byte-preserved, 128 deletions) and dropped the row; dotNetCrew pass 18:33 UTC stripped Skills_dotNetCrew/BACKLOG.md (row 85->84); sqlite: 0 tracked BACKLOG.md rows for both projects.
- 2026-10-06 [agent] — AC3 tick — 49 open lines moved to the crew microbatch ledger as #269-#317 (spec-sanctioned microbatch-entry homes); covered/resolved items enumerated in obstacles.
- 2026-10-06 [agent] — AC4 tick — pointer per plan step 4; git diff BACKLOG.md vs HEAD 2cce10c = 3 insertions / 212 deletions, nothing else; control pass left the pointer byte-identical.
- 2026-10-06 [agent] — 049/05 obstacles: sweep disposition, kit-surface reality check, pointer composition note, non-scoped rows, concurrent session activity.
- 2026-10-06 [agent] — 049/05 live migration executed: 18:18 UTC pass on the new build — repo-root BACKLOG.md managed block stripped byte-preserving (88 hand lines kept), row untracked, no drift refusal; orphan sweep -> crew ledger #269-#317 (covered list in obstacles); 18:30 UTC pointer swap per plan step 4 (git diff vs HEAD 2cce10c: 3 insertions / 212 deletions); 18:33 UTC dotNetCrew pass stripped Skills_dotNetCrew/BACKLOG.md (tombstone header kept, row 85->84) + gitignored in-repo kit copy stripped to match; 18:36 UTC control pass — written 0, BACKLOG.md absent from the run report, pointer byte-identical after the pass; sqlite PlanningMaterializedFiles: 0 BACKLOG.md rows for Overmind and dotNetCrew (6 remain in other projects' repos, out of scope). No production code touched, no push.
- 2026-10-06 [agent] — live migration complete — see log
- 2026-10-06 [agent] — light review APPROVED — 0 blockers. Pointer composition matches the contract (prose-deviation judged contract-faithful, recovery via git show 2cce10c), 49 orphan items #269-#317 verified with working ex-LNN mapping, covered/resolved claims verified (Serilog pin, IDE0161 commit), sqlite 0 tracked rows for Overmind+dotNetCrew (6 foreign rows — own lanes), zero production code.
</log>