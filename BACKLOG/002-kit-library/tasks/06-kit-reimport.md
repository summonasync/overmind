---
feature: 002
task: 06
slug: kit-reimport
status: done
review: light
created: 2026-09-14
---

# 002/06 — KitImporter: re-import (three-way update)

<goal>
`KitImporter.ReimportAsync(kitId)` re-scans the kit's `SourcePath` and updates the kit in place, returning a full report: added / updated (new `EntryVersion`) / kept-local / conflicted (refused) / removed. Conflicts never overwrite local work and keep the old `ImportedContentHash` so the conflict re-surfaces next time. Missing source path → typed failure.
</goal>

<context>
Imported kits remember their source (task 02). Re-import is the manual refresh story (git auto-update is out of scope). The diff is three-way per entry, keyed on `ImportedContentHash` (H = hash at last import): file hash == H ⇒ no upstream change → keep local edits (kept-local); entry unchanged since import (current content hash == H) and file changed ⇒ take file, snapshot new `EntryVersion` (updated); both changed ⇒ conflict — refuse that entry, keep local, keep old hash, report; new file ⇒ new entry (added); file gone since last import ⇒ entry removed from kit and deleted (kit-owned), reported (removed). Executor reads ONLY this file.
</context>

<scope>
- `src/Overmind.Infrastructure/Kits/KitImporter.cs` — add `ReimportAsync(kitId)` + report model (per-entry outcome + counts)
- Re-scan via `KitScanner` (task 03); match file entries to kit entries by relative path within the candidate
- **Review carry-over from 002/04 (`audits/review-002-04.md`, IMPORTANT-2 — root cause is scanner-side):** `KitScanner` marks empty `gates/*.md`/`guards/*.md` files as handled without an `EmptyFile` issue, so the preview shows them as importable while the importer silently drops them. Fix in the scanner (one condition: empty gate/guard file → `EmptyFile` issue, not a gate/guard entry), then remove the importer's silent-skip workaround and add regression tests on both sides
- **Review carry-over from 002/05 (`audits/architect-review-2026-09-14-002-05.md`, NIT-2 test part):** pin the deliberate `DuplicateTarget` behavior with a test — a hand-mixed kit (agnostic + explicit `SkillFile` with the same `RelativePath` in one skill, constructible via `EntriesApi`) throws `DuplicateTarget` at materialization, no silent overwrite
- `EntryVersion` snapshot on update follows the existing `ContextEntry.UpdateContent` behavior — reuse it, don't hand-roll versioning
- `ImportedAtUtc` and touched `ImportedContentHash`es update; conflicted entries keep the old hash
- Re-import on a manual kit (no `SourcePath`) or a missing path → `KitImportException` with distinct kinds (no-source vs source-missing)
- `ImportedContentHash` on removed entries' rows go away with the entry; removals are reported
- Tests: `tests/Overmind.Infrastructure.Tests/` — the full conflict matrix (file-only changed, entry-only changed, both changed, unchanged, added, removed), hash bookkeeping on conflicts, typed failures, `EntryVersion` created on updates; PLUS two review carry-overs from 002/02 (`audits/architect-review-2026-09-14-002-02.md`, NIT-1/NIT-2): a Core.Tests case pinning that `MarkImported` on an already-imported kit re-stamps silently (load-bearing for re-import), and an Infrastructure roundtrip test pinning the INTEGER enum persistence convention for `KitSourceKind`
</scope>

<anti-scope>
- No materializer changes (task 05); re-import does NOT trigger materialization sync (that's an activate/drift concern, unchanged)
- No API endpoints (task 07), no web UI (task 08)
- No auto-merge or conflict-resolution UI hooks — refuse + report only
- No changes to `EntryVersion` semantics or the version-numbering convention (known deferred item)
</anti-scope>

<constraints>
- No new NuGet packages
- `dotnet build Overmind.sln` + `dotnet test tests/Overmind.Infrastructure.Tests` green
- No comments in code; no auto-commits
</constraints>

<acceptance-criteria>
- [ ] Full conflict matrix covered by tests with correct outcomes per cell
- [ ] Updated entries gain an `EntryVersion`; conflicted entries keep local content and old `ImportedContentHash` (conflict re-reports on next re-import)
- [ ] Removed files remove their kit-owned entries; added files create entries with hashes
- [ ] Re-import on manual kit and on missing path raise distinct typed failures
- [ ] `ImportedAtUtc` bumps on every re-import run
- [ ] Build + Infrastructure.Tests green
</acceptance-criteria>

<refs>
- @../spec.md — D8, R8–R10 (reviewer reads, not executor)
- @../plan.md — task 06 row
- tasks/02-origin-model.md — origin fields + `ImportedContentHash`
- tasks/03-kit-scanner.md — re-scan source
- tasks/04-kit-importer.md — same service, commit flow
</refs>

<obstacles>
- EF Core discovery quirk (cost a debug round): entities added to a nav collection of a tracked non-Added principal are assumed to already exist in the DB when the key is ValueGeneratedOnAdd (`SkillFile.Id`) — tracked as Modified → UPDATE against a missing row → DbUpdateConcurrencyException. Fix: `ResyncSkillFiles` adds each new `SkillFile` via `_db.SkillFiles.Add(file)` BEFORE attaching it to the entry nav. (`EntryVersion.Id`/`SkillKitEntry` composite key are `ValueGeneratedNever` and track as Added via nav — no special handling needed, pinned by tests.)
- Kit links are snapshotted once (`importedLinks`) before the diff passes: links created by adds during the run have a null `Entry` nav and must not be re-matched or re-removed by later passes.
- Gate/guard identity is title-based (the entry model stores no source path for rules). An upstream H1 rename surfaces as removed+added; duplicate titles within one kind match 1:1 in deterministic order (by EntryId / relative path).
- Removed entries: the ContextEntry row is deleted only when no OTHER kit links it (`KitsApi.AddEntry` allows cross-kit links; a manual kit referencing an imported entry must not lose it). Otherwise only this kit's link is removed. Pinned by `Reimport_RemovedEntryLinkedElsewhere_RemovesLinkKeepsEntryRow`.
- Empty/whitespace upstream skill is refused as Conflicted (local content + old hash kept, re-surfaces next run) instead of crashing the run on `UpdateContent`'s whitespace guard; a brand-new empty skill is likewise refused rather than added. Whitespace gates/guards can no longer reach the importer at all (scanner fix), the in-importer check is belt-and-braces.
- Skill match key on the kit side is reconstructed as `{skillsDir}/{title}` from `SkillFiles[].Platform` via `PlatformDirectoryMap` (agnostic ⇒ bare title), compared separator-normalized and case-insensitively — disambiguates same-name platform variants (`.claude/skills/deploy` vs `.gemini/skills/deploy`) that title matching would merge.
- `KitImporter` ctor gained `PlatformDirectoryMap` (skill-key reconstruction); importer is not in Host DI yet (task 07), only `KitImporterTests`/`KitReimportTests` construct it — both updated.
</obstacles>

<log>
- 2026-09-14 — done. `ReimportAsync` three-way diff (added/updated/kept-local/conflicted/removed + counts) with per-entry report (`KitReimportReport`/`KitReimportEntryResult`/`KitReimportOutcome`); conflicts keep local content + old `ImportedContentHash` and re-report; updates snapshot via `ContextEntry.UpdateContent` + resync `SkillFiles`; removals delete kit-owned entry rows (guarded when linked elsewhere); `MarkImported` re-stamp bumps `ImportedAtUtc`; `NoSource`/`SourceMissing` failure kinds. Scanner IMPORTANT-2 fix: empty/whitespace gates+guards → `EmptyFile` issue, no gate entry; importer silent-skip removed; regression tests both sides (scanner test new, `Commit_EmptyGateFile_SkipsThatGate` still green end-to-end). 002/05 NIT-2: `DuplicateTarget` pinning test. 002/02 NIT-1/NIT-2: `MarkImported` re-stamp Core test + `KitSourceKind` INTEGER roundtrip (`KitOriginPersistenceTests`, raw `SqlQuery` value + `typeof` = integer). 21 new tests (15 reimport matrix + scanner + materialization + 2 persistence + Core re-stamp); suite 462/462 green (Core 94 / Infra 174 / Api 135 / Mcp 59); scoped `dotnet format whitespace --verify-no-changes` clean. Files: `src/Overmind.Core/Abstractions/KitImport{FailureKind,Exception}.cs`, `src/Overmind.Core/Entities/SkillKitEntry.cs` (hash writer), `src/Overmind.Infrastructure/Kits/Kit{Importer,Scanner}.cs` + 3 new report models, `tests/Overmind.Infrastructure.Tests/KitReimportTests.cs` (new), `KitScannerTests.cs`, `KitImporterTests.cs`, `MaterializationTests.cs`, `KitOriginPersistenceTests.cs` (new), `tests/Overmind.Core.Tests/Entities/EntityFactoryTests.cs`.
</log>
