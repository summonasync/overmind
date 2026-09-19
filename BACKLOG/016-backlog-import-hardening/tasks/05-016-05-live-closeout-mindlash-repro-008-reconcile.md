---
feature: 016
task: 05
slug: backlog-import-hardening
status: done
review: light
created: 2026-09-19
---

# 016/05 — Live closeout (Mindlash repro + 008 reconcile)

<goal>
Feature 016 is proven live: the MindlashStudio import repro reruns clean on a throwaway profile, the live Overmind 008 false-missing drift is reconciled with a documented procedure, gates green, 0.1.6 deployed, ceremony closed and pushed.
</goal>

<context>
Feature 016, final task. Tasks 01-04 delivered the key registry, importer fidelity, name pin + pre-seed, and the run-and-exit CLI. The executing agent reads ONLY this file.

Environment: repo /mnt/Ubuntu-Dev/Projects/AI-Tools/Overmind. Live install ~/apps/overmind (symlink → 0.1.5), host headless via nohup, health http://127.0.0.1:4761/api/health. Pack ritual: bash build/pack.sh --version 0.1.6 --bundle-version 2 --rids linux-x64, untar to ~/apps, ln -sfn, restart host from the install dir; SNAPSHOT ~/.overmind before the first 0.1.6 boot (two EF migrations land in this feature). Overmind project id: ff313f29-8144-488c-94e6-5ea5dde57bfd. MCP helper /tmp/omcp.py (recreate per obstacles if missing). Materialization: POST /api/projects/{id}/planning/materialize {"force":false}. Feature status human door: PUT /api/projects/{id}/planning/features/{slug} {"status":"done"}. Mindlash repro source: /mnt/Ubuntu-Dev/Projects/AI-Tools/MindlashStudio (their BACKLOG), commits 8501a30..392b339 hold before/after — use git worktree at 8501a30 for the ORIGINAL files.
</context>

<scope>
- Full gates: dotnet build Overmind.sln && dotnet test Overmind.sln; bash build/smoke.sh (watch guard included)
- Mindlash repro on a THROWAWAY profile (copy ~/.overmind to /tmp or point the profile knob at a temp dir — find the actual knob, never mutate live data): import the ORIGINAL Mindlash BACKLOG (worktree at 8501a30), verify: zero silent drops (unknown keys reported + registered with colors), duplicate-NN skips reported, no double-prefix titles, created dates from mtimes, first materialize force:false = no-op. Record the evidence (report excerpts) for the result artifact.
- Live 008 reconcile: on the LIVE Overmind project, fix the 12 false-missing drift entries (tracked Cyrillic names vs transliterated disk files) — with the name pin the materializer keeps tracked paths; the reconcile is likely: verify each tracked path's content equals disk content under the transliterated name, then run materialize/adopt and confirm drift empties. Whatever the exact working sequence is — DOCUMENT it in the result artifact (it is the runbook for other projects hitting A7). If a manual SQL/file step is needed, say so plainly in the result.
- CLI proof on the packed 0.1.6: --import-backlog against a throwaway profile with the live host up: exits alone, host.port untouched, --report file matches stdout
- Pack 0.1.6 + deploy ritual + health/version check
- Ceremony: result artifact (kind=result) with all evidence → planning_validate → feature done via human door → unpin current → materialize BACKLOG before/after artifact writes → BACKLOG/016 clean
- Git: conventional commit(s) scoped to 016 (user delegated commits); push, report outcome
</scope>

<anti-scope>
- No code changes beyond closeout fixes for what the repro/reconcile proves broken (bigger = new task)
- No sysctl/machine changes; no cleanup of old installs
- Never run imports against the live MindlashStudio/Squeezr project data — throwaway profiles only
</anti-scope>

<constraints>
- Known flake: Internal CLR error on cold dotnet → dotnet build-server shutdown, retry
- pkill -f Overmind.Host bogus exit codes — verify via curl only
- planning_set_status requires logNote for every move
</constraints>

<acceptance-criteria>
- [ ] Gates green; 0.1.6 live with HEAD stamp
- [ ] Mindlash repro evidence: zero silent drops, all A-findings visibly fixed
- [ ] Live 008 drift reconciled + documented runbook in the result artifact
- [ ] CLI run-and-exit + --report proven on the packed binary
- [ ] result validated, feature done, current unpinned, BACKLOG clean
- [ ] Committed; push attempted and reported
</acceptance-criteria>

<refs>
- tasks/01-key-registry.md … tasks/04-import-cli-run-exit.md — the shipped work under test
- @BACKLOG.md — index flip via materialization
</refs>

<obstacles>
- /tmp/omcp.py may have been cleaned: recreate as a python3 stdio JSON-RPC client that sends initialize → notifications/initialized → tools/call to /home/johan-boolman/apps/overmind/mcp/overmind-mcp and prints text content blocks
</obstacles>

<log>
- 2026-09-19 — Created from validated plan 016
- 2026-09-19 — Started: live closeout — Mindlash repro + 008 reconcile + 0.1.6
- 2026-09-19 — Live closeout complete: gates 735/735 + smoke OK; 0.1.6 packed/deployed (snapshot pre-boot, both migrations landed, HEAD stamp 8c0e9e4); Mindlash repro clean on throwaway profile (zero silent drops, A1-A6 fixed, materialize no-op); live 008 drift reconciled 12→0 with documented runbook; CLI run-and-exit + --report proven on packed binary. Result artifact 6749a1b1 validated.
</log>
