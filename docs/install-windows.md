# Installing Overmind on Windows

> **Verification mode: source-verified.** Every statement below was read out of the
> Windows build and installer sources plus the Host runtime code at `main` @ `5904f671`
> (`build/windows/overmind.iss`, `build/windows/pack-win.ps1`,
> `build/windows/smoke-installer.ps1`, `build/install.ps1`, `build/run.cmd`,
> `build/pack.sh`, `build/dist-layout.env`, `src/Overmind.Host/*`). No Windows installer
> was executed to produce this page — this documentation lane runs on a Linux box. The
> automated installer proof (`build/windows/smoke-installer.ps1`) and the live pass
> through the real wizard belong to the release train (feature 233), not to this page.
> The line-by-line evidence table lives in the task log of feature 256, task T1.

Overmind ships for Windows as one Inno Setup executable. You download it, run it, and the
Host lives in your own user profile — no admin rights, no .NET install, no Docker. This
page tells you what the installer writes where, what an upgrade does to a running Host,
how to find the UI after the first start, and what uninstall does to your data.

There is a second, less common Windows route: a portable zip archive with `run.cmd` and
`install.ps1` in it. It is documented at the end of this page because the two routes
register autostart differently and confuse people who mix them.

## Prerequisites

| Requirement | Value | Where it comes from |
| --- | --- | --- |
| OS architecture | 64-bit Windows (`x64compatible`) | `ArchitecturesAllowed` / `ArchitecturesInstallIn64BitMode` in `overmind.iss` |
| Rights | None by default — per-user install; UAC only if you opt into a per-machine install | `PrivilegesRequired=lowest`, `PrivilegesRequiredOverridesAllowed=dialog` |
| .NET runtime | Not needed — the Host and the MCP adapter are published self-contained | `dotnet publish … --self-contained` in `pack-win.ps1` |
| Disk | The Host, the MCP adapter, the static web export and six bundled crew-kit folders | `[Files]` + `[InstallDelete]` in `overmind.iss` |

## Get the installer

Releases are published on the GitHub repository's **Releases** page
(`github.com/summonasync/overmind` — the `origin` remote of this repo; see
`git remote -v`). Two files travel with each Windows release:

```
overmind-<version>-setup.exe        # the installer
overmind-<version>-setup.exe.sha256 # lowercase-hash sidecar, ASCII, no BOM
```

The names are not a convention someone made up — `OutputBaseFilename` in `overmind.iss`
builds `overmind-{#AppVersion}-setup`, and `pack-win.ps1` writes exactly that file plus
its `.sha256` sidecar into `dist\`. To check the download:

```powershell
Get-FileHash .\overmind-<version>-setup.exe -Algorithm SHA256
```

Compare case-insensitively: the sidecar stores the hash lowercased, Windows prints it
uppercased.

**The build is unsigned** — there is no `SignTool` step anywhere in the Windows build
path (0 matches in `overmind.iss` and `pack-win.ps1`; it is a personal tool, not a
commercial product). So the first run shows *"Windows protected your PC"*. That is the
expected outcome, not a broken download: **More info** → **Run anyway**.

## Install

Run the setup exe. The wizard's modern-style pages are the standard Inno ones; what
matters are the location and the four tasks.

### Install location

The default is `{autopf}\Overmind`. With the default lowest-privileges install that
resolves per-user, to `%LocalAppData%\Programs\Overmind`; if you deliberately take the
per-machine option (which raises UAC), it becomes `C:\Program Files\Overmind`.

A per-machine install has one real trap. Autostart, the port-file lookup during an
upgrade, and the uninstall data prompt all key off `%USERPROFILE%` and `HKCU` — and when
you accept the UAC prompt with a *different* admin account, those resolve to that
account's profile, not the one you work in. Autostart then registers somewhere you will
never look, and the graceful Host stop during an upgrade silently does nothing. Confirm
UAC with your own account.

### The four wizard tasks

The first three arrive pre-checked; only the desktop shortcut starts unchecked (`Flags:
unchecked` is on that entry alone).

| Task | Default | What it actually writes |
| --- | --- | --- |
| **Start Overmind when I sign in** (`autostart`) | on | `Overmind` string value in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, pointing at `{app}\host\Overmind.Host.exe`; removed on uninstall |
| **Add the MCP adapter to PATH** (`mcppath`) | on | `{app}\mcp` appended to the user (or, elevated, machine) `Path` after install, with a duplicate check first, plus a settings-broadcast (`ChangesEnvironment=yes`) so new shells see it |
| **Register Overmind with AI agents** (`mcpregister`) | on | Runs `{app}\mcp\overmind-mcp.exe install` during the install and **edits agent config files** — see the caveat below |
| **Create a desktop shortcut** (`desktopicon`) | off | A second shortcut on the desktop; the Start Menu shortcut is created regardless |

Two consequences of the registration task worth knowing before you click:

- **A running agent will not pick the registration up.** Agents read their MCP config at
  startup, so restart the agent (Claude Code, Qwen Code, …) after installing. Per-runtime
  detail lives in [docs/agent-connection.md](agent-connection.md).
- Registration is a convenience, not a dependency. It runs hidden and waits for the
  adapter to finish; if it fails, install still succeeds — and uninstall runs
  `overmind-mcp install --remove` so no agent is left pointing at an exe that no longer
  exists.

### What ends up on disk

```
{app}\
├── host\                   Overmind.Host.exe + the whole self-contained Host
│   └── bundled\            six crew-kit folders + manifest.json (bundleVersion, kits, legacy)
├── mcp\                    overmind-mcp.exe (the stdio MCP adapter)
└── web\                    static Next.js export served by the Host
```

The kit folders are cut from `kit-membership.json` in the crew kit following
`build/dist-layout.env`: `overmind-ceremony`, `dotnet-crew`, `rust-crew`, `js-crew`,
`sociality`, `crew-extras`, of which `overmind-ceremony` is marked mandatory and
`crew` is listed as legacy (it is *not* shipped). `pack-win.ps1` fails the build if the
staged skill/agent counts do not match the source tree, so an installer never ships a
partially cut bundle.

### Unattended install

The flags the automated proof uses are the ones that work:

```powershell
.\overmind-<version>-setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR="C:\Overmind" /TASKS="mcppath" /LOG="$env:TEMP\om.log"
```

`/TASKS=""` selects none of the four — that is how the smoke run installs without letting
the registration task near a real agent config. An install is finished when its Inno log
shows `Log closed.`

## First run

Start it from the Start Menu shortcut, the desktop shortcut if you made one, or the
**Start Overmind now** checkbox on the wizard's last page. That checkbox is an interactive
option only — a silent install never starts the Host.

Then the Host does, in order: creates `%USERPROFILE%\.overmind`, opens the SQLite database
(migrations apply themselves), picks port **4761** or the first free one if it is taken,
writes the bound port to `%USERPROFILE%\.overmind\host.port`, starts Kestrel on loopback,
and raises the tray icon.

- **UI URL:** `http://overmind.localhost:<port>`. The `.localhost` hostname resolves to
  loopback in every modern browser with no setup; the API and the MCP adapter keep talking
  to `127.0.0.1` directly.
- **The port is not guaranteed to be 4761.** Anything that needs the real number reads the
  port file — that is the contract for scripts too:

  ```powershell
  Get-Content "$env:USERPROFILE\.overmind\host.port"
  ```
- **One instance.** Running the exe again while a Host is alive does not spawn a second
  process — it opens the browser on the existing instance and exits.
- **Headless** (no tray, for debugging or CI): add `--no-tray`. In that mode the startup log
  prints the raw bound address instead of the `overmind.localhost` form, since there is no
  tray to open a browser from.
- **Config override:** `Overmind:BindUrl` replaces the port probe entirely when it is set.

## Tray notes

The Host has no windows. The tray icon is an Avalonia `TrayIcon` with a two-item menu:
**Open Overmind** (the web UI in your default browser) and **Quit** (a planned stop —
`StopApplication`, the port is released). The icon is an embedded PNG resource
(`Overmind.Host.Assets.tray-icon.png`), so it is present regardless of where you
installed; the *installer's* icons come from `src\Overmind.Host\Assets\overmind.ico`.

On Windows the tray runs its own UI thread in single-threaded apartment mode
(`SetApartmentState(ApartmentState.STA)`), which is why the tray works in a process that
is otherwise a Kestrel server. Quit is the polite stop; the window is the browser.

## Upgrading

Run the new `setup.exe` over the existing install. It is an in-place upgrade — same
`AppId`, so the installer finds the remembered location and reuses it. What happens, in
the order the script runs it:

1. **Stop the running Host — politely.** `PrepareToInstall` reads
   `~/.overmind/host.port`, and only if `GET /api/health` answers 200 *and* the body
   contains both `"status"` and `"vectorSearch"` does it conclude "that is our Host". Then
   `POST /api/shutdown`, then it polls health for up to 10 seconds. Only if the Host is
   still answering does it fall back to `taskkill /F /IM Overmind.Host.exe` filtered to
   your username. A foreign process on that port is left alone.
2. **Stop the MCP adapter — not politely.** `overmind-mcp.exe` processes you own are
   force-killed unconditionally, because the upgrade replaces files the adapter holds open.
   ⚠️ **This kills adapters serving live agent sessions in other repositories on the same
   machine.** Close your agent sessions before upgrading, or accept that they lose their
   Overmind connection mid-conversation.
3. **The bundled kit tree is wiped before copying** — `[InstallDelete]` deletes
   `{app}\host\bundled` wholesale. That is deliberate: an upgrade from the old single-kit
   layout must not leave the retired kit folder sitting next to the six kit folders the new
   manifest ships. Everything under `bundled\` is regenerated from the payload.
4. Files are copied, tasks re-applied, and the Host is *not* restarted by a silent upgrade
   (the start option is `skipifsilent`).

Your data is never part of an upgrade: `%USERPROFILE%\.overmind` lives outside the install
directory and survives it.

**An upgrade can be refused before it starts.** The bundled kits contain deeply nested
files, and the whole payload must fit inside the 259-character Windows path budget from
your install directory. In a silent run the refusal happens in `InitializeSetup`, before
any UI, and is written to the install log; in the wizard you get an explicit error on the
directory page and again on the Ready page, naming the overflow in characters. For an
upgrade, the message tells you to uninstall and reinstall into a shorter directory — the
remembered location is measured, not the one you might prefer. The build side is stricter:
`pack-win.ps1` measures the longest staged path and fails the packaging if it does not fit
a worst-case default root (a 20-character username, the SAM account-name limit), so a
payload that could not install is never published.

## Uninstall

Through **Apps & features** / Control Panel like any program (`UninstallDisplayName` is
`Overmind`), or `unins000.exe` in the install directory. In the uninstall steps:

1. The Host and your `overmind-mcp.exe` processes are stopped, and `{app}\mcp` is removed
   from `Path` (matched case-insensitively, the raw value type preserved).
2. `overmind-mcp.exe install --remove` runs, unregistering Overmind from the same agent
   configs the install touched.
3. `{app}\host`, `{app}\mcp`, `{app}\web` are deleted, then `{app}` if it is empty.
4. Finally a separate confirmation asks whether to delete user data — the database,
   settings and downloaded models in `%USERPROFILE%\.overmind`. **The default is No**: the
   data stays for the next install. A silent uninstall (`/VERYSILENT`) never touches
   `~/.overmind` unless you pass `/DELETEDATA` explicitly.

## The portable zip route

`build/pack.sh` also publishes `overmind-<version>-win-x64.zip` — a self-contained Host,
the adapter, the web export and the bundled kits, with `run.cmd` and `install.ps1` at the
archive root. This route has no wizard and no registry entries:

- **Start it:** `run.cmd` sets `OVERMIND_WEB_ROOT` to the `web` folder next to it and
  launches `host\Overmind.Host.exe`. It takes a `--no-tray`-style argument pass-through, so
  `run.cmd --no-tray` works.
- **Autostart** is a Startup-folder shortcut to `run.cmd`, not an `HKCU\...\Run` value:

  ```powershell
  powershell -ExecutionPolicy Bypass -File .\install.ps1            # register
  powershell -ExecutionPolicy Bypass -File .\install.ps1 -Unregister
  ```

  Run it from inside the unpacked folder — it refuses if `run.cmd` is not next to it, and
  writes nothing outside the Startup folder. Note the printed "UI:
  http://127.0.0.1:4761" is that script's own courtesy line: the real port comes from
  `%USERPROFILE%\.overmind\host.port`, and the tray opens `overmind.localhost`.

Do not mix the routes: the setup.exe payload is verified *forbidden* from containing
`run.cmd` or `install.ps1` (an automated cell throws if it finds them), so a script from
the zip has no business sitting in an installed tree.

## Troubleshooting

| Symptom | What the sources say |
| --- | --- |
| "Windows protected your PC" on first run | Expected — the build is unsigned. **More info → Run anyway**, and verify the `.sha256` sidecar if the download is not from your own build. |
| Agents still do not see Overmind after installing | Restart the agent — config is read at startup. If `overmind-mcp` is not found, the PATH task was off: use `{app}\mcp\overmind-mcp.exe` by full path, or re-run the installer with `/TASKS="mcppath"`. New shells only, or after the broadcast. |
| UI does not open on 4761 | Normal when the port was taken: read `~/.overmind\host.port` and open `http://overmind.localhost:<that-port>`. |
| Agent sessions in other repos dropped during an upgrade | By design — the installer force-kills your `overmind-mcp.exe` processes so it can replace the files they lock. |
| "This installation directory is too long by N character(s)" | Move the install closer to the drive root (`/DIR="C:\Overmind"`). For an upgrade: uninstall, then reinstall into the shorter path. |
| Autostart / upgrade stop does nothing after a per-machine install | You accepted UAC with another account; `HKCU` and `%USERPROFILE%` point at that profile. Reinstall per-user, or re-run elevated as yourself. |
| Data survived an uninstall | That is the default (`No` in the data dialog). Re-run the uninstall and answer yes, or `/VERYSILENT /DELETEDATA` on `unins000.exe`. |

## Verification status

- **Source-verified (this page):** all installer behavior above is read from the files at
  `5904f671`; the evidence table with per-claim line references is in the task log of
  feature 256 T1.
- **Automatically proven on real Windows** (`build/windows/smoke-installer.ps1`, thirteen
  cells 0–11 plus 1b): silent install with a forbidden-path check, an isolated headless
  boot, `/api/health`, the seeded bundled kit set with its manifest `mandatory` flags, the
  web UI at `/`, an MCP `initialize` handshake, `overmind-mcp install --dry-run`, a graceful
  `POST /api/shutdown` (202 Accepted, process gone within 10s) discovered through the real
  `~/.overmind/host.port`, an upgrade over a running install, a silent uninstall with
  residue checks (install dir, Start Menu shortcut, uninstall key, autostart Run value,
  `Path` restored byte-identically, `~/.overmind` intact), a PATH + autostart round-trip,
  and the over-long-directory refusal.
- **Not verified here:** the live wizard experience — SmartScreen prompts, Start
  Menu/tray/autostart across a real sign-in, the uninstall data dialog. That pass is
  user-driven and tracked on the release train (feature 233).

## See also

- [README](../README.md) — what Overmind is and the platform install index.
- [docs/agent-connection.md](agent-connection.md) — what registration writes per runtime.
- [docs/install-ubuntu.md](install-ubuntu.md) — the tarball route on Linux.
- [docs/mcp-tool-contracts.md](mcp-tool-contracts.md) — the adapter's tool surface.
- `build/windows/overmind.iss`, `build/windows/pack-win.ps1`,
  `build/windows/smoke-installer.ps1`, `build/install.ps1`, `build/run.cmd` — the sources
  this page is derived from.
