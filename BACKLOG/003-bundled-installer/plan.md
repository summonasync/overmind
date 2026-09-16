---
feature: 003
slug: bundled-installer
status: validated
created: 2026-09-16
validated: 2026-09-16
---

# Plan 003 — Bundled kits (003a: portable distribution)

<approach>
Six sequential tasks, backend-first, mirroring the 002a shape: origin semantics → boot seed/sync → edit-fork → packaging → web polish → closeout. Every task ends green on its own verification command. The import pipeline, three-way diff, materializer and web origin badges already exist (002a shipped 2026-09-16) — this plan adds the "bundled" source kind behavior, the boot lifecycle, and the distribution packaging. No new NuGet or npm packages.

Key design consequences from spec.md:

- **Bundled is an origin behavior, not a pipeline branch.** `KitImporter` already commits kits with an origin; bundled mode only changes stamping (`MarkBundled`, no `ImportedAtUtc`) — one commit path, two origin flavors.
- **Boot lifecycle is swallow-and-log** (same posture as planning materializer sync): seeding or upgrade-sync failures log and never block host startup.
- **Edit-fork is a service-level hook on the entry update path**, scoped to entries owned exclusively by a bundled kit (spec D7) — no copy semantics, an in-place origin conversion.
- **Packaging is shell-only on this Linux host** (spec D9): cross RID publish works for win/osx without those machines; smoke verification is linux-only, win/osx archives are build-verified (produce + integrity check) but not run-verified — recorded as a known limit in the closeout.

Scope note: this plan covers **003a only**. 003b (MSIX/pkg) gets its own plan when a Windows/macOS host or CI exists.
</approach>

<tasks>
- [x] 01-bundled-origin — Core: `SkillKit.MarkBundled(sourcePath)`, `ClearSource()` refuses bundled kits with a typed failure (`KitImportException` kind, mapped to 409 in `KitImportErrors`); importer commit accepts bundled mode; API clear-source guard test; verify: build + Core.Tests + Api.Tests → tasks/01-bundled-origin.md. **Done 2026-09-16: `KitImportFailureKind.BundledSourceProtected` (8); `MarkBundled` re-stamp semantics pinned (a previously imported kit re-stamped bundled ends with `ImportedAtUtc` null); light review PASS-WITH-NITS (`audits/review-003-01.md`), NIT-1 (re-import bundled guard) folded into task 02, NIT-2 (feature.md refs dup) fixed same day; 505/505 green (Core 100 / Infra 187 / Api 159 / Mcp 59)**
- [x] 02-boot-seed-sync — Infrastructure: `BundledSeedService` + Program.cs boot hook (after `MigrateAsync`): `OvermindPaths.BundledDirectory` (`bundled/` next to the binary) + `manifest.json` (`bundleVersion`, kit names); empty library + no `BundledSeedVersion` AppSetting → import all as Bundled + stamp; `bundleVersion` > stored → upgrade sync (add new kits, D8-re-import untouched bundled, skip manual/edited); all failures swallow-and-log; verify: Infrastructure.Tests incl. seed/skip/upgrade/failure matrices → tasks/02-boot-seed-sync.md. **Done 2026-09-16: `BundledManifest.TryLoad` strict no-throw, `LibraryInUse` stamps nothing, version stamped even on partial failures, stale-SourcePath repoint before sync; carry-over NIT-1 landed (bundled re-import preserves origin via `MarkBundled`; external re-import → typed 409 `BundledReimportBlocked`); hard review PASS-WITH-NITS (`audits/architect-review-2026-09-16-003-02.md`), 6 nits deferred; 522/522 green (Core 100 / Infra 203 / Api 160 / Mcp 59)**
- [x] 03-edit-fork — service-level hook on entry content update: entry linked to exactly one kit and that kit `Bundled` → in-place conversion to `Manual` (clear origin fields, badge flips via existing API); shared entries mutate directly (no fork); verify: Api.Tests (fork fires / shared untouched / manual untouched) → tasks/03-edit-fork.md. **Done 2026-09-16: `SkillKit.ForkToManual()` + `BundledKitForkService` staged on the tracked kit (one transaction with the entry save); light review PASS (`audits/review-003-03.md`, 3 nits deferred); 526/526 green (Core 100 / Infra 203 / Api 164 / Mcp 59)**
- [x] 04-pack-scripts — `build/pack.sh`: cross RID publish (win-x64, linux-x64, osx-x64, osx-arm64) + web static assets + `bundled/` snapshot (crew kit from a pinned source path, overridable) + `manifest.json` (bundleVersion param) + `install.sh`/`install.ps1` inside each archive (autostart registration: XDG / Startup shortcut / launchd plist); real run on this host: linux archive builds, unpacks, Host boots, seed fires; verify: pack.sh green + smoke boot from unpacked archive → tasks/04-pack-scripts.md. **Done 2026-09-16: 4 archives + SHA-256 sidecars (0.3.0), forbidden-path scans clean, linux `SMOKE OK`; light review PASS WITH FINDINGS (`audits/review-003-04.md`), IMPORTANT-1/2 fixed same day (fresh zip stage via `rm -f` + `chmod -R u+w` before deletes), nits folded/deferred; archives repacked + re-verified in the task 06 e2e**
- [x] 05-web-bundled-ux — kits UI: hide Re-import + Clear-source actions for `sourceKind: bundled` (badge already exists); small hint line on bundled kits ("edits fork to a personal copy"); verify: `npm run lint && npx tsc --noEmit && npm run build` → tasks/05-web-bundled-ux.md. **Done 2026-09-16: hint line in the kit source section; Re-import/Clear-source were already `imported`-gated (verified no dead actions elsewhere); light review PASS 0/0/0 (`audits/review-003-05.md`); lint 0 errors / 6 known warnings, tsc + static build clean (9 routes)**
- [x] 06-closeout-e2e-docs — e2e (clean HOME, unpacked archive): seed on first boot → bundled kit visible + activatable → edit entry → kit flips manual → bump bundleVersion + restart → untouched bundled kit updates, manual fork untouched, delete-all + restart → no re-seed (flag holds); CONTEXT.md distribution/bundled sections + entity/API deltas; TECHNICAL-SOLUTIONS.md ADRs (portable-first distribution; bundled = source kind + edit-fork semantics; boot seed/upgrade sync swallow-and-log); BACKLOG sync; hard architect review → tasks/06-closeout-e2e-docs.md. **Done 2026-09-16: e2e all five cells green live from the unpacked linux archive (seed 23 entries → edit-fork → upgrade sync both branches incl. collision skip → no-reseed → corrupt-manifest boot); 526/526 green (Core 100 / Infra 203 / Api 164 / Mcp 59), web lint/tsc/build clean; CONTEXT.md + 3 ADRs in this pass; hard review follows**
</tasks>

<risks>
- **Crew kit snapshot privacy** — the bundle embeds the user's crew kit (agents, SOCIAL_DICTIONARY): fine for the private home-Gitea distribution, but pack.sh takes the source path as a parameter so a curated/scrubbed variant can be packed instead.
- **win/osx archives are build-verified only** (no machines to run them) — macOS tray/autostart specifics may need a fix-forward from 003b; smoke coverage stays linux.
- **Boot-sync vs first user import race** — seeding runs during startup before the API accepts requests; acceptable single-writer window, noted in the service.
- **bundleVersion monotonicity is manual** (pack.sh parameter) — a lower version silently skips sync; the closeout documents the rule.
- **Edit-fork surprises** — a user editing a bundled kit sees it silently become manual; mitigated by the 05 UI hint + origin badge flip; no modal in 003a.
</risks>

<verification>
- `dotnet build Overmind.sln` + `dotnet test Overmind.sln` — green after every task.
- `npm run lint && npx tsc --noEmit && npm run build` in `web/` — green after task 05.
- Full e2e (task 06): unpack a freshly packed linux archive into a clean HOME → boot → bundled kits seeded → activate → edit-fork → upgrade sync → no-reseed guard, all observed via API + files.
- Hard architect review on 02 (boot lifecycle) and 06 (closeout); light on the rest.
</verification>

<!--
LIFECYCLE: после status: approved файл ЗАМОРОЖЕН (как spec.md).
Чекбоксы в <tasks> — единственное, что обновляется по ходу работы.
-->
