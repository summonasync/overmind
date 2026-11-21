# Installing Overmind on Ubuntu / Debian

How the Linux build ships, what lands on disk, how to start it, and how to have it come up at
login. Read this before you look for a `.deb` — there isn't one, and the page says what to use
instead. Every claim here is a measured fact from the build scripts and the Host sources at this
commit; the `file:line` index is at the bottom.

## What the Linux build produces

**No packaged installer exists for Linux yet — no `.deb`, no AppImage, no snap.** The Linux
channel is a *portable folder archive*: one `.tar.gz` that unpacks to a single self-contained
directory, plus two shell scripts inside it. Unpacking registers nothing with the system, writes
nothing to `/usr` and needs no admin rights; autostart is a separate opt-in step you run yourself
(below), and uninstall is deleting that folder.

| Channel | Artifact | Status |
|---|---|---|
| Windows | `overmind-<version>-setup.exe` (Inno Setup) | packaged installer exists |
| Linux | `overmind-<version>-linux-x64.tar.gz` | portable folder archive only |
| macOS | `overmind-<version>-osx-*.tar.gz` | build-verified only, no installer |

The archive is produced by `build/pack.sh`, and the default target list is
`win-x64,linux-x64,osx-x64,osx-arm64` — a plain `linux-arm64` build is not cut by default, though
`--rids linux-arm64` is accepted by the argument check.

## Prerequisites

- **x86-64 Linux.** The default Linux target is `linux-x64`.
- **No .NET runtime needed.** Both binaries are published `--self-contained`, the runtime ships in
  the folder.
- **A browser.** The app has no native window; the tray icon is a launcher and the browser is the
  UI.
- **systemd with user sessions** (default on Ubuntu and Debian) — only for the autostart service
  below. `Type=exec` in the unit needs systemd 254+; the machine this page was verified on runs
  systemd 259. A `Type=simple` fallback for older systemd is spelled out in that section.
- **Optional: Docker + Qdrant** for semantic search, **zenity or kdialog** for the "browse folder"
  dialog in the web UI. Both absent = documented degraded behavior, not a broken install.

## Getting the bundle

The build produces the archive; the repo does not document a download URL for it yet. So either
build it from a checkout or unpack whatever artifact your release process hands you.

Build from a repo checkout (needs `dotnet`, `npm`, `jq`, `sha256sum`, and `web/node_modules` from
`npm ci`):

```bash
build/pack.sh --version 0.9.0 --bundle-version 233 --rids linux-x64
# => dist/overmind-0.9.0-linux-x64.tar.gz
# => dist/overmind-0.9.0-linux-x64.tar.gz.sha256
```

`--bundle-version` is a positive integer that drives the bundled skill-kit sync on first boot; a
lower value silently skips the sync, so it only ever goes up.

Check and unpack. Pick one stable home for the folder — upgrades replace the folder in place, so a
path you will not rename is the right choice:

```bash
cd "$(dirname overmind-0.9.0-linux-x64.tar.gz)"
sha256sum -c overmind-0.9.0-linux-x64.tar.gz.sha256

mkdir -p ~/.local/share
tar -xzf overmind-0.9.0-linux-x64.tar.gz -C ~/.local/share
mv ~/.local/share/overmind-0.9.0-linux-x64 ~/.local/share/overmind
```

The archive unpacks to exactly one top-level folder (the smoke test asserts this before it boots
anything), and both scripts inside it arrive executable.

## Layout

```text
~/.local/share/overmind/
├── run.sh                    launcher: pins the web root, execs the Host, forwards your args
├── install.sh                autostart registration (XDG desktop entry), not an unpacker
├── host/                     self-contained Overmind Host
│   ├── Overmind.Host         the process you run
│   └── bundled/              six skill kits + manifest.json, materialized into your projects
│       ├── overmind-ceremony/  dotnet-crew/  rust-crew/  js-crew/
│       └── sociality/  crew-extras/  manifest.json
├── mcp/                      self-contained MCP adapter
│   └── overmind-mcp          the binary your agent config points at
└── web/                      static Next.js export the Host serves
```

The four directory names are the layout contract in `build/dist-layout.env` (`host`, `mcp`, `web`,
`bundled`). The bundled kits are the six listed in that same file, staged read-only (`chmod 444` on
files) next to the Host binary, where the Host looks for them.

## First run

```bash
~/.local/share/overmind/run.sh
```

`run.sh` does three things and nothing else: it resolves its own folder, exports
`OVERMIND_WEB_ROOT=<folder>/web`, and `exec`s `host/Overmind.Host` with whatever arguments you
passed. That pin is why you start the app through the script rather than by calling the binary
directly — the binary also has a fallback that looks for `../web`, but the script makes the answer
explicit.

On startup the Host:

1. creates `~/.overmind/` if it is missing;
2. takes the single-instance lock — if another Overmind is already running, this process opens the
   browser on the running one and exits `0`;
3. binds Kestrel to `http://127.0.0.1:4761`, falling back to an OS-assigned free port when 4761 is
   taken;
4. applies SQLite migrations and seeds/updates the bundled kits;
5. writes the bound port to `~/.overmind/host.port`;
6. starts the tray icon and prints the ready line to the console.

Open the UI at **`http://overmind.localhost:4761`** — that hostname is just the bound loopback
address rewritten for the browser, it resolves to 127.0.0.1 locally. If the port was taken, the
real one is in the port file:

```bash
cat ~/.overmind/host.port
```

Headless (no tray, server-style — this is what the autostart service below uses):

```bash
~/.local/share/overmind/run.sh --no-tray
# console line: "UI ready at http://127.0.0.1:4761"
```

Stop it with `Ctrl+C` (the console lifetime handles `SIGINT`/`SIGTERM` and shuts Kestrel down), or
ask the API politely — `POST /api/shutdown` answers `202 Accepted` and stops the app once the
response is flushed:

```bash
curl -fsS -X POST "http://127.0.0.1:$(cat ~/.overmind/host.port)/api/shutdown"
```

## Data directory

Everything the app knows lives in `~/.overmind/`, outside the app folder — the two are independent,
which is what makes replace-the-folder upgrades safe.

| Path | What it holds |
|---|---|
| `~/.overmind/overmind.db` | SQLite (WAL): projects, entries, planning tables, kits, app settings. Migrations apply at Host startup. |
| `~/.overmind/host.port` | the port the running Host bound; written at startup |
| `~/.overmind/models/` | the local ONNX embedding model, downloaded on first run |
| `~/.overmind/wake/` | per-alias wake drop files for agent sessions |

Base-URL resolution for consumers (the MCP adapter included) is ordered: `OVERMIND_API_URL`
environment variable → `~/.overmind/host.port` → default `4761`.

## Autostart at login

Two routes, and they are not interchangeable:

| | what it starts | needs | turn it off with |
|---|---|---|---|
| `install.sh` | `run.sh` **with the tray** | a graphical desktop session | `install.sh --unregister` |
| systemd user service | `run.sh --no-tray` | systemd user session (+ linger to start before login) | `systemctl --user disable --now overmind-host.service` |

### Route A — the bundled `install.sh`

Run it from inside the unpacked folder (it refuses if `run.sh` is not a sibling):

```bash
cd ~/.local/share/overmind && ./install.sh
```

On Linux it writes one file, `~/.config/autostart/overmind.desktop`, and nothing else:

```ini
[Desktop Entry]
Type=Application
Name=Overmind
Comment=Overmind host with the bundled crew kit
Exec=/home/you/.local/share/overmind/run.sh
Terminal=false
X-GNOME-Autostart-enabled=true
```

`Exec=` is the absolute path of the `run.sh` that was sitting next to the script when you ran it —
move or rename the folder afterwards and re-run `install.sh`. Removal is `./install.sh --unregister`
(it deletes that one `.desktop` file).

Note the naming trap: `install.sh` is an *autostart registrar*, not an installer — it does not
unpack, copy or build anything. Unpacking is `tar`, as above.

### Route B — systemd user service (headless)

Use this when the machine should run Overmind without you logging into a desktop, or when you do not
want a tray icon at all. The unit below is the text that was verified on the machine this page was
written on — `systemd-analyze verify` returned `rc=0`, verbatim in the Evidence appendix.

```bash
mkdir -p ~/.config/systemd/user
tee ~/.config/systemd/user/overmind-host.service > /dev/null <<'EOF'
[Unit]
Description=Overmind host (headless, loopback API and web UI)
After=default.target

[Service]
Type=exec
ExecStart=/bin/bash %h/.local/share/overmind/run.sh --no-tray
WorkingDirectory=%h/.local/share/overmind
Restart=on-failure
RestartSec=3
TimeoutStopSec=15

[Install]
WantedBy=default.target
EOF

systemctl --user daemon-reload
systemctl --user enable --now overmind-host.service
systemctl --user status overmind-host.service
```

Why the unit is shaped this way:

- **`ExecStart` goes through `run.sh`, not the binary** — that is where `OVERMIND_WEB_ROOT` gets
  pinned, so the web UI is served. `--no-tray` is appended to the script's arguments, which the
  script forwards.
- **`/bin/bash <path>` instead of a bare `<path>`.** `systemd-analyze verify` checks that the
  `ExecStart` target exists and is executable, and `%h` expands to the home of whoever runs verify
  — a bare `%h/.local/share/overmind/run.sh` fails verify on any machine where the app is not
  already installed at that exact path. Wrapping in `/bin/bash` makes the check pass on the unit
  text as written here. The script is executable in the archive either way, so if you would rather
  keep the direct form, change the line to
  `ExecStart=%h/.local/share/overmind/run.sh --no-tray` and expect verify to report
  `Command ... is not executable: No such file or directory` until the folder is really there.
- **`Type=exec`** (systemd 254+) reports a failed `exec` as a start failure instead of a service
  that looks healthy and then dies. On older systemd use `Type=simple` — every other line stays the
  same.
- **`Restart=on-failure`** cannot loop on the single-instance path: a second Host that finds a live
  instance exits `0`, which is not a failure.
- **`TimeoutStopSec=15`** bounds shutdown; `Ctrl+C`-equivalent `SIGTERM` is handled by the ASP.NET
  Core host, which stops Kestrel cleanly.

Start it even when nobody is logged in:

```bash
loginctl enable-linger "$USER"
```

Logs: `journalctl --user -u overmind-host.service -f`. A `status=203/EXEC` in that output means the
`ExecStart` path is wrong — check where the folder actually landed.

## Upgrade

Stop the app, replace the folder, start again. User data is not part of the swap:

```bash
systemctl --user stop overmind-host.service      # or just quit the tray
rm -rf ~/.local/share/overmind
tar -xzf overmind-<new-version>-linux-x64.tar.gz -C ~/.local/share
mv ~/.local/share/overmind-<new-version>-linux-x64 ~/.local/share/overmind
systemctl --user start overmind-host.service
```

A higher `bundleVersion` in the new archive's `bundled/manifest.json` is what triggers the bundled
kit re-sync on the first boot after the swap. There is no auto-update — nothing in the app checks
for a newer version.

## Uninstall

```bash
systemctl --user disable --now overmind-host.service
rm ~/.config/systemd/user/overmind-host.service
systemctl --user daemon-reload
~/.local/share/overmind/install.sh --unregister
rm -rf ~/.local/share/overmind
```

`~/.overmind/` is deliberately left in place — that is your database. Delete it only when you mean
it: `rm -rf ~/.overmind`.

## Connecting an agent

Registration on Linux is manual: nothing in the portable archive puts `overmind-mcp` on your `PATH`
(the Windows installer has a task for that; the Linux scripts do not — they only write the autostart
entry). Call the adapter by path, or symlink it yourself:

```bash
~/.local/share/overmind/mcp/overmind-mcp install          # writes agent config entries
ln -s ~/.local/share/overmind/mcp/overmind-mcp ~/.local/bin/overmind-mcp
```

Per-runtime config and what `install` actually edits: [docs/agent-connection.md](agent-connection.md).

## Limitations, honestly stated

- **No packaged Linux installer.** No `.deb`, no AppImage, no snap — portable folder only, and the
  `PATH` registration step a package would have done is on you.
- **`linux-arm64` is not built by default** — you have to ask `pack.sh --rids linux-arm64` for it,
  and that target has not been run-verified here.
- **The tray is desktop-dependent.** On Linux it is a StatusNotifierItem over D-Bus, so it needs a
  session with a tray host; the app also swallows a known Avalonia tray teardown exception
  (avalonia#21979) as benign on shutdown. Headless (`--no-tray`) avoids the whole surface, which is
  why the service uses it.
- **No auth, by design.** The HTTP API is unauthenticated and loopback binding is the security
  boundary. Do not port-forward 4761 or bind anything but 127.0.0.1 — a public bind would need a new
  architecture decision, not a config tweak.
- **No app auto-update**, as stated above.
- **First-run model download.** The default embedding provider fetches its ONNX model on first run;
  offline after that, but the first run wants network unless you switch providers in Settings.
- **Linux verification posture** for the archive is the end-to-end smoke test
  (`build/smoke.sh <archive>`: unpack to an isolated `HOME`, boot headless, health check, kit seed,
  web UI served, clean shutdown). The systemd unit in this page is `systemd-analyze verify`-checked,
  not boot-tested here — starting it would run a Host against the live `~/.overmind` data of the
  machine it was written on.

## Troubleshooting

| Symptom | Check |
|---|---|
| Browser says connection refused on 4761 | `cat ~/.overmind/host.port` — 4761 was taken, the Host fell back to a free port. |
| `run.sh` exits immediately, no error | Another Host holds the single-instance lock; the running one is the answer, and it should have opened your browser. |
| UI loads, no styling / blank | `OVERMIND_WEB_ROOT` is not pinned and `../web` is missing — start via `run.sh`, not the binary. |
| Agent cannot reach the API | Resolution order is `OVERMIND_API_URL` → `~/.overmind/host.port` → 4761. A stale `OVERMIND_API_URL` wins over all of it. |
| `203/EXEC` in the journal | `ExecStart` path does not exist; the folder is somewhere other than `~/.local/share/overmind`. |
| No tray icon in a desktop session | No StatusNotifierItem host in that DE; run with `--no-tray` and open the UI in the browser. |
| Folder picker does nothing | Install `zenity` or `kdialog`. |

## Evidence appendix

Claims in this page map to the sources below, at commit `5904f671` on
`feat/256-release-docs-pack`. Paths are repo-relative.

| Claim | Source |
|---|---|
| Linux channel is a portable `.tar.gz`, win is `.zip`; per-archive `.sha256` sidecar | `build/pack.sh:8`, `build/pack.sh:299`, `build/pack.sh:305` |
| No `.deb`/AppImage anywhere in the build surface | sweep of `build/**`, `docs/**`, `*.md`, `*.env`, `*.csproj`, `*.props` for `appimage\|\.deb\b\|dpkg` → 0 hits |
| Default RID list excludes `linux-arm64`; `--rids` accepts it | `build/pack.sh:17`, `build/pack.sh:246` |
| `--bundle-version` positive int, drives kit sync | `build/pack.sh:50`, `CONTEXT.md:193` |
| `--self-contained` publish of Host and MCP | `build/pack.sh:276-277`, `build/pack.sh:280-281` |
| Layout `host/` `mcp/` `web/` `host/bundled/` | `build/pack.sh:275`, `build/pack.sh:283`, `build/pack.sh:286`, `build/dist-layout.env:8-11` |
| Six bundled kits, names | `build/dist-layout.env:1` |
| Kit files staged read-only | `build/pack.sh:220` (`find … -type f -exec chmod 444`) |
| `run.sh`/`install.sh` copied into the unix archive, made executable | `build/pack.sh:297-298` |
| Archive unpacks to a single top-level folder; `run.sh` executable | `build/smoke.sh:51-52` |
| `run.sh` pins `OVERMIND_WEB_ROOT`, execs `host/Overmind.Host`, forwards args | `build/run.sh:9`, `build/run.sh:10` |
| Binary fallback web-root lookup at `../web` | `src/Overmind.Host/Program.cs:280-284`, env override `src/Overmind.Host/Program.cs:114-115` |
| `install.sh` Linux branch writes only `~/.config/autostart/overmind.desktop` | `build/install.sh:35-52` |
| Generated desktop entry text (`Exec=` = sibling `run.sh`, `Terminal=false`) | `build/install.sh:43-50`, path built at `build/install.sh:12-13` |
| `install.sh` refuses without sibling `run.sh`; `--unregister` removes the entry | `build/install.sh:18`, `build/install.sh:37-41` |
| Autostart hint prints `http://127.0.0.1:4761` + port file | `build/install.sh:28-31` |
| Portable install = unpack + optional autostart; uninstall = delete folder, `~/.overmind` untouched | `CONTEXT.md:192` |
| Upgrade = replace folder; `bundleVersion` bump drives kit sync; no auto-update | `CONTEXT.md:193` |
| Windows is the only packaged installer channel | `CONTEXT.md:194` |
| Linux smoke-verified end to end; headless boot via `run.sh --no-tray` | `CONTEXT.md:195`, `build/smoke.sh:54` |
| `--no-tray` flag parsing | `src/Overmind.Host/Program.cs:43` |
| Headless ready line `UI ready at …` | `src/Overmind.Host/Program.cs:228-231` |
| Single instance: second launch opens browser on the live one, exits 0 | `src/Overmind.Host/Program.cs:47-64` |
| Bind `http://127.0.0.1:<port>`, default 4761, ephemeral fallback | `src/Overmind.Host/Program.cs:101`, `src/Overmind.Host/OvermindPorts.cs:8`, `src/Overmind.Host/OvermindPorts.cs:10-11` |
| `Overmind:BindUrl` override | `src/Overmind.Host/Program.cs:97-105` |
| Port file written at startup | `src/Overmind.Host/Program.cs:220-225` |
| `~/.overmind`, `overmind.db`, `host.port`, `bundled/` next to the binary | `src/Overmind.Host/OvermindPaths.cs:5-6`, `src/Overmind.Host/OvermindPaths.cs:8`, `src/Overmind.Host/OvermindPaths.cs:10`, `src/Overmind.Host/OvermindPaths.cs:12` |
| Data dir created on startup | `src/Overmind.Host/Program.cs:33` |
| Base-URL resolution order env → port file → 4761 | `CONTEXT.md:95`, `src/Overmind.Mcp/ApiBaseUrl.cs:24`, `:30-31` |
| `overmind.localhost` rewrite for the browser | `src/Overmind.Host/BrowserUri.cs:5-8` |
| Loopback is the security boundary, no auth in v1, public bind needs a new ADR | `CONTEXT.md:94` |
| Tray = StatusNotifierItem over D-Bus on Linux; browser is the window | `CONTEXT.md:28` |
| Linux tray teardown exception swallowed as benign (avalonia#21979) | `src/Overmind.Host/Program.cs:252-255` |
| Folder picker shells out to zenity/kdialog on Linux | `CONTEXT.md:28` |
| ONNX model downloaded on first run into `~/.overmind/models/` | `CONTEXT.md:49`, `CONTEXT.md:216`, `src/Overmind.Infrastructure/Embeddings/OvermindModelPaths.cs:11-12` |
| Wake drop dir `~/.overmind/wake/`, one `<alias>.json` per agent alias | `src/Overmind.Infrastructure/Wake/WakeDropWriter.cs:23-24`, `src/Overmind.Infrastructure/Wake/WakeDropWriter.cs:36-43` |
| Adapter binary name `overmind-mcp` | `src/Overmind.Mcp/Overmind.Mcp.csproj:4` |
| `overmind-mcp install` / `install --remove` is the registration surface; the PATH task is Windows-installer-only | `README.md:207-217` |
| `POST /api/shutdown` → `202 Accepted`, then `StopApplication` on response completion | `src/Overmind.Host/Api/ShutdownApi.cs:7-8`, `src/Overmind.Host/Api/ShutdownApi.cs:12-19`, mapped at `src/Overmind.Host/Program.cs:199` |
| `SIGINT`/`SIGTERM` graceful shutdown | .NET generic-host behavior (framework), not repo code; headless path waits on `app.WaitForShutdownAsync()` at `src/Overmind.Host/Program.cs:231` |
| No Linux `PATH` registration step | `build/pack.sh:297` (only `run.sh`/`install.sh` ship), `build/install.sh` writes autostart only |

### Verification record — systemd unit

Environment: `systemd 259 (259.9-0ubuntu0.1)`, `/usr/bin/systemd-analyze`. Unit files were written
to a scratch directory inside the lane worktree and deleted before the commit; the machine's own
units were not touched, and the service was never enabled or started here.

The unit below is byte-identical to the one in the systemd route above ("Route B" under Autostart at
login): it was re-extracted from this page and re-verified, `sha256` matching both ways
(`df8891566b8a57fee82a5e2bc1c2453d82d968bacf5ddfb1eef3bd88c47d59c8`).

```ini
[Unit]
Description=Overmind host (headless, loopback API and web UI)
After=default.target

[Service]
Type=exec
ExecStart=/bin/bash %h/.local/share/overmind/run.sh --no-tray
WorkingDirectory=%h/.local/share/overmind
Restart=on-failure
RestartSec=3
TimeoutStopSec=15

[Install]
WantedBy=default.target
```

Run 1 — the shipped text, verbatim:

```console
$ systemd-analyze verify overmind-host.service; echo "rc=$?"
/etc/systemd/system/hoptodesk.service:9: PIDFile= references a path below legacy directory /var/run/, updating /var/run/hoptodesk.pid → /run/hoptodesk.pid; please update the unit file accordingly.
rc=0
```

The `hoptodesk.service` line is not about this unit — `verify` walks the machine's unit set, and a
foreign third-party unit on this box produced that warning. No warning or error was reported against
the Overmind unit, and the exit code captured before any pipe was `0`.

Run 2 — the direct-exec alternative (`ExecStart=%h/.local/share/overmind/run.sh --no-tray`), so the
`/bin/bash` wrapper above is a documented choice rather than a hunch:

```console
$ systemd-analyze verify overmind-host-direct.service; echo "rc=$?"
/etc/systemd/system/hoptodesk.service:9: PIDFile= references a path below legacy directory /var/run/, updating /var/run/hoptodesk.pid → /run/hoptodesk.pid; please update the unit file accordingly.
overmind-host-direct.service: Command /home/johan-boolman/.local/share/overmind/run.sh is not executable: No such file or directory
rc=1
```

Run 3 — same direct-exec structure, only with the path pointing at a `run.sh` that exists, proving
the failure in run 2 is path existence and not unit syntax:

```console
$ systemd-analyze verify overmind-host-abs.service; echo "rc=$?"
/etc/systemd/system/hoptodesk.service:9: PIDFile= references a path below legacy directory /var/run/, updating /var/run/hoptodesk.pid → /run/hoptodesk.pid; please update the unit file accordingly.
rc=0
```

`Type=simple` (the older-systemd fallback) verifies `rc=0` with the same `ExecStart` line.
