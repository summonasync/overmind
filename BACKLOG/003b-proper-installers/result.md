---
feature: 003b
slug: proper-installers
status: draft
created: 2026-09-21
---

# Result 003b — Proper OS installers (Windows)

<summary>
The Windows half of 003b shipped: `dist/overmind-<version>-setup.exe`, built by `build/windows/pack-win.ps1` from an Inno Setup script, installing per-user (no elevation) or per-machine (elevated) into the same folder layout the 003a portable archive uses. It registers login autostart, optionally puts `overmind-mcp` on PATH, upgrades in place over a running Host by stopping it through the new `POST /api/shutdown`, and uninstalls everything it wrote — removing `~/.overmind` only on explicit consent.

This is also the first time any win-x64 build of Overmind has been executed at all. 003a cross-published the RID from Linux and never ran it (`CONTEXT.md` recorded it as "build-verified only"). Running it surfaced platform defects that had been latent since 003a — they are recorded in `BACKLOG.md` Deferred, not silently fixed.

The macOS `.pkg` half remains blocked and keeps its own `[?]` row in `BACKLOG.md`.
</summary>

<verification>
- **Automated:** `build/windows/smoke-installer.ps1 <setup.exe>` — 12 cells, all green, on real Windows: preflight, silent install (tree correct; `run.cmd`/`install.ps1`/`skills\synced` absent; Start Menu shortcut and uninstall key present), isolated headless boot, `/api/health`, seeded bundled `crew` kit, web UI served with no launcher and no `OVERMIND_WEB_ROOT`, `initialize` handshake against the real `overmind-mcp.exe`, graceful `POST /api/shutdown`, silent uninstall with residue and byte-identical-PATH assertions, upgrade over a running install via real `host.port` discovery, PATH round-trip with `mcppath,autostart`, and an over-long silent `/DIR=` refused pre-UI with exit code 1. The deliberate-failure path was exercised and reverted: a forced assertion fails the run, names the cell and still cleans up.
- **Build:** `dotnet build Overmind.sln` green. Unit tests per project (the suite cannot run as one command on Windows — see Deferred): Core 108/108, Infrastructure 332/336, Api 307/309; the failures are pre-existing Windows-only issues unrelated to this feature.
- **Not verified here:** the interactive wizard pass — SmartScreen prompt, the three task checkboxes, tray icon, autostart across a real sign-out, and the uninstall data prompt. That pass is user-driven; its result is recorded in the task 04 log when performed.
</verification>

<decisions-log>
Decisions taken during execution that are not evident from the code, recorded here because the execution ledger is gitignored and does not survive the commit.

- **`POST /api/shutdown` exists because a windowless tray process cannot be stopped from outside.** `WM_CLOSE` via `taskkill` reaches no window, and Inno's Restart Manager is unreliable against it. Without the endpoint the installer's only option on upgrade/uninstall is `taskkill /F` against a process holding an open SQLite database.
- **The MCP adapter is force-killed, always, user-scoped.** It has no graceful-stop affordance and agents hold it open as a stdio child, locking `{app}\mcp`. Terminating it is safe for data (it is a stateless proxy over the HTTP API) but does break a live agent session during an upgrade. Documented in the shutdown ADR rather than hidden.
- **The installer's liveness probe cannot distinguish a timeout from a refused connection.** Inno's Pascal Script exposes no numeric WinHTTP error code and matching exception text breaks on localised Windows. Mitigated by requiring HTTP 200 plus an Overmind-shaped body, and by a 15 s receive timeout against the Host's 10 s worst-case Qdrant probe. The residual is recorded in Deferred.
- **`skills/synced` was removed from the bundled crew kit** (user decision) after a real install rolled back at `MAX_PATH`. It was 207 files / 4.1 MB of cloud-synced Claude Code skill state, not curated content; excluding it cut the longest payload path from 195 to 67 characters and took the margin at the default install root from 18 to ~130.
- **`LongPathsEnabled` is not a usable lever.** It was already `1` on the build machine when the install failed, because Inno's `SETUP.E32` does not declare `longPathAware`. A check for that flag would have passed while the bug remained, so the guard validates the effective target path length instead.
- **Two independent packaging scripts are an accepted cost** (user decision): `pack.sh` for the portable channel, `pack-win.ps1` for the installer. `build/dist-layout.env` bounds the duplication to build steps by owning the folder names and the crew allowlist; `overmind.iss` still restates the folder names and is recorded as hardening.
- **Windows-only scope.** The backlog entry read "MSIX/pkg"; `.pkg` needs a macOS build host that does not exist, so the feature closes as the Windows half and the macOS row stays `[?]` rather than being closed as done.
- **Parked rather than fixed**, each with reasoning in `audits/architect-review-2026-09-21-003b-{03,04,final}.md` and mirrored into `BACKLOG.md` Deferred: cross-scope PATH duplication, `overmind.iss` restating layout literals, `%USERPROFILE%`/HKCU following the elevating account, trailing-`;` doubling on PATH append, and the Minor sets from all three audits.
</decisions-log>

<artifacts>
- `audits/architect-review-2026-09-21-003b-03.md` — hard review of the installer script and packaging pipeline (1 Critical, 7 Important, 15 Minor; Critical and 4 Important fixed, re-review clean)
- `audits/architect-review-2026-09-21-003b-04.md` — hard review of the smoke harness and docs (1 Critical, 11 Important, 16 Minor; Critical and 8 Important fixed)
- `audits/architect-review-2026-09-21-003b-final.md` — final whole-branch review (PASS-WITH-NITS, 0 Critical)
- `audits/review-003b-01-02.md` — controller summary of the light reviews for tasks 01 and 02 and the scoped re-review of task 03
</artifacts>

<!--
LIFECYCLE: written at feature close; the decisions-log section exists because the
SDD execution ledger lives in a gitignored workspace and is not part of the repo.
-->
