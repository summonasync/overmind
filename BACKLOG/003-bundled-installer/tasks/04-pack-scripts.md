---
feature: 003
task: 04
slug: pack-scripts
status: done
review: light
created: 2026-09-16
---

# 003/04 — Portable distribution: pack.sh + install scripts

<goal>
One command on this Linux host produces versioned self-contained archives (win-x64, linux-x64, osx-x64, osx-arm64), each containing the Host, MCP adapter, embedded web assets, `install.sh`/`install.ps1`, and the `bundled/` snapshot (crew kit + manifest). The linux archive is smoke-verified by actually unpacking and booting it.
</goal>

<context>
Spec D1/D9: portable-first distribution, shell-only build tooling, no CI. `dotnet publish -r` cross-targets win/osx fine from Linux (no execution needed at pack time). The web UI is a static export already consumed by the Host (`OVERMIND_WEB_ROOT` / `web/out` convention — check `Program.cs`). The bundled snapshot source path is pinned in the script with a parameter override (privacy risk noted in the plan: crew kit is personal content). Executor reads ONLY this file.
</context>

<scope>
- `build/pack.sh` — params: `--version <x.y.z>` (required), `--bundle-version <int>` (required, manifest), `--crew-kit <path>` (default: the user's `.claude` crew folder pinned in the script), `--rids <list>` (default all four)
  - publishes the Host self-contained per RID (`dotnet publish -r <rid> --self-contained`), embeds web static export (`npm run build` output — run it if stale or always, pick and record), stages `bundled/` (crew kit snapshot copied read-only + `manifest.json` with `bundleVersion` + kit folder names), adds `install.sh` (unix archives) / `install.ps1` (win archive)
  - outputs `dist/overmind-<version>-<rid>.zip|tar.gz` + a SHA-256 sidecar per archive
- `build/install.sh` — run from inside the unpacked folder: registers XDG autostart (`~/.config/autostart`) or launchd plist (detect OS), prints the port/UI hint; `--unregister` removes the registration; never writes outside autostart dirs
- `build/install.ps1` — Startup-folder shortcut, same contract (best-effort implementation, cannot be run-verified from Linux — record that)
- Smoke verification (script or documented manual steps executed in this task): pack linux-x64 → unpack to a temp HOME → boot the Host headless → observe the bundled seed log/API → kill
- `dist/` is gitignored; `build/` is committed
</scope>

<anti-scope>
- No MSIX/pkg (003b); no CI; no code signing
- No app auto-update machinery
- No Host code changes except what embedding the web assets/bundled path strictly requires (ideally none — `OVERMIND_WEB_ROOT` and the binary-adjacent `bundled/` convention already cover it; if a gap appears, record it in `<obstacles>` before touching Host code)
</anti-scope>

<constraints>
- Shell-only tooling (bash + pwsh for install.ps1 content); no new NuGet/npm packages
- `dotnet build Overmind.sln` still green (no source changes expected)
- Archives must not contain build intermediates, `.git`, or secrets — verify the archive listing in the task log
- No auto-commits
</constraints>

<acceptance-criteria>
- [x] `pack.sh --version --bundle-version` produces all four archives with SHA-256 sidecars
- [x] Archive listing shows: host binary, adapter, web assets, install script, `bundled/` with manifest — and no `.git`/`obj`/secrets
- [x] linux-x64 archive smoke: unpack → boot → seed fires → API answers → clean shutdown
- [x] `dotnet build Overmind.sln` green (sources untouched or trivially extended)
</acceptance-criteria>

<refs>
- @../spec.md — D1, D2, D8, D9, R1–R3 (reviewer reads, not executor)
- @../plan.md — task 04 row + risks (crew kit privacy, win/osx build-verified only)
- tasks/02-boot-seed-sync.md — the boot behavior the smoke test observes
</refs>

<obstacles>
- The Host's default web-root resolution (`ContentRootPath/../../web/out`, `Program.cs:84-87`)
  points two levels ABOVE the content root, so no self-contained layout can satisfy it with the
  web assets inside the unpacked folder — by construction the default escapes the archive root.
  Per the task's anti-scope guidance, Host code was NOT touched: the generated `run.sh`/`run.cmd`
  pin `OVERMIND_WEB_ROOT` to `<archive>/web` (absolute, derived from the script location). The
  smoke asserts the UI is actually served at `/`, so the workaround is verified, not assumed.
- `build/install.ps1` + the Windows layout (`run.cmd`, `.exe`, zip) are best-effort per the plan:
  cross-publish from Linux is verified, but the PowerShell registration path cannot be run-verified
  from this host.
- Read-only bundled snapshot (dirs 555 / files 444) makes plain `rm -rf` fail on the unpacked
  tree — both `pack.sh` (stage drop) and `smoke.sh` (temp cleanup) `chmod -R u+w` before deleting.
</obstacles>

<log>
2026-09-16 — portable distribution shipped: pack + install + run + smoke scripts, four RID archives.

Scripts (all committed under `build/`, shell-only, usage header only — no inline comments):
- `build/pack.sh` — params `--version` (required, x.y.z), `--bundle-version` (required, int >= 1),
  `--crew-kit` (default pinned `/home/johan-boolman/.claude`), `--rids` (default
  `win-x64,linux-x64,osx-x64,osx-arm64`). Publishes Host + MCP adapter self-contained per RID
  (`dotnet publish -r <rid> --self-contained`), always rebuilds the web export (`npm run build`;
  decision recorded: mtime staleness checks are flaky, the build is cheap — requires
  `web/node_modules` present, fails fast with a hint otherwise), stages the crew kit snapshot,
  writes `install.sh`+`run.sh` into unix archives / `install.ps1`+`run.cmd` into the win zip,
  and emits `dist/overmind-<version>-<rid>.tar.gz|.zip` + `.sha256` sidecar per archive.
- `build/run.sh` / `build/run.cmd` — archive launchers; pin `OVERMIND_WEB_ROOT` (see obstacles)
  and exec `host/Overmind.Host[.exe]` with pass-through args.
- `build/install.sh` — `--unregister` / register; XDG autostart (`~/.config/autostart/overmind.desktop`,
  Exec spaces backslash-escaped) on Linux, per-user LaunchAgent
  (`~/Library/LaunchAgents/app.overmind.host.plist`, XML-escaped, RunAtLoad) on macOS; prints the
  UI/port hint (`http://127.0.0.1:4761`, live port in `~/.overmind/host.port`). Writes nothing
  outside the autostart dir. Register+unregister verified on Linux with a temp HOME.
- `build/install.ps1` — Startup-folder `Overmind.lnk` → `run.cmd`, same `--unregister` contract
  (best-effort, not run-verified from Linux).
- `build/smoke.sh <archive>` — unpacks to a temp dir, boots `run.sh --no-tray` under an isolated
  HOME, polls `~/.overmind/host.port` (120s budget, fast-fail with log tail if the host exits
  early), asserts `/api/health` ok, `GET /api/kits` contains `"name":"crew"` with
  `"sourceKind":"bundled"`, `GET /` serves the web export, the `Seeded bundled kits` log line is
  present, then SIGTERMs and cleans up.
- `.gitignore` — `dist/` appended (build/ is committed, dist output stays untracked).

Archive layout (single top folder `overmind-<version>-<rid>/`):
`host/` (self-contained Host, `host/bundled/manifest.json` + `host/bundled/crew/`),
`mcp/` (self-contained `overmind-mcp`), `web/` (static export), `run.*`, `install.*`.
Crew snapshot allowlist (curated, privacy-driven — runtime state stays out):
`agents/`, `skills/`, `CLAUDE.md`, `SKILLFLOW.md`, `SOCIAL_DICTIONARY.md`; everything else in
`.claude` (sessions/, backups/, cache/, projects/, history.jsonl, settings.json, ...) is
deliberately excluded. Kit ships as folder name `crew`; manifest content:
`{"bundleVersion":1,"kits":["crew"]}`. Snapshot staged read-only (555/444).

Verification (worktree root):
- `dotnet build Overmind.sln` — Build succeeded, 0 warnings, 0 errors (no source changes).
- `pack.sh --version 0.3.0 --bundle-version 1` — ran twice (initial + final script), produced:
  `overmind-0.3.0-win-x64.zip` 134M, `overmind-0.3.0-linux-x64.tar.gz` 110M,
  `overmind-0.3.0-osx-x64.tar.gz` 101M, `overmind-0.3.0-osx-arm64.tar.gz` 108M — each with a
  `.sha256` sidecar; all four sidecars verify `OK` via `sha256sum -c`.
- Archive listings: 803 / 794 / 791 / 792 entries (win/linux/osx-x64/osx-arm64); forbidden-path
  scan (VCS dir, `obj/`, `bin/`, `.env*`, secret/key/pfx/pem patterns) — 0 hits in every archive.
  Key files present per AC: `host/Overmind.Host[.exe]`, `mcp/overmind-mcp[.exe]`,
  `web/index.html`, `host/bundled/manifest.json`, `host/bundled/crew/` (73 files), install+run
  scripts per OS.
- Smoke on `overmind-0.3.0-linux-x64.tar.gz` (run on both the initial and final archives):
  `SMOKE OK: port=4761 health=ok crew kit seeded (bundled) web UI served clean shutdown` —
  isolated HOME, `--no-tray`, exit 0.
- No git state changed; nothing staged or committed.

Decisions:
- Web export rebuilt on every pack (recorded per scope's "pick and record").
- `install.sh` on macOS registers the plist but does not bootstrap it into the running session —
  prints the `launchctl bootstrap` command instead (registration is the contracted job; starting
  a service on an OS we cannot test from here is left to the user).
- Script portability details: `pipefail`-safe summaries (`find | wc -l` instead of `grep -c`
  where grep's no-match exit 1 would kill the script), zip only required when a win rid is
  selected, rid whitelist regex `^(win|linux|osx)-(x64|arm64)$`.
</log>
