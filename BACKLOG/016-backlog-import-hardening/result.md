---
feature: 016
slug: backlog-import-hardening
status: validated
created: 2026-09-19
validated: 2026-09-19
---

# Result 016 — Backlog import pipeline hardening (Mindlash + Squeezr dogfood findings)

<result>
016 backlog-import-hardening — live closeout result (task 05). All acceptance criteria met on 2026-09-19.

## Gates
- `dotnet build Overmind.sln` — 0 warnings, 0 errors.
- `dotnet test Overmind.sln` — 735/735 passed (Core 106, Infrastructure 271, Api 178, Mcp 180). No flakes hit.
- `bash build/smoke.sh dist/overmind-0.1.6-linux-x64.tar.gz` — SMOKE OK: health ok, crew kit seeded (bundled), web UI served, watch guard green (host 28<100, adapter 1<25), clean shutdown.

## Deploy
- Pack: `build/pack.sh --version 0.1.6 --bundle-version 2 --rids linux-x64` → dist/overmind-0.1.6-linux-x64.tar.gz (110M) + sha256.
- ~/.overmind SNAPSHOT before first 0.1.6 boot: `~/.overmind.snapshot-pre-0.1.6` (quiescent copy, host stopped). Both EF migrations landed at first boot: AddFrontmatterKeyRegistry + AddArtifactPreservedFrontmatter. Snapshot verified pre-migration (last migration 20260918113608).
- Untar → ~/apps/overmind-0.1.6-linux-x64, `ln -sfn`, nohup restart. Live health: version 1.0.0+8c0e9e4… = repo HEAD at deploy time.

## Mindlash repro (throwaway profile, ORIGINAL BACKLOG at worktree 8501a30)
Profile knob: `Overmind__DatabasePath` → /tmp/om-repro/overmind.db (fresh schema via CLI MigrateAsync; project row + planning.materializationDefault=on seeded to exercise the materializing path). Live MindlashStudio/Squeezr data untouched; worktree discarded after.

Import report (excerpt, /tmp/om-repro/import1.report == stdout):
```
Imported planning from /tmp/mindlash-orig: 3 features (3 new), 39 artifacts, 7 skipped
skipped: BACKLOG/001-mindlash-studio/tasks/01-wrapper-spike.md (duplicate order 01; 01-report.md imported first)
skipped: BACKLOG/001-mindlash-studio/tasks/20-e2e-validation.md (duplicate order 20; 20-acceptance-checklist.md imported first)
unknown status 'frozen' in BACKLOG/001-mindlash-studio/spec.md, defaulted to draft
unknown status 'frozen' in BACKLOG/001-mindlash-studio/plan.md, defaulted to draft
unknown status 'pending' in BACKLOG/001-mindlash-studio/tasks/37-ux-bring-your-own-qdrant.md, defaulted to todo
unknown status 'idea' in BACKLOG/002-mindlash-agent/feature.md, defaulted to draft
unknown status 'idea' in BACKLOG/003-agent-telegram-bridge/feature.md, defaulted to draft
new key: spec (#f472b6)
preserved: BACKLOG/001-mindlash-studio/tasks/23-bug-builtin-agent-profile-rejected.md (created_during, depends_on, phase)
… (37 preserved lines, one per file with non-structural keys)
created: BACKLOG/001-mindlash-studio/tasks/01-report.md (no created token; fell back to file mtime 2026-09-19)
… (37 created-fallback lines total)
42 files tracked, materialize is a no-op
```

A-findings status (all visibly fixed):
- A1 silent frontmatter loss — FIXED. depends_on/phase/decision/created_during/spec preserved per file (report lines), round-trip into DB PreservedFrontmatter JSON (verified: task 23 → `{"created_during":"task 20 e2e (2026-09-15)","depends_on":"[\\u002216\\u0022, \\"22\\"]","phase":"F"}`), re-emitted on materialize. Registry: 13 keys with colors (12 pre-seeded incl. depends_on #fb923c, phase #22d3ee, created_during #e879f9, decision #f472b6; unseen `spec` auto-registered #f472b6 and reported as `new key`).
- A2 title double-prefix — FIXED. Feature titles in DB: «Mindlash Studio», «Mindlash Agent (internal studio copilot)», «Agent Telegram Bridge (remote attention + reply)» — no «Feature 001 — Feature 001: …» doubling (colon form stripped).
- A3 filename round-trip instability — FIXED (pin). Zero new/untracked files in the worktree after import+materialize (git status: only the 42 canonicalized M files, no ?? lines); re-import of canonical files = «3 features (0 new), 0 artifacts» — byte-stable.
- A4 duplicate NN silent last-wins — FIXED. Both duplicates reported with reasons (see excerpt).
- A5 created default — FIXED. 37 files fell back to file mtime, each listed in the report.
- A6 first materialize needs force — FIXED. Import pre-seeds 42 rows; report ends «42 files tracked, materialize is a no-op»; verified live on the throwaway host: drift [] → materialize force:false (43 files incl. BACKLOG.md, 0 warnings) → drift [].
- A7 false-missing drift — reconciled live, runbook below.

## Live 008 reconcile (A7 runbook)
Symptom: drift showed 12 «missing» for BACKLOG/008-dogfood-hardening/tasks — tracked rows carried historical names (Cyrillic `01-008-01-import-cli-построчный-skipped-лист.md` and generic `01-task-01.md`), disk holds transliterated names; the 12 stale rows accumulated across pre-016 slug-rule eras (the 016 pin stops new orphans but does not dedupe existing ones).

Working sequence (rehearsed end-to-end on a hermetic copy — live DB copy + repo copy — then applied live; both ran identical):
1. GET /api/projects/{id}/planning/drift — take the «missing» items for the feature (12).
2. For each missing row, BEFORE touching anything, verify:
   a. no file exists at the tracked path;
   b. a sibling tracked row for the same feature + same task order (prefix `tasks/NN-`) exists whose file IS on disk;
   c. that sibling's Sha256 equals sha256 of the on-disk bytes (all 9 good rows matched — the disk files are byte-exact renders).
3. Manual SQL step (no API exists for row-level removal): `DELETE FROM PlanningMaterializedFiles WHERE Id = <row>` for the 12 verified-stale rows, one transaction.
4. POST /api/projects/{id}/planning/materialize {"force":false} — rewrites 120 files with zero warnings (good rows own their paths; renders byte-identical). force:true is NOT needed.
5. GET drift again → [] (was 12). Repo git-status shows no materialize-induced changes.

Safety notes: never use ClearAsync/feature-delete for this — it would delete the tracked files from disk. If step 2c ever fails (sibling hash ≠ disk), STOP — the disk content is ahead/behind the DB and needs an import pass first, not a reconcile.

## CLI proof (packed 0.1.6 binary, live host up on :4761)
`Overmind__DatabasePath=/tmp/om-cli-proof2/overmind.db ~/apps/overmind/host/Overmind.Host --import-backlog /tmp/mindlash-orig --report …/report.txt`
- exit 0, elapsed 1s, no lingering process, stderr empty;
- ~/.overmind/host.port mtime unchanged (22:54:07 before and after);
- report file byte-identical to stdout (diff clean), content matches the repro run deterministically;
- live host healthy throughout; live DB project list unchanged (CLI stayed on its throwaway profile).

## Observations (non-blocking)
- Pre-existing 0-byte overmind.db file (e.g. from an interrupted copy) makes the CLI fail loudly with «SQLite Error 8: attempt to write a readonly database», exit 1. Cosmetic robustness nit — the runbook says: point the knob at a nonexistent path or a valid DB.
- Mindlash `status: frozen` (their spec-approved marker) maps to draft with an in-report note (not silent). A future mapping frozen→validated would be a one-line addition if their convention is adopted.
- 0.1.6 version stamp = 8c0e9e4 (HEAD at pack time); the closeout commit lands after deploy, so the stamp lags final HEAD by the closeout commit — inherent to the task order (deploy → ceremony → commit).
</result>