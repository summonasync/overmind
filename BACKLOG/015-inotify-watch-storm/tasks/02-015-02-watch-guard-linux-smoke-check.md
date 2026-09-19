---
feature: 015
task: 02
slug: inotify-watch-storm
status: done
review: light
created: 2026-09-19
---

# 015/02 — Watch guard (Linux smoke check)

<goal>
A Linux-only smoke guard proves both binaries stay cheap on inotify watches forever: the adapter started from a deep dummy tree and the host started from any cwd keep their watch counts below a small threshold. Wired into build/smoke.sh; skips cleanly off Linux.
</goal>

<context>
Feature 015, second task. Task 01 pinned the content root of both binaries. This guard keeps the regression from coming back silently (the original bug cost 43312 watches on one host and ~4-10k per MCP adapter, starving the machine's 65536 budget). The executing subagent reads ONLY this file.

Existing shape: build/ holds pack.sh, smoke.sh, run.sh, install.sh. build/smoke.sh runs post-pack cells against the packed archive. Watch counting method (no strace dependency): for a pid, iterate /proc/<pid>/fd, find symlinks targeting anon_inode:inotify, sum `grep -c '^inotify' /proc/<pid>/fdinfo/<fd>`.
</context>

<scope>
- New build/watch-guard.sh: bash, set -euo pipefail
  - Linux-only: non-Linux → print skip, exit 0
  - Args/env: path to the adapter binary and the host binary (default to the dist layout the smoke script already unpacks; mirror how build/smoke.sh locates artifacts — READ it first and follow its conventions)
  - Adapter cell: generate a deep dummy tree (e.g. 300 dirs via mkdir -p chains in mktemp -d), cd into it, spawn the adapter (it exits on its own if the API is unreachable — that is FINE for this guard only if the watches are registered before the health check fails; verify empirically: if the adapter exits too early, point OVERMIND_API_URL at a live host or stub; choose whatever keeps the guard self-contained and document the choice in the script header), count watches of its pid, assert < 25
  - Host cell: start the host binary with OVERMIND_WEB_ROOT pointed at a dummy web dir and a throwaway port (Overmind:BindUrl env or whatever the binary already supports — read src/Overmind.Host/Program.cs port resolution), wait for /api/health, count watches, assert < 100 (install tree scale), kill it
  - Cleanup: kill children, rm the temp tree; no set -x noise on failure paths beyond the failing numbers
- build/smoke.sh: call watch-guard.sh as an additional cell (same style as existing cells)
- One-time proof (do it, log the numbers in the task <log>, then discard): run the guard against the CURRENTLY INSTALLED 0.1.4 binaries (~/apps/overmind-0.1.4-linux-x64) and confirm it FAILS (host ~43k from $HOME-scale tree, adapter ~300 from the dummy tree)
</scope>

<anti-scope>
- No changes to src/ (binaries are task 01, already done)
- No pack/deploy/docs (task 03)
- Do not make the guard depend on strace or root
</anti-scope>

<constraints>
- bash, shellcheck-clean style matching existing build/*.sh; no comments restating the code
- The guard must not touch the live host on :4761 or its port file — throwaway ports only; verify host state via curl, never via pkill exit codes (bogus on this machine)
- bash build/smoke.sh must end green on this machine with the guard included
</constraints>

<acceptance-criteria>
- [ ] watch-guard.sh: adapter cell < 25 watches from a 300-dir tree; host cell < 100 watches
- [ ] Wired into build/smoke.sh; full smoke green on Linux; skip path prints on non-Linux (code inspection)
- [ ] Proof run against 0.1.4 binaries failed the guard (numbers in the task log), then discarded
- [ ] No strace/root dependency; temp artifacts cleaned
</acceptance-criteria>

<refs>
- @build/smoke.sh — cell conventions and artifact locations
- @build/pack.sh — dist layout (host/, mcp/ dirs)
- @src/Overmind.Host/Program.cs — port + webRoot resolution for the host cell
- tasks/01-content-root-pin.md — the binaries under test
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-19 — Created from validated plan 015
- 2026-09-19 — Started: watch guard smoke (coder dispatched)
- 2026-09-19 — Coder done: watch-guard.sh wired into smoke.sh; patched host=28/adapter=1, fail-proof on 0.1.4 = 301/301; SMOKE OK; light review dispatched
- 2026-09-19 — Light review PASS-WITH-NITS, audits/review-015-02.md; IMPORTANT (grep -c arithmetic) fixed in-tree, guard re-run green
</log>
