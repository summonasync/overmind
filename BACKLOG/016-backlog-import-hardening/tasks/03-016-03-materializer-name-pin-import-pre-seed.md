---
feature: 016
task: 03
slug: backlog-import-hardening
status: done
review: light
created: 2026-09-19
---

# 016/03 — Materializer name pin + import pre-seed

<goal>
Task filenames are stable: after first materialization the tracked RelativePath wins over H1-derived regeneration (title edits and slug-rule changes no longer create missing+untracked drift pairs), and the importer pre-seeds PlanningMaterializedFile rows so the first materialize after an import is a byte-stable no-op (no force needed).
</goal>

<context>
Feature 016, third task. Dogfood findings A3/A6/A7: filenames regenerate from the task H1 every run — the 008 slug-rule fix (transliteration) orphaned 12 tracked names on disk (false "missing" while files exist); first materialize after import requires force with no guidance. The executing subagent reads ONLY this file.

Materializer: src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs (~496 lines); tracked rows table PlanningMaterializedFiles (ProjectId, FeatureId, RelativePath, Sha256, MaterializedAt). Importer: BacklogImporter.cs (same folder).
</context>

<scope>
- Name pin (A3/A7): when materializing an artifact that ALREADY has a tracked PlanningMaterializedFiles row, reuse the tracked RelativePath — do NOT regenerate from the current H1/title. New artifacts (no tracked row) get the current naming rule. Drift detection treats the pinned name as canonical: a title change after first materialize produces NO missing/untracked pair. Tests: materialize → rename title → materialize again: same path, clean drift.
- Preserved-frontmatter emit (if task 02 stored-only): preserved frontmatter keys are written back into the emitted file's frontmatter block, keys ordinal-sorted, so import → materialize round-trips them byte-stably. (If 02 already did emit, verify + test here instead.)
- Pre-seed (A6): after a successful import, for every file the importer wrote (or verified identical), insert PlanningMaterializedFile rows with path + sha256 of the on-disk bytes. The sha MUST equal what the materializer would emit for that artifact (byte-stable emit) — otherwise the first materialize reports a false modified-wave; acceptance pins this. First materialize after import then completes with force:false and zero changes.
- Import report gains a closing next-step line only if something still needs it (e.g. "N files tracked, materialize is a no-op"); no more "run with force" guidance needed.
</scope>

<anti-scope>
- No rename-detect by content hash (Deferred candidate, explicitly out)
- No adopt/rename UI flow
- No CLI changes (task 04), no registry work (task 01/02)
</anti-scope>

<constraints>
- .NET 10 / C# 14; no new NuGet packages; no comments
- Byte-stability is the acceptance core: emit path and pre-seed path must hash identical bytes (BOM/newline handling identical)
- dotnet build + dotnet test green
</constraints>

<acceptance-criteria>
- [ ] Title rename after first materialize keeps the pinned filename; drift clean (test)
- [ ] Slug-rule-sensitive names (the 008 Cyrillic→transliteration case) no longer produce missing+untracked pairs for already-tracked files (test or documented proof)
- [ ] Import → materialize(force:false) = no-op, zero modified/missing/untracked (test)
- [ ] Preserved frontmatter keys round-trip onto disk (test)
- [ ] dotnet build + test green
</acceptance-criteria>

<refs>
- @src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs — naming + drift + emit
- @src/Overmind.Infrastructure/Planning/BacklogImporter.cs — pre-seed point
- tasks/02-importer-fidelity.md — preserved-frontmatter storage (emit split noted there)
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-19 — Created from validated plan 016
- 2026-09-19 — Started: name pin + pre-seed (coder dispatched)
- 2026-09-19 — Coder done: name pin + preserved emit + pre-seed; 725/725 green; light review dispatched
- 2026-09-19 — Review PASS-WITH-NITS (audits/review-016-03.md) with 2 IMPORTANTs — fix loop done by same coder: seeding moved inside ImportAsync transaction + FeatureId backfill (legacy null rows too), flipped test now asserts no-duplicate; NIT-6/8 fixed, NIT-3 drive-by; 735/735 green
</log>
