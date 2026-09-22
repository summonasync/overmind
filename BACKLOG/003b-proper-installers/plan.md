---
feature: 003b
slug: proper-installers
status: validated
version: 1
spec: spec.md
created: 2026-09-21
validated: 2026-09-21
---

# Plan 003b — Proper OS installers (Windows)

<approach>
Four sequential tasks: the two prerequisites the installer needs from the repo (a graceful stop contract, real icons), then the installer itself, then verification and docs. Backend-first, same shape as 003a. Only task 01 touches C#; tasks 02–04 are packaging assets and scripts under `build/`, which sits below every project and violates no dependency rule. No new NuGet or npm packages — the icon generation uses `System.Drawing` shipped with Windows, and Inno Setup is external build tooling, not a dependency of the product.

Key design consequences from spec.md:

- **The stop contract comes first (D7/R1).** Every later task depends on it: the installer calls it before replacing files, the uninstaller before deleting them, and the smoke upgrade cell asserts it. A windowless Avalonia tray process cannot be closed by `WM_CLOSE`, so without R1 the installer has only `taskkill /F`.
- **The installer never launches a launcher (D9/R6).** `ResolveWebRoot()` (`src/Overmind.Host/Program.cs:227-246`) resolves `<host>/../web` before falling back, so shortcuts point straight at `Overmind.Host.exe` and `run.cmd`/`install.ps1` stay out of the payload. This is why the installed tree must keep the 003a folder names exactly.
- **Layout is data, build steps are code (D12/R3).** `build/dist-layout.env` owns the crew-kit allowlist and the folder names; `pack.sh` and `pack-win.ps1` read it. The user chose two independent build scripts (D4) — this bounds the duplication to the steps, not the contract.
- **Graceful stop is implemented in Pascal Script, not a helper process.** Inno's `CreateOleObject('WinHttp.WinHttpRequest.5.1')` can POST to the loopback endpoint directly, so the installer needs no temporary PowerShell extraction to stop a running Host.
- **Verification is a real run on real Windows (D6/R9).** This is the first time a win-x64 build of Overmind is executed at all; 003a shipped it build-verified only. Anything that surfaces here is a fix-forward inside this feature, not a new one.

Scope note: this plan covers the **Windows** half only. The macOS `.pkg` keeps its `[?]` row in `BACKLOG.md` (spec D8).
</approach>

<tasks>
- [x] 01-shutdown-endpoint — Host: `POST /api/shutdown` returning `202 Accepted` and stopping via `IHostApplicationLifetime` in `Response.OnCompleted`; registered in `Program.cs` next to the other `Map*Api()` calls; Api test asserts 202 + `ApplicationStopping` fires; verify: `dotnet build` + `dotnet test` → tasks/01-shutdown-endpoint.md
- [x] 02-installer-icons — `build/windows/make-icon.ps1` renders `menacing.png` into a square multi-frame `build/windows/overmind.ico` (16/32/48/256) and regenerates `src/Overmind.Host/Assets/tray-icon.png`; both artifacts committed; verify: frames enumerate correctly via `System.Drawing.Icon` + tray icon still loads in a Host run → tasks/02-installer-icons.md
- [x] 03-iss-and-pack-win — `build/dist-layout.env` (+ `pack.sh` reads it), `build/windows/overmind.iss` (per-user/per-machine, three tasks, PATH registration, graceful stop, uninstall data prompt), `build/windows/pack-win.ps1` (prereq checks → web build → two publishes → crew staging → ISCC → `dist/overmind-<version>-setup.exe` + sidecar); verify: setup.exe produced, payload listing clean, `pack.sh --rids linux-x64` still green → tasks/03-iss-and-pack-win.md
- [x] 05-app-icon-and-winexe — <!-- added 2026-09-22 from the user's live wizard pass (spec D14/R11/R12) --> `Overmind.Host.csproj`: `<ApplicationIcon>` so shortcuts stop rendering blank, and `<OutputType>WinExe</OutputType>` plus a parent-console attach so no console window appears on a normal launch while terminal runs, `--no-tray` and `--import-backlog` keep printing; verify all four launch paths and re-run the smoke → tasks/05-app-icon-and-winexe.md
- [ ] 06-agent-registration-task — <!-- added 2026-09-22: the live install showed that installing Overmind does not make agents see it (spec D15/R13) --> `overmind.iss`: a fourth wizard task running `overmind-mcp install` after copy, plus `[UninstallRun]` with `install --remove` before deletion so no agent is left pointing at a deleted exe; README section; smoke proves the plumbing via `install --dry-run` without touching real agent configs → tasks/06-agent-registration-task.md
- [ ] 04-smoke-and-docs — `build/windows/smoke-installer.ps1` (silent install → isolated-profile headless boot → health/kits/UI/MCP asserts → silent uninstall → residue check, plus the upgrade-over-running-install cell); live install on this machine through the real wizard; `CONTEXT.md`, `STRUCTURE.md`, `BACKLOG.md`, `README.md`, two ADRs in `TECHNICAL-SOLUTIONS.md` → tasks/04-smoke-and-docs.md
</tasks>

<risks>
- **First-ever win-x64 execution.** 003a never ran this build (`CONTEXT.md:136`). Avalonia tray init, SQLite native interop, the ONNX model download path and FTS5 on Windows are all unproven here. Mitigation: task 01 ends with a real `dotnet run` on this machine, so a platform break surfaces before any packaging work is built on top of it.
- **Inno `{%USERPROFILE}` vs the Host's `SpecialFolder.UserProfile`.** The installer resolves the port file from an environment variable while the Host uses the shell folder API; they agree on a normal machine but can diverge with redirected profiles. Mitigation: the stop helper treats a missing port file as "not running" and continues, never blocking the install.
- **A silent install cannot answer the uninstall data prompt.** `/VERYSILENT` uninstall must default to keeping data (R8); a wrong default here destroys user data non-interactively. Mitigation: task 04's residue cell asserts `~/.overmind` survives a silent uninstall, and the `/DELETEDATA` path is tested separately against a throwaway profile.
- **PATH rewriting is destructive if done carelessly.** Appending via `{olddata}` is safe, removal is not. Mitigation: the uninstall path removes exactly its own segment by string match and leaves PATH untouched when the segment is absent; task 04 asserts the surrounding PATH is byte-identical before and after.
- **Two packaging scripts drift (accepted, D4).** Mitigation is `dist-layout.env` plus an explicit note in both script headers pointing at the other one.
- **SmartScreen on an unsigned setup.** Expected and documented (spec D1 / 003a ADR); it will appear during the live install in task 04 and must not be mistaken for a defect.
</risks>

<verification>
- `dotnet build Overmind.sln` + `dotnet test Overmind.sln` — green after task 01 and at feature close.
- `build/windows/pack-win.ps1 -Version <x.y.z> -BundleVersion <int>` — produces `setup.exe` + verified SHA-256 sidecar (task 03).
- `build/pack.sh --rids linux-x64 --crew-kit <path>` — still green after the `dist-layout.env` refactor, proving the shared contract did not break the 003a channel (task 03).
- `build/windows/smoke-installer.ps1 <setup.exe>` — `SMOKE OK`, all cells including upgrade-over-running-install (task 04).
- Live wizard install on this machine: tray icon appears, UI opens, `overmind-mcp` resolves on PATH in a fresh shell, autostart entry present, uninstall leaves no residue and keeps `~/.overmind` (task 04).
- Reviews: light on 01 and 02, hard on 03 (the installer contract) and 04 (closeout).
</verification>

<!--
LIFECYCLE: после status: approved файл ЗАМОРОЖЕН (как spec.md).
Чекбоксы в <tasks> — единственное, что обновляется по ходу работы.
-->
