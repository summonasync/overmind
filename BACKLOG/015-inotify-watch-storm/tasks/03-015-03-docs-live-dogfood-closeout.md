---
feature: 015
task: 03
slug: inotify-watch-storm
status: done
review: light
created: 2026-09-19
---

# 015/03 — Docs + live dogfood closeout

<goal>
Feature 015 is verified live: docs updated, 0.1.5 packed and deployed, per-process inotify counts measured against the 0.1.4 baseline, BACKLOG materialized, result artifact written, feature closed, work committed and pushed.
</goal>

<context>
Feature 015, final task. Task 01 pinned both binaries' content roots; task 02 added the watch guard. The executing agent reads ONLY this file.

Environment: repo /mnt/Ubuntu-Dev/Projects/AI-Tools/Overmind. Live install ~/apps/overmind (symlink → overmind-0.1.4-linux-x64 right now), host runs headless via nohup from the install dir, health http://127.0.0.1:4761/api/health. Pack ritual: `bash build/pack.sh --version 0.1.5 --bundle-version 2 --rids linux-x64` (bundle unchanged), then untar into ~/apps, `ln -sfn`, restart the host with nohup FROM THE INSTALL DIR (cwd matters for watch counts until the fix is live — after the fix it doesn't). Overmind project id: ff313f29-8144-488c-94e6-5ea5dde57bfd. MCP helper /tmp/omcp.py (python3 /tmp/omcp.py <tool> '<json>'; recreate per the obstacles section if missing). Materialization: POST /api/projects/{id}/planning/materialize {"force":false}. Feature status human door: PUT /api/projects/{id}/planning/features/{slug} {"status":"done"}. Baseline numbers to beat (measured 2026-09-19 on 0.1.4): host 43312 watches (cwd was $HOME), adapter 2955 watches from the Overmind repo root, machine budget 65536 was ~64k used.
</context>

<scope>
- Docs: CONTEXT.md — short note that host and MCP adapter pin their content root to the install dir (caller's cwd no longer matters; adapters can be spawned from any repo at ~zero inotify cost); run.sh comment only if it still implies cwd matters; BACKLOG.md Deferred untouched
- Full gates: dotnet build Overmind.sln && dotnet test Overmind.sln (677 baseline); bash build/smoke.sh incl. the watch-guard cell
- Pack 0.1.5, deploy via the ritual, restart host, verify /api/health version stamp matches HEAD
- Live measurement (the dogfood evidence — record all numbers): new host watch count via /proc/<pid>/fdinfo (expect install-tree scale, not 43312); fresh adapter from the Overmind repo root (expect ≤ ~5, was 2955); machine-wide inotify pressure before/after if cheaply measurable
- Ceremony closeout: task 04… (this task) status moves with logNotes; result artifact (kind=result) with the before/after numbers, planning_validate on it, feature status done via the human door, unpin current (planning_set_current isCurrent=false); materialize BACKLOG before and after artifact writes; verify BACKLOG/015-inotify-watch-storm/ clean
- Git: conventional commit(s) covering 015 (user delegated commits); push to origin, report outcome (remote was flaky 503 earlier)
</scope>

<anti-scope>
- No code changes beyond closeout fixes for what live verification proves broken (bigger = new task)
- No sysctl changes on the machine (user's call, not ours)
- No cleanup of old installs/snapshots
</anti-scope>

<constraints>
- Known flake: Internal CLR error on cold dotnet → dotnet build-server shutdown, retry
- pkill -f Overmind.Host returns a bogus exit code — verify host state via curl only
- planning_set_status requires a logNote for every move
</constraints>

<acceptance-criteria>
- [ ] Gates green; 0.1.5 live with HEAD version stamp
- [ ] Live numbers recorded: host and adapter watch counts vs the 43312/2955 baseline
- [ ] CONTEXT.md updated; BACKLOG/015 materialized clean
- [ ] result artifact validated; feature done; current unpinned
- [ ] Committed; push attempted and reported
</acceptance-criteria>

<refs>
- tasks/01-content-root-pin.md, tasks/02-watch-guard-smoke.md — the shipped work under test
- @CONTEXT.md — topology section for the doc note
</refs>

<obstacles>
- /tmp/omcp.py may have been cleaned: recreate as a python3 stdio JSON-RPC client that sends initialize → notifications/initialized → tools/call to /home/johan-boolman/apps/overmind/mcp/overmind-mcp and prints text content blocks
</obstacles>

<log>
- 2026-09-19 — Created from validated plan 015
- 2026-09-19 — 2026-09-19 — closeout started: docs, full gates, pack 0.1.5 (bundle 2), live dogfood measurement vs 43312/2955 baseline
- 2026-09-19 — 2026-09-19 — Closeout complete. Gates: build 0/0, tests 677/677, smoke+watch-guard green (guard: host 28<100, adapter 1<25). Packed 0.1.5 (bundle 2), deployed ~/apps/overmind → 0.1.5, health stamp 1.0.0+bb3fc4f matches HEAD. Live: host 0 inotify watches (baseline 43312), adapter 1 watch from repo root (baseline 2955), machine-wide 23562→23524 of 65536. CONTEXT.md updated; commit+push follow.
</log>
