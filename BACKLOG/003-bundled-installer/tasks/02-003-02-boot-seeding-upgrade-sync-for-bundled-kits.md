---
feature: 003
task: 02
slug: bundled-installer
status: done
review: hard
created: 2026-09-16
---

# 003/02 — Boot seeding + upgrade sync for bundled kits

<goal>
Host startup materializes the bundled set into the library: first run on an empty library imports every bundled kit as `Bundled` (once, guarded by an `AppSetting` flag); a newer `manifest.json` bundleVersion triggers an upgrade sync (add new kits, three-way-diff-update untouched bundled kits, never touch manual kits). All failures are swallowed and logged — boot never breaks because of bundles.
</goal>

<context>
The distribution ships a read-only `bundled/` folder next to the app binary (layout: a multi-kit folder the 002 scanner already understands + `manifest.json` with `bundleVersion` and kit names). This task owns the boot lifecycle that consumes it, built on the bundled origin semantics from task 01 and the re-import diff from 002. Register the service in Infrastructure DI; call it from `Program.cs` right after `MigrateAsync`, before the API starts accepting requests. Manifest shape: `{ "bundleVersion": <int>, "kits": ["<folder-name>", ...] }`. Executor reads ONLY this file.
</context>

<scope>
- `src/Overmind.Host/OvermindPaths.cs` — `BundledDirectory` (next to the app binary; check how the Host resolves its own executable/content root today and follow that)
- **Carry-over from the 003/01 review (`audits/review-003-01.md` NIT-1):** `KitImporter.ReimportAsync` currently re-stamps origin via `MarkImported` — for a bundled kit that silently flips it to `Imported`. This task owns the bundled lifecycle, so: (a) re-import must PRESERVE the bundled origin (bundled kit stays `Bundled` through the upgrade diff — stamp the source path, keep `ImportedAtUtc` semantics per `MarkBundled`), and (b) an EXTERNAL re-import call on a bundled kit (`POST /api/kits/{id}/reimport`) is refused with a typed failure mapped to 409 (refresh is upgrade-sync's job per spec D6). Pin both with tests.
- `src/Overmind.Infrastructure/Kits/BundledSeedService.cs` (or the house-preferred location/naming) — read manifest; if the bundled folder is absent/invalid → log once, no-op
  - Seed: `SkillKits` empty AND no `BundledSeedVersion` AppSetting → import all manifest kits through `KitImporter` in bundled mode; stamp the setting
  - Upgrade: stored version < manifest version → for each manifest kit: kit with that name missing → import; existing kit `Bundled` and untouched (its entries carry import hashes) → re-import through the 002 diff; existing kit `Manual`/`Imported` → skip untouched; then stamp the new version
  - Every failure path: catch, log (structured, Serilog), continue — never propagate to startup
- `AppSetting` key: `BundledSeedVersion` (int as string — follow how AppSetting values are stored/read elsewhere)
- `Program.cs` boot hook
- Tests (Infrastructure.Tests, temp-dir fixtures like the 002 suites): seed on empty library; no-op on non-empty library; no re-seed when user deleted all kits (flag holds); upgrade adds a new kit; upgrade D8-updates an untouched bundled kit; upgrade skips a manual kit with a colliding name; corrupt/missing manifest → logged no-op; a failing kit import does not abort the rest nor throw out of the service
</scope>

<anti-scope>
- No edit-fork behavior (task 03); no UI; no packaging (task 04)
- No changes to the 002 diff/importer algorithms beyond what bundled mode requires
- No scheduling/loops — boot-time only
</anti-scope>

<constraints>
- No new NuGet packages; no comments; English-only messages
- `dotnet build Overmind.sln` + `dotnet test Overmind.sln` green
- No auto-commits
</constraints>

<acceptance-criteria>
- [x] First boot on empty library seeds all bundled kits as `Bundled` (pinned)
- [x] Flag guards re-seeding after user deletes kits (pinned)
- [x] Upgrade sync: new kit added, untouched bundled kit updated via the three-way diff, manual kit untouched (pinned)
- [x] Manifest missing/corrupt and per-kit failures log and never break boot (pinned)
- [x] Full suite green
- [x] Carry-over (a): bundled re-import preserves `Bundled` origin — `MarkBundled` restamp, `ImportedAtUtc` stays null (pinned)
- [x] Carry-over (b): external `POST /api/kits/{id}/reimport` on a bundled kit returns typed 409 `BundledReimportBlocked` (pinned)
</acceptance-criteria>

<refs>
- @../spec.md — D3, D4, R5, R6 (reviewer reads, not executor)
- @../plan.md — task 02 row
- tasks/01-bundled-origin.md — the semantics this rides on
- `src/Overmind.Infrastructure/Kits/KitImporter.cs` — bundled mode + re-import diff
</refs>

<obstacles>
- `KitsUpdated` in `BundledSeedResult` counts kits that ran the bundled refresh diff, not kits whose
  content changed — an unchanged bundled kit still counts as updated (it went through re-import).
  Pinned this reading in `Run_NewerManifest_AddsNewKit`; rename if the reviewer finds it misleading.
- EF migration files fail `dotnet format --verify-no-changes` (IDE0161 block-scoped namespaces) —
  pre-existing generated code, untouched by this task; all 9 violations live in `Migrations/`.
</obstacles>

<log>
2026-09-16 — implemented boot seeding + upgrade sync for bundled kits.

Production:
- `OvermindPaths.BundledDirectory` = `AppContext.BaseDirectory/bundled` (next to the app binary).
- `BundledManifest` — strict `TryLoad` of `manifest.json` (`bundleVersion` int >= 1, `kits` string
  array, non-empty names); IO/JSON failures return false, no throw.
- `BundledSeedService.RunAsync` decision flow: no manifest file → `NoBundle` log-once no-op; invalid
  manifest → `InvalidManifest`; no `BundledSeedVersion` AppSetting + empty `SkillKits` → seed all
  manifest kits via `KitImporter.CommitAsync(bundled: true)`, stamp version; no setting + non-empty
  library → `LibraryInUse` no-op (nothing stamped, so a truly fresh library still seeds); stored
  version < manifest version → upgrade (missing kit → import, `Bundled` kit → refresh via
  `ReimportAsync(bundled: true)`, `Manual`/`Imported` colliding name → skip + log), stamp version;
  stored >= manifest → `UpToDate`. Per-kit and whole-run failures are caught, logged (structured
  ILogger), never propagated — `OperationCanceledException` rethrow mirrors `BacklogImportCommand`.
  Before refreshing a bundled kit, a stale `SourcePath` (relocated install) is repointed at the
  current bundle location via `MarkBundled` restamp, so a moved bundle does not stick the kit on
  `SourceMissing` forever.
- Carry-over from review-003-01 NIT-1: `KitImporter.ReimportAsync` gained `bool bundled = false`
  (same shape as `CommitAsync`). External mode throws typed `KitImportException.BundledReimportBlocked`
  (kind = 9, 409 arm in `KitImportErrors`) when the kit is `Bundled`; bundled mode finishes with
  `MarkBundled(sourcePath)` — origin stays `Bundled`, `ImportedAtUtc` stays null per `MarkBundled`
  semantics. Single API call site updated to `bundled: false` named arg (positional `ct` would not
  compile — loud, not silent).
- DI: `BundledSeedService` registered in Host `DependencyInjection.cs` with
  `OvermindPaths.BundledDirectory` injected (Infrastructure stays Host-agnostic). `Program.cs` runs
  it inside the existing boot scope right after `MigrateAsync`, before `BacklogImportCommand` and
  `app.StartAsync()`.
- No migration needed: `AppSettings` table pre-exists; version stored as invariant-culture string
  through `AppSettingsService`.

Tests (Infrastructure.Tests temp-dir fixtures + Api.Tests):
- `BundledSeedServiceTests` (14 cases): seed-on-empty stamps `Bundled`/path/null-stamp + hashes;
  non-empty library no-op without stamping; no re-seed after user deletes all kits (flag holds);
  upgrade adds new kit; upgrade D8-updates untouched bundled kit via diff (content + hash + origin);
  upgrade keeps local edits through diff; upgrade skips manual colliding kit; missing bundle no-op;
  invalid manifest Theory (garbage JSON / missing kits / version 0 / blank name); failing kit import
  continues the rest and stamps; relocated bundle repoints source before sync.
- `KitReimportTests` +2: bundled-mode re-import preserves origin; external call on bundled kit
  throws `BundledReimportBlocked` and keeps origin.
- `KitImportApiTests` +1: `POST /api/kits/{id}/reimport` on bundled kit → 409, title
  `BundledReimportBlocked`, origin survives server-side.

Decisions:
- Eligibility for upgrade refresh is `SourceKind == Bundled` only — the three-way diff itself
  protects locally edited entries (KeptLocal/Conflicted), no extra "untouched" pre-check; hashes
  are written by bundled seeding by construction.
- Version stamped even when some kits fail (seed or upgrade) — per-kit failures are logged and
  won't retry-loop on every boot; the next version bump is the recovery path.
- `LibraryInUse` does not stamp the flag: a library that later becomes truly empty still seeds.

Verification (worktree root):
- `dotnet build Overmind.sln` — Build succeeded, 0 warnings, 0 errors.
- `dotnet test Overmind.sln` — 522/522 green: Core 100, Infrastructure 203 (+16), Api 160 (+1),
  Mcp 59. Zero failures, zero skips.
- `dotnet format Overmind.sln --verify-no-changes` — clean except 9 pre-existing IDE0161 in
  generated `Migrations/` files (untouched by this task).
</log>
