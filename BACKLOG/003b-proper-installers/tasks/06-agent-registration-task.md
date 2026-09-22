---
feature: 003b
task: 06
slug: agent-registration-task
status: in-progress
review: light
created: 2026-09-22
---

# 003b/06 — Wizard registers Overmind with detected AI agents

<goal>
Installing Overmind makes the AI agents on the machine see it, without the user having to discover a separate command. A fourth wizard task — checked by default — runs the adapter's own registration; the uninstaller removes it again so no agent is left pointing at a deleted executable.
</goal>

<context>
Found by the user immediately after the live install that closes this feature: Claude did not see the MCP server. Nothing was broken — the installer had correctly put `overmind-mcp` on PATH and written its registry entries — but registration with agents is a separate step (`overmind-mcp install`), and neither the wizard, the README nor the post-install screen mentions it. The user reasonably expected "installed ⇒ agents see it" and had to ask.

The adapter already implements everything needed (`src/Overmind.Mcp/Registration/`):

```
usage: overmind-mcp install [--all] [--agents <csv>] [--project] [--onboard] [--remove] [--dry-run]

  --all           register at all six agents, ignoring detection
  --agents <csv>  comma-separated agent subset; wins over --all
  --project       project scope (git root from cwd) instead of user scope
  --onboard       write the managed onboard block to the repo's agent instruction file
  --remove        remove the overmind registration instead of adding it
  --dry-run       print the plan, write nothing

exit codes: 0 ok or nothing to do, 1 any failure, 2 usage error
```

Plain `install` (no flags) registers at user scope with whichever of the six agents it detects, and was verified live on this machine: it detected `claude` and `kimi-code`, skipped four others, and wrote a working entry. So this task is wiring, not new registration logic.

Spec D15 / R13 cover this (the spec was amended on 2026-09-22 with a tombstone).

Executor reads ONLY this file.
</context>

<scope>

## 1. The wizard task

In `build/windows/overmind.iss`, add a fourth entry to `[Tasks]`, checked by default:

```
Name: "mcpregister"; Description: "Register Overmind with AI agents found on this machine (writes to their config files)"; GroupDescription: "Integration:"
```

The parenthetical is required, not decoration — this task writes into files the installer does not own, and the user must be able to see that before consenting.

## 2. Run it after install, undo it before uninstall

`[Run]` entry, after the files are copied and before (or alongside) the existing "Start Overmind now" entry:

```
Filename: "{app}\mcp\overmind-mcp.exe"; Parameters: "install"; Tasks: mcpregister; Flags: runhidden waituntilterminated; StatusMsg: "Registering with AI agents..."
```

`[UninstallRun]`, which must execute **before** the files are deleted:

```
Filename: "{app}\mcp\overmind-mcp.exe"; Parameters: "install --remove"; RunOnceId: "UnregisterAgents"; Flags: runhidden waituntilterminated
```

Two things to get right:

- **Neither may fail the operation.** A non-zero exit from the adapter must not abort the install or the uninstall — the app works fine unregistered, and a failed unregister must never block removal. Inno does not fail an install on a `[Run]` non-zero exit by default; verify that holds for `[UninstallRun]` too, and if it does not, wrap the call so it cannot.
- **`[UninstallRun]` ordering.** Inno runs `[UninstallRun]` entries at the start of uninstallation, before `[UninstallDelete]` and before file removal — confirm this in your build rather than trusting the description, because if it ran after deletion the executable would already be gone and every uninstall would silently leave a stale registration.

Note the interaction with the existing adapter-kill: `StopRunningMcpAdapter` force-terminates `overmind-mcp.exe` during `usUninstall`. Make sure the unregister call and the kill do not fight each other — the unregister is a short-lived separate process invocation, but check the ordering and say in your report which runs first and why that is safe.

## 3. README

Add to the Windows section: what the fourth task does, that it writes to agent config files, which agents are detected, that it can be run or reversed later by hand (`overmind-mcp install` / `overmind-mcp install --remove`), and that an agent already running must be restarted to pick up a new registration — the user hit exactly that.

Also correct the PATH note if it currently implies agents will use the bare `overmind-mcp` command: the adapter's registration writes an **absolute** path today. Do not change that behaviour in this task — it is recorded as a separate Deferred item — but the README must not describe something that does not happen.

## 4. Verification

- `pack-win.ps1 -Version 0.4.0 -BundleVersion 1` rebuilds cleanly.
- **Do NOT enable `mcpregister` in the smoke.** `smoke-installer.ps1` installs with `/TASKS=""` and `/TASKS="mcppath,autostart"`; leave both as they are. Enabling it would write into the developer's real `~/.claude.json` and `~/.kimi-code/mcp.json` — unacceptable in an automated test.
- Instead add a smoke cell that proves the plumbing without touching real configs: install silently **without** the task, then invoke the installed `{app}\mcp\overmind-mcp.exe install --dry-run` and assert it exits 0 and prints a plan mentioning at least one agent. That exercises the exact binary the `[Run]` entry would call, with `--dry-run` guaranteeing nothing is written.
- Re-run the full smoke; all cells including the new one must stay green.
- Manually verify the `[UninstallRun]` ordering claim from step 2 by reading the generated install log of a silent install/uninstall cycle (`/LOG=`), and quote the relevant lines in your report.
</scope>

<anti-scope>
- Do not modify `src/Overmind.Mcp/` — the registration command already exists and works. This task is installer wiring plus docs.
- Do not change how `overmind-mcp install` writes the command path (absolute vs bare). That mismatch with the PATH task is a real finding, already recorded in `BACKLOG.md` Deferred, and is out of scope here.
- Do not add `--all` (it would register with agents that are not installed), `--project` (user scope is right for an installed app), or `--onboard` (it writes into repo instruction files, which an installer has no business doing).
- Do not enable the new task in `smoke-installer.ps1`, and do not write to any real agent config from an automated test.
- Do not touch the other three wizard tasks or the `[Icons]`/`[Registry]` entries.
- No git commits.
</anti-scope>

<constraints>
- Inno Setup 6 Pascal Script / `.iss` directives only; Windows PowerShell 5.1 for scripting.
- No new dependencies.
- Scripts carry a usage header only; no inline comments (repo rule).
- The installed tree and all existing behaviour must be unchanged apart from the new task.
- Leave the machine clean: no registration left behind by your testing, real `~/.claude.json` untouched by anything automated. The user's live registration for `claude` already exists and must survive — do not remove it.
</constraints>

<acceptance-criteria>
- [x] `[Tasks]` has `mcpregister`, checked by default, with a caption naming the side effect
- [x] `[Run]` invokes `overmind-mcp install` only when the task is selected; a non-zero exit does not fail the installation (state how you verified)
  <!-- evidence caveat 2026-09-22: established on three throwaway Inno installers, NOT on `overmind.iss` itself, because running the real one would have killed the user's live Host and three in-use MCP adapters. The ordering and exit-tolerance behaviours are Inno engine-level and script-independent, and a direct read of `overmind.iss` confirms no `Check:` or exit-code wrapping exists — but this is proof by analogy. Converts to direct proof on the deferred full smoke run. -->
- [x] `[UninstallRun]` invokes `overmind-mcp install --remove` and provably runs **before** the binaries are deleted — quote the install-log lines that establish the ordering
  <!-- evidence caveat 2026-09-22: established on three throwaway Inno installers, NOT on `overmind.iss` itself, because running the real one would have killed the user's live Host and three in-use MCP adapters. The ordering and exit-tolerance behaviours are Inno engine-level and script-independent, and a direct read of `overmind.iss` confirms no `Check:` or exit-code wrapping exists — but this is proof by analogy. Converts to direct proof on the deferred full smoke run. -->
- [x] A failed unregister cannot block uninstallation (state how you verified)
  <!-- evidence caveat 2026-09-22: established on three throwaway Inno installers, NOT on `overmind.iss` itself, because running the real one would have killed the user's live Host and three in-use MCP adapters. The ordering and exit-tolerance behaviours are Inno engine-level and script-independent, and a direct read of `overmind.iss` confirms no `Check:` or exit-code wrapping exists — but this is proof by analogy. Converts to direct proof on the deferred full smoke run. -->
- [x] README documents the task, its side effect, the detected agents, the manual add/remove commands, and the "restart your agent" caveat; the PATH note no longer implies a bare-command registration
- [x] New smoke cell added to `smoke-installer.ps1` (cell 1b): installed adapter `install --dry-run` asserted to exit 0 and name at least one agent, with nothing written
- [ ] Full smoke green, all cells — **not run**: the user's real Overmind Host + multiple overmind-mcp.exe adapters were live on this machine for the entire task (confirmed via `Get-Process`), and `overmind.iss`'s `PrepareToInstall`/`CurUninstallStepChanged` unconditionally probe/stop the *real* `~/.overmind/host.port` host and `taskkill /F` every `overmind-mcp.exe` the user owns on **any** install or uninstall run — including a "throwaway" smoke run into a temp dir. Per explicit instructions I did not kill the user's live app or use `-KillLiveMcpAdapters` without asking; flagging this back to the controller instead. `pack-win.ps1 -Version 0.4.0 -BundleVersion 1` rebuild is clean (verified); `dotnet build Overmind.sln` is green (verified).
- [x] The user's existing `claude` registration in `~/.claude.json` is still present and unmodified at the end of your work (verified — never touched; the real installer/uninstaller was never invoked)
</acceptance-criteria>

<refs>
- @../spec.md — D15, R13 (added 2026-09-22; reviewer reads, not executor)
- @../plan.md — task 06 row
- `src/Overmind.Mcp/Registration/InstallOptionsParser.cs` — the usage contract quoted above
- `build/windows/overmind.iss` — `[Tasks]`, `[Run]`, `StopRunningMcpAdapter`
- tasks/03-iss-and-pack-win.md — the installer this extends
</refs>

<obstacles>
- The user's real Overmind Host (`Overmind.Host.exe`) and one or more real `overmind-mcp.exe` adapters were running on this machine for the whole task (a live daily-driver install). `overmind.iss`'s `PrepareToInstall` always probes the *real* `~/.overmind/host.port` and gracefully stops whatever answers there, and `StopRunningMcpAdapter` always `taskkill /F`s every `overmind-mcp.exe` the current user owns — on *any* install or uninstall invocation, even one aimed at a disposable temp directory. That means `smoke-installer.ps1` (and any manual silent-install/uninstall cycle of the real `overmind.iss`) cannot be run at all right now without first stopping the user's live app, which I was told not to do without asking. Worked around this for the required empirical proofs (Run/UninstallRun exit-code tolerance, UninstallRun-before-delete ordering) by building three tiny throwaway Inno Setup test installers (unrelated AppIds, own temp dirs, no overmind.iss code) that isolate exactly the two Inno behaviors in question — see task-6-report.md for the full logs. Did not run `smoke-installer.ps1` itself; flagged back to the controller instead of using `-KillLiveMcpAdapters` or otherwise touching the user's live processes.
- One early PowerShell foreground invocation of the throwaway test installer under `/VERYSILENT` unexpectedly returned Inno exit code 2 and took ~2 minutes after being run through a chained Bash-tool background invocation; a clean retry through the PowerShell tool with `Start-Process -Wait` and absolute paths completed normally (exit 0, ~instant) both times after that — attributed to a quirk of the bash-tool's own backgrounding/cwd handling with a long-running native GUI-capable process, not a defect in the installer or the new .iss code.
</obstacles>

<log>
2026-09-22 — general-purpose agent (task 06 executor) — added `mcpregister` Task, `[Run]`/`[UninstallRun]` entries to `build/windows/overmind.iss`; empirically proved (via three standalone throwaway Inno test installers, not the real overmind.iss) that (a) a non-zero exit from `[Run]`/`[UninstallRun]` does not fail install/uninstall by default (no wrapping needed) and (b) `[UninstallRun]` executes before `[UninstallDelete]`/file removal, and that `usUninstall` (where `StopRunningMcpAdapter`/`StopRunningHost` run) fires before `[UninstallRun]`; updated README's Windows section (fourth wizard task, side effect, detected agents, manual add/remove commands, restart-your-agent caveat, corrected the PATH note to stop implying agents use the bare `overmind-mcp` command) and `CONTEXT.md`'s 003b bullet + cell count; added smoke cell 1b to `build/windows/smoke-installer.ps1` (installed `overmind-mcp.exe install --dry-run` from the `/TASKS=""` install, asserts exit 0 + dry-run confirmation + an agent name in output, no config touched) and updated its docstring/summary; verified `dotnet build Overmind.sln` green and `pack-win.ps1 -Version 0.4.0 -BundleVersion 1` rebuilds clean (ISCC compiled the new `[Tasks]`/`[Run]`/`[UninstallRun]` entries without error). Did not run `smoke-installer.ps1` or any real install/uninstall of `overmind.iss` because the user's real Host + mcp adapters were live the whole session; reported this back to the controller instead of stopping the user's app. `~/.claude.json`'s `overmind` entry was never touched and is confirmed present/unmodified at end of task.
</log>
