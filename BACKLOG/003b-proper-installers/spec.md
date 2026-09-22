---
feature: 003b
slug: proper-installers
status: validated
created: 2026-09-21
validated: 2026-09-21
amended: 2026-09-22
---

# Spec 003b — Proper OS installers (Windows)

<decisions>
Brainstorm 2026-09-21 (user answers). D1–D6 are user decisions; D7–D13 are agent proposals — veto at the spec gate.

- **D1 — Inno Setup (user).** The installer is an Inno Setup script (`build/windows/overmind.iss`) compiled by `ISCC.exe` into one `setup.exe`. Rejected: **MSIX** — its payload lands in `C:\Program Files\WindowsApps\<versioned-package-id>\`, a directory with restrictive ACLs whose name changes on every update, so an external agent (Claude Code and friends) has no stable path to spawn `overmind-mcp.exe` with; sideloading also requires signing. **WiX v5** — the transactional install and GPO/Intune story of MSI buys nothing for a personal developer tool and costs Component/GUID ceremony over ~200 published files. **Velopack** — ships auto-update, which 003a explicitly rejected as a non-goal, and adds a production dependency.
- **D2 — Per-user by default, per-machine on request (user).** `PrivilegesRequired=lowest` + `PrivilegesRequiredOverridesAllowed=dialog`: the default target is `%LocalAppData%\Programs\Overmind` with no UAC prompt, and the user can elevate to `C:\Program Files\Overmind`. Rationale: data (`~/.overmind`), the single-instance mutex and the tray are all per-user already.
- **D3 — Wizard tasks and the uninstall prompt (user).** The install wizard carries three tasks: login autostart (checked by default), `overmind-mcp` on PATH (checked), desktop shortcut (unchecked); the Start Menu shortcut is unconditional. The fourth user choice — removing `~/.overmind` — is not an install-time task but an uninstall-time question, because the answer is only meaningful when the data is about to be orphaned; it defaults to No.
- **D4 — `pack-win.ps1` is self-contained (user).** The Windows packaging script performs the whole pipeline itself — web build, both publishes, crew-kit staging, ISCC — rather than consuming an archive produced by `build/pack.sh`. Accepted trade-off: staging logic now exists in two scripts. The rejected agent alternative was a thin script over the 003a archive (zero duplication, Linux keeps producing the payload); the user chose the self-contained route for a single-command Windows build. Drift mitigation is D12.
- **D5 — Icon comes from the zergling set (user).** `web/src/shared/assets/zerlings/menacing.png` (379×385, real alpha, frontal and symmetric — the only one that survives 16 px) is the source for `build/windows/overmind.ico` (16/32/48/256) and, per the same user instruction, replaces `src/Overmind.Host/Assets/tray-icon.png`.
- **D6 — Automated smoke verification (user).** `build/windows/smoke-installer.ps1` is the Windows counterpart of `build/smoke.sh`: silent install, headless boot, API/UI/MCP assertions, silent uninstall, residue check. Verification posture stops being "build-verified only" for win-x64.
- **D7 — `POST /api/shutdown` is the stop contract (agent, approved in design review).** The Host currently exposes no way to stop it from outside: it has no windows (Avalonia tray only), so `WM_CLOSE` via `taskkill` does nothing and Restart Manager is unreliable against a windowless tray process. A loopback-only `POST /api/shutdown` calling `IHostApplicationLifetime.StopApplication()` gives the installer a graceful path for upgrade and uninstall. The installer reads `~/.overmind/host.port`, posts, waits for the single-instance mutex to clear, and only falls back to `taskkill /F` after a timeout. The endpoint lives on the human door, not the agent door — the MCP contract is untouched.
- **D8 — Windows only; the macOS `.pkg` stays blocked (agent).** The backlog entry reads "MSIX/pkg", but no macOS build host exists, so this feature ships the Windows half and the `[?]` macOS entry survives in `BACKLOG.md`. Closing 003b entirely would hide undone work.
- **D9 — The installed layout is the 003a archive layout (agent).** `host/` (with `host/bundled/`), `mcp/`, `web/` under one install root, because `ResolveWebRoot()` already resolves `<host>/../web` — the app finds its UI with no launcher and no environment variable. `run.cmd` and `install.ps1` are deliberately excluded from the installer payload: the shortcut targets `Overmind.Host.exe` directly, and autostart is the job of the installer. The Start Menu and autostart shortcuts set a minimized window style, avoiding the console-flash NIT recorded against the 003a `install.ps1`.
- **D10 — Autostart is always per-user (agent).** Even for a per-machine install, autostart is written to `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. A machine-wide autostart would boot the tray of one user against the data directory of another.
- **D11 — The PATH entry is scoped to the install mode (agent).** A per-user install writes `HKCU\Environment`, a per-machine install writes the system `Environment` key; both append only when the segment is absent and broadcast `WM_SETTINGCHANGE`. Uninstall removes exactly its own segment and never rewrites the rest of PATH.
- **D12 — `build/dist-layout.env` is the shared layout contract (agent).** A flat `KEY=VALUE` file (`source`-able from bash, three-line regex parse from PowerShell) owns the crew-kit allowlist and the `host`/`mcp`/`web`/`bundled` folder names. Both `pack.sh` and `pack-win.ps1` read it, so the D4 duplication is bounded to build steps rather than the layout contract.
- **D14 — The application binary carries its own icon and no console window (user, 2026-09-22).** <!-- amended 2026-09-22 after the user's live wizard pass: the desktop shortcut was created without an icon, because D5 produced an `.ico` for the *installer* (`SetupIconFile`) but nothing ever embedded it into `Overmind.Host.exe`, and a shortcut takes its icon from its target. The same live pass asked for the console window to stop appearing. Both are properties of the Host binary, so they are one change. -->
  `Overmind.Host.csproj` gains `<ApplicationIcon>` so every shortcut, the taskbar, Explorer and the Installed Apps entry show the zergling, and `<OutputType>WinExe</OutputType>` so launching the app shows no console window at all — it lives in the tray. To keep the command line usable, the Host attaches to a parent console when one exists (`AttachConsole(ATTACH_PARENT_PROCESS)`), so running it from a terminal, with `--no-tray`, or as the `--import-backlog` CLI still prints where the operator can see it. Rejected: hiding the console window after start (`ShowWindow(SW_HIDE)`) — the window still flashes, which is the complaint.
- **D15 — The wizard offers to register Overmind with the AI agents it finds (user, 2026-09-22).** <!-- added 2026-09-22 after the user's live install: installing Overmind did not make Claude see the MCP server, because `overmind-mcp install` is a separate manual step that nothing in the wizard, the README or the post-install screen mentioned. The user reasonably expected "installed ⇒ agents see it". -->
  A fourth wizard task runs the adapter's own `overmind-mcp install` after copying files, registering with whichever agents are detected on the machine. It is checked by default, because that expectation is the common case. Registration failure never fails the installation — the app is perfectly usable without it. **The uninstaller must reverse it** (`overmind-mcp install --remove`) before deleting the binaries: leaving a registration behind would point an agent at an executable that no longer exists. The registration writes into the user's own agent config files, so the task's wizard caption says so plainly rather than hiding it.
- **D13 — Version is a pack-time parameter; `AppId` is a committed constant (agent).** The repo pins no assembly version, so `pack-win.ps1 -Version x.y.z` flows into `-p:Version=` and into the Inno `AppVersion`/`VersionInfoVersion` directives, mirroring `pack.sh`. The Inno `AppId` GUID is generated once and committed — changing it later would orphan every installed copy from its upgrade path.
</decisions>

<open-questions>
- None. D7–D13 passed the user gate on 2026-09-21 (spec validated).
</open-questions>

<requirements>
## Host change

- R1 — `POST /api/shutdown` on the human door answers `202 Accepted` and then stops the Host gracefully via `IHostApplicationLifetime.StopApplication()` — the response is written before shutdown begins, so the caller learns the request was accepted rather than losing the connection. Covered by an API test. Loopback binding remains the only security boundary (no new ADR needed); the agent door and the MCP contract are unchanged (D7).

## Packaging

- R2 — `build/windows/pack-win.ps1 -Version <x.y.z> -BundleVersion <int> [-CrewKit <path>] [-SkipWeb]` builds the web export, publishes Host and MCP adapter as self-contained win-x64, stages the crew kit with `manifest.json`, and compiles `dist/overmind-<version>-setup.exe` plus a `.sha256` sidecar. Missing prerequisites (`dotnet`, `npm`, `ISCC.exe`, `web/node_modules`, crew-kit items) fail loudly before any work starts, matching `pack.sh` behaviour (D4).
- R3 — `build/dist-layout.env` is the single source for the crew-kit allowlist and the installed folder names; both `pack.sh` and `pack-win.ps1` consume it and neither restates those values (D12).
- R4 — `build/windows/overmind.ico` carries 16/32/48/256 frames generated from the zergling source by a committed, re-runnable `make-icon.ps1`; `src/Overmind.Host/Assets/tray-icon.png` is regenerated from the same source (D5).

## Installer behaviour

- R5 — The wizard offers per-user (default, no elevation) and per-machine (elevated) installs, and the four tasks of D3 with their stated defaults.
- R6 — The installed tree matches the 003a archive layout and excludes `run.cmd` / `install.ps1`; the Host serves its UI with no launcher and no environment variable (D9).
- R7 — Installing over an existing installation upgrades in place under a stable `AppId`, preserving `~/.overmind`; a running Host is stopped via R1 before files are replaced, with `taskkill /F` only as a post-timeout fallback (D7, D13).
- R8 — Uninstall reverses every write the installer made — files, shortcuts, autostart entry, PATH segment — and prompts separately, defaulting to No, before deleting `~/.overmind`. A silent uninstall never deletes user data unless `/DELETEDATA` is passed (D3, D11).

## Application presentation (D14, added 2026-09-22)

- R11 — `Overmind.Host.exe` embeds the application icon via `<ApplicationIcon>`, so shortcuts, the taskbar, Explorer and the Installed Apps entry render it without the installer having to specify an icon path per shortcut. The single `.ico` has one home in the repo; neither the installer script nor the project keeps a second copy.
- R12 — The Host is a `WinExe`: a normal launch (shortcut, autostart, double-click) shows no console window. When started from a terminal it attaches to the parent console so log output and the `--import-backlog` CLI report still reach the operator; when stdout is redirected (smoke, CI) output continues to flow as before. Verified on all four paths, not assumed.

- R13 — A fourth wizard task, checked by default, runs `overmind-mcp install` after the files are in place, and the uninstaller runs `overmind-mcp install --remove` before deleting them. Neither failure blocks its operation: a non-zero exit from either is logged and ignored. The task caption states that it writes to the user's agent configuration files. The automated smoke must NOT enable this task — it would write to the developer's real agent configs — so it is covered by a dry-run assertion instead, and by the user's live pass (D15).

## Verification and docs

- R9 — `build/windows/smoke-installer.ps1 <setup.exe>` runs a silent install into a temporary prefix, boots the Host headless against an isolated `%USERPROFILE%` so the real developer database is never touched, asserts `/api/health`, the seeded crew kit via `/api/kits`, the web UI at `/`, and an MCP `initialize` handshake, then silently uninstalls and asserts no residue in the install dir, PATH or the Run key. A separate cell covers upgrade-over-running-install through the R1 shutdown path (D6).
- R10 — `CONTEXT.md` loses the "There is no installer package yet" claim and documents the installer channel and its verification posture; `STRUCTURE.md` gains `build/windows/`; `BACKLOG.md` keeps a `[?]` entry for the macOS `.pkg` half (D8); `TECHNICAL-SOLUTIONS.md` gains two ADRs — the installer technology choice (D1) and the shutdown contract (D7). The unsigned-binary SmartScreen prompt is documented for users, not worked around.
</requirements>

<non-goals>
- macOS `.pkg`, code signing, store distribution, app auto-update, Linux packages (inherited from 003a / D8).
- Replacing or retiring `build/pack.sh` and the portable archive channel.
- Migrating existing portable installations into the installed layout.
- Making portable and installed instances run simultaneously — the per-user single-instance mutex forbids it by design, and both read the same `~/.overmind`.
- Stopping Host instances owned by *other* Windows users during a per-machine upgrade: R1 reaches only the port file of the invoking user. A per-machine upgrade while a second user runs the app is out of scope and documented, not handled.
- CI: the script is the CI input later, as in 003a D9.
</non-goals>

<!--
LIFECYCLE: после status: approved файл ЗАМОРОЖЕН.
Изменение = ре-валидация с пользователем + bump version. Молчаливые правки запрещены.
Читатели: оркестратор, ревьюер. Исполняющий субагент spec НЕ читает —
его контракт — tasks/NN-foo.md.
-->
