---
feature: 002
slug: kit-library
status: validated
created: 2026-09-14
validated: 2026-09-17
---

# Plan 002 — Kit library

<approach>
Nine sequential tasks, backend-first, mirroring the 006 shape: spike → origin model → scanner → importer → materializer expansion → re-import → API → web UI → closeout. Each task ends green on its own verification command; the UI is coded against real contracts that already exist. No new NuGet or npm packages (scan/parse is hand-rolled; hashing reuses `ContentHash` from `Infrastructure/Materialization`, platform mapping reuses `PlatformDirectoryMap`/`platforms.json`).

Scope note: this plan covers **002a only**. 002b (`.omkit` export/import) gets its own plan version when scheduled — by design it rides the folder pipeline, so nothing here is throwaway.

Key design consequences from spec.md:

- **Agnostic variant representation.** A platform-agnostic skill gets a single `SkillFile` with a null/"any" platform marker; `SkillMaterializer` must learn to expand agnostic skills into every known platform dir (task 05) — today it only writes platforms explicitly present in the kit's skill files.
- **Failure translation follows the house pattern** (`PlanningException`/`PlanningFailureKind`): a `KitImportException` + `KitImportFailureKind` in `Core/Abstractions`, produced by the scanner/importer services, mapped to ProblemDetails in one Host helper. No raw exceptions to callers.
- **Import writes go through services, not handlers** (same extraction trigger as 006/A1: UI import, re-import and future `.omkit`/bundled import are multiple write paths for the same aggregate). `Infrastructure/Kits/KitScanner` (read-only) and `Infrastructure/Kits/KitImporter` (commit + re-import) own the logic; API handlers only resolve/map.
- **Conflict policy is exactly spec D8** — three-way diff on `ImportedContentHash`; conflicts refuse the entry, keep local, keep the old hash; report everything.
</approach>

<tasks>
- [x] 01-format-spike — spike: SKILL.md frontmatter across Claude / Kimi / Codex / Gemini / Qwen (docs + real examples); findings + normalization verdict in the task `<log>`; output feeds task 05 → tasks/01-format-spike.md. **Done 2026-09-14: verdict PASS-THROUGH + import gates G1–G5; platforms.json gaps found (qwen missing, codex path unverified)**
- [x] 02-origin-model — Core: `KitSourceKind { Manual, Imported, Bundled }` enum, `SkillKit` origin fields (`SourceKind`, `SourcePath`, `ImportedAtUtc`) + `MarkImported`/`ClearSource` methods, `SkillKitEntry.ImportedContentHash`; EF migration; `KitResponse` gains origin fields (additive — Host↔MCP contract ADR holds); verify: build + Core.Tests → tasks/02-origin-model.md. **Done 2026-09-14: 412/412 green, migration verified on legacy DB; hard review PASS-WITH-NITS (`audits/architect-review-2026-09-14-002-02.md`), NIT-1/2 folded into task 06, NIT-3 deferred**
- [x] 03-kit-scanner — `Infrastructure/Kits/KitScanner`: read-only folder scan → scan-result model (kit candidates per spec R3, skills with platform detection via `PlatformDirectoryMap`, `gates/*.md`/`guards/*.md`, per-file issues, name-collision flags vs DB, re-import match by `SourcePath`); ignore-list (`.git`, `node_modules`, `bin`, `obj`, hidden), no symlink follow, scan caps; verify: Infrastructure.Tests with temp-dir fixtures → tasks/03-kit-scanner.md. **Done 2026-09-14: 140/140 green; light review PASS-WITH-NITS (`audits/review-002-03.md`), IMPORTANT-1/2 + NIT-1 fixed same day (nested platform-dir detection, file-symlink skip, Windows-portable tests), remaining nits deferred**
- [x] 04-kit-importer — `Infrastructure/Kits/KitImporter.CommitAsync`: per-kit transaction creating `SkillKit` (imported origin) + entries + per-platform `SkillFile`s + `ImportedContentHash`es (via `ContentHash`); name collision fails that kit only; `ClearSourceAsync` per spec R10; verify: Infrastructure.Tests → tasks/04-kit-importer.md. **Done 2026-09-14: 437/437 green; agnostic = nullable `SkillFile.Platform` (+ migration), imported entries live in find-or-create "Kit Library" project; light review PASS-WITH-NITS (`audits/review-002-04.md`), IMPORTANTs folded into tasks 06/07**
- [x] 05-materializer-agnostic — `SkillMaterializer`: agnostic `SkillFile`s expand to every platform from `PlatformDirectoryMap` (per spike verdict: frontmatter normalization per target platform only where the spike showed real differences, else byte-identical pass-through); drift hashes cover expanded paths; existing materialization tests stay green; verify: Infrastructure.Tests → tasks/05-materializer-agnostic.md. **Done 2026-09-14: pass-through honored (no transforms, current write pipeline kept); qwen added to map+enum; codex `.codex/skills` verified against official docs — NOT a Codex skills path (recorded in task log + BACKLOG deferred); mixed-kit expansion, drift, deactivation, OverwriteBlocked-on-expanded-path tests added, all green; hard review PASS-WITH-NITS (`audits/architect-review-2026-09-14-002-05.md`), NIT-1 deferred, NIT-2 folded into task 06**
- [x] 06-kit-reimport — `KitImporter.ReimportAsync`: three-way diff per spec D8, report model (added/updated/kept-local/conflicted/removed), `ImportedAtUtc` + hash updates, conflicts keep old hash; missing source path → typed failure; verify: Infrastructure.Tests incl. conflict matrix → tasks/06-kit-reimport.md. **Done 2026-09-14: 462/462 green; carry-overs landed (scanner EmptyFile + importer un-skip, DuplicateTarget pin, MarkImported re-stamp + INTEGER roundtrip); EF nav-attach ValueGeneratedOnAdd bug fixed; light review PASS-WITH-NITS (`audits/review-002-06.md`), IMPORTANT-1 deferred by user to hardening, NIT-1/6 folded into task 07**
- [x] 07-import-api — Host: `KitImportApi` (own slice) — `POST /api/kits/import/scan`, `POST /api/kits/import`, `POST /api/kits/{id}/reimport`, `POST /api/kits/{id}/clear-source`; contracts + FluentValidation + `KitImportErrors` mapping (400/404/409); verify: Api.Tests → tasks/07-import-api.md. **Done 2026-09-15: four endpoints + contracts/validators/mapper/`KitImportErrors`; carry-overs landed (delete-guard 409 incl. the "Kit Library" project, case-insensitive library lookup, unique index `SkillKits.Name` with migration dedup + 409 mapping in KitsApi, relativePath selection matching per review-002-08 special assignment); light review PASS-WITH-NITS (`audits/review-002-07.md`), IMPORTANT-1/2 fixed same day; 486/486 green (Core 94 / Infra 175 / Api 158 / Mcp 59)**
- [x] 08-web-import-ui — kits page: "Import from folder" (reuses `browseFolder`), scan-preview dialog (candidate checkboxes, editable names, per-kit issues, re-import flags), per-kit import report view, origin badges in kit list/detail, "Re-import" + "clear source" actions with report surfacing; verify: `npm run lint && npx tsc --noEmit && npm run build` → tasks/08-web-import-ui.md. **Done 2026-09-15: import dialog/wizard/report + origin badges + kit source section (Re-import report with distinct conflict rows, clear source); relativePath contract adopted, live collision highlight; light review PASS-WITH-NITS (`audits/review-002-08.md`), IMPORTANT-1/2 fixed same day; lint 0 errors / 6 known warnings, tsc + static build clean (9 routes); live-Host flow deferred to task 09 e2e**
- [x] 09-closeout-e2e-docs — e2e scenario (fixture folder: two kits + platform-marked + agnostic skills + gates/guards → import → activate/materialize → local edit → file edit → re-import conflict report → clear source), CONTEXT.md kit-library section, ADRs in TECHNICAL-SOLUTIONS.md (snapshot-into-DB; agnostic-by-default + spike verdict; three-way re-import diff; two-phase scan/commit API), BACKLOG status updates, hard architect review → tasks/09-closeout-e2e-docs.md. **Done 2026-09-16: e2e green on a live Host (scan → import → agnostic expansion to all 6 platforms → conflict re-import → clear source); reimport-matching BLOCKER found & fixed same change (`SkillKitEntry.RelativePath`, migration `20260916083250`, pinned by `KitReimportTests`); 495/495 green (Core 94 / Infra 184 / Api 158 / Mcp 59); CONTEXT.md + 4 ADRs in this pass; hard review follows**
</tasks>

<risks>
- **Spike verdict widens task 05** — if agents' frontmatter genuinely differs, normalization code grows (per-platform transforms). Mitigation: normalize only proven differences; pass-through stays the default; spike records exact deltas so 05 has a checklist.
- **Scan false positives** — random folders with a stray `SKILL.md` become "candidates"; huge trees slow the scan. Mitigation: ignore-list + depth-1 candidates + entry/size caps with "truncated" flags in the report.
- **Symlink loops / permission errors mid-scan.** Mitigation: no symlink follow, per-directory try/catch into issue list — scan never throws for content reasons.
- **Agnostic expansion collides with the deferred double-nesting hardening** (materialized `<kit-slug>/<skill-slug>/` layout). Mitigation: task 05 keeps the current layout (drift hashes unchanged in shape); the layout decision stays a separate deferred item — do NOT fix it mid-feature.
- **SourcePath churn** (folder moved/renamed, removable drives). Mitigation: typed 409 + clear-source flow (spec R10); no path heuristics.
- **Existing-kits regression** — origin fields touch `SkillKit` and `KitResponse`. Mitigation: additive-only (nulls for hand-made kits), existing Api/Mcp tests are the safety net.
</risks>

<verification>
- `dotnet build Overmind.sln` + `dotnet test Overmind.sln` — green after every task.
- `npm run lint && npx tsc --noEmit && npm run build` in `web/` — green after task 08.
- Full-feature e2e (task 09): fixture folder → scan preview shows two candidates with correct platform detection and issues → import → kits materialize under multiple platform dirs (agnostic expansion visible) → local UI edit + external file edit → re-import reports conflict on the double-changed entry, updates the file-only-changed one, keeps the local-only one → clear source converts kit to manual.
- Hard architect review on 02 (schema), 05 (materializer) and 09 (closeout); light on the rest.
</verification>

<!--
LIFECYCLE: после status: approved файл ЗАМОРОЖЕН (как spec.md).
Чекбоксы в <tasks> — единственное, что обновляется по ходу работы.
-->

<log>
- 2026-09-17 — re-imported from BACKLOG/002-kit-library/plan.md
</log>
