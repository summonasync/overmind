---
feature: 003
slug: bundled-installer
status: validated
created: 2026-09-16
validated: 2026-09-17
---

# Spec 003 — Installer with pre-bundled kits

<decisions>
Brainstorm 2026-09-16 (user answers). D1–D4 are user decisions; D5–D9 are agent proposals — veto at the spec gate.

- **D1 — Portable-first (user).** 003a ships self-contained distribution archives — `overmind-<version>-win-x64.zip`, `overmind-<version>-linux-x64.tar.gz`, `overmind-<version>-osx-<arch>.tar.gz` (single-file `dotnet publish -r`, host + MCP adapter + web assets inside) — plus install scripts (`install.ps1` / `install.sh`) that unpack to a user folder and register autostart (Startup shortcut / launchd plist / XDG autostart). MSIX/pkg proper installers move to 003b — blocked on a Windows/macOS build host or CI runners. "Linux packages" stays a non-goal: a tar.gz + script is not a package.
- **D2 — The curated set is the user's crew kit (user).** First bundled kit = the existing crew kit (`.claude` agents/skills/SOCIAL_DICTIONARY etc.), snapshotted into the distribution at build time — dogfooding, content already battle-tested. Bundles live in a read-only `bundled/` folder next to the app binary in the exact layout the 002 scanner consumes (a multi-kit folder: each subfolder = one kit). A `bundled/manifest.json` records `bundleVersion` and kit names.
- **D3 — First run seeds automatically on boot (user).** Host startup: if the library has zero kits AND the seed flag (`AppSetting` `BundledSeedVersion`) is absent → import every bundled kit through `KitImporter` with `SourceKind = Bundled` and stamp the flag. If the user later deletes all kits, bundles do NOT come back on their own — the flag is the once-guard.
- **D4 — Edit forks the bundle to manual (user).** The first content edit of an entry owned exclusively by a bundled kit converts that kit in place to `Manual` (drops the bundle link, clears source fields — the kit becomes an ordinary user kit). Untouched bundled kits stay bundled and take updates. Upgrade path: when `manifest.json` bundleVersion > stored seed version, a boot sync pass adds NEW bundled kits, re-imports existing UNTOUCHED bundled kits through the D8 three-way diff (user edits survive; but by D4 an edited kit is already manual and is never touched), and leaves manual kits alone.
- **D5 — `SkillKit` gets `MarkBundled(path)`; `ClearSource()` refuses bundled kits.** Closes the open NIT-3 from the 002/02 hard review: clearing source on a bundled kit returns a typed failure (the exit is edit-fork per D4, or delete). The import/clear-source API surfaces the same guard.
- **D6 — Bundled kits are not re-importable from the UI.** The Re-import action is hidden for `sourceKind: bundled` (refresh happens automatically at upgrade). Clear-source is likewise hidden. Origin badge shows `bundled` (web badge for it already exists).
- **D7 — Edit-fork trigger scope.** The conversion fires on entry content update (`EntriesApi.Update` → service layer) when the entry is linked to exactly one kit and that kit is `Bundled`. Entries shared with another kit stay shared — the edit mutates the shared row directly (existing sharing semantics; no copy-on-edit for shared entries in 003a).
- **D8 — Uninstall = delete the app folder.** Portable distribution never touches `~/.overmind` on uninstall; data cleanup is documented as manual (delete the folder). The install scripts never write outside the chosen install dir + autostart registration.
- **D9 — Build scripts are shell-only and repo-hosted.** `build/pack.sh` produces the archives from this Linux host (dotnet cross-publish + web `npm run build` output embedded + `bundled/` snapshot copied from a source path pinned in the script). No CI in 003a.
</decisions>

<open-questions>
- None blocking. D5–D9 are agent proposals awaiting the user gate.
</open-questions>

<requirements>
## Distribution (003a)

- R1 — One `pack.sh` (repo `build/`) produces versioned self-contained archives for win-x64, linux-x64, osx-x64, osx-arm64; each archive contains the Host binary, MCP adapter, embedded web assets, `install.ps1`/`install.sh`, and `bundled/` (crew kit snapshot + `manifest.json`).
- R2 — Install scripts unpack to a user-chosen folder and register OS autostart; they fail loudly on missing prerequisites and never write outside their scope (D8).
- R3 — The bundled snapshot is taken from a pinned source path at pack time; `manifest.json` carries `bundleVersion` (monotonic) and the kit folder names.

## Bundled kits as a source kind

- R4 — `SkillKit.MarkBundled(sourcePath)` sets kind/path without `ImportedAtUtc` stamping; `ClearSource()` on a bundled kit is a typed failure (D5); importer commit accepts a bundled mode (same pipeline, different origin stamping).
- R5 — Boot seeding per D3: empty library + no seed flag → import all `bundled/` kits as `Bundled`; stamp flag. Log line records kits imported.
- R6 — Boot upgrade sync per D4: `manifest bundleVersion` > stored → add new kits, D8-re-import untouched bundled kits, skip manual kits; update stored version. Failures never block host startup (log + continue, same swallow-and-log posture as planning sync).
- R7 — Edit-fork per D4/D7: service-level conversion on first exclusive-entry edit; the kit's origin fields clear, badge flips to manual in the UI with no further action.

## UI

- R8 — Kits list/detail: bundled origin badge (exists), no Re-import / no Clear-source actions for bundled kits (D6), bundled kits otherwise indistinguishable (activate, materialize, view).
</requirements>

<non-goals>
- MSIX/pkg proper installers, code signing, store distribution (003b / ops).
- App auto-update (portable: download the next archive).
- Git-linked bundle refresh (git-linked kits are a tombstoned v2 feature, 2026-09-14).
- Linux deb/rpm/flatpak packages.
- Bundled-kit marketplace or multi-bundle selection UI.
</non-goals>

<!--
LIFECYCLE: после status: approved файл ЗАМОРОЖЕН.
Изменение = ре-валидация с пользователем + bump version. Молчаливые правки запрещены.
Читатели: оркестратор, ревьюер. Исполняющий субагент spec НЕ читает —
его контракт — tasks/NN-foo.md.
-->

<log>
- 2026-09-17 — re-imported from BACKLOG/003-bundled-installer/spec.md
</log>
