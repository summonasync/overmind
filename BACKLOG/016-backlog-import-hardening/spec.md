---
feature: 016
slug: backlog-import-hardening
status: validated
created: 2026-09-19
validated: 2026-09-19
---

# Spec 016 — Backlog import pipeline hardening (Mindlash + Squeezr dogfood findings)

<decisions>
- A3+A7 (filename instability) are one root cause: the materializer regenerates task filenames from the current H1 on every run, so any slug-rule change or title edit orphans the tracked name (missing + untracked pairs, observed live on BACKLOG/008 with 12 false-missing). Fix: filename is PINNED after first materialization — the tracked row's RelativePath wins over regeneration; a title/slug change keeps the old filename until an explicit adopt/rename flow (out of scope here). Rename-detect by content hash stays a Deferred candidate, not this feature.
- A4 (duplicate NN- prefixes): silent last-wins becomes a loud skip — the importer reports every duplicate order number and skips the losers, never dropping content silently.
- A1 (unknown frontmatter keys) — REPLACED by user decision 2026-09-19: NOTHING is dropped. Frontmatter keys are flexible metadata — the importer round-trips ALL keys. A pre-seeded key registry (dictionary with colors, e.g. status/review/version/created + the Mindlash set depends_on/phase/decision/created_during) defines known keys with assigned colors; an unseen key is AUTO-CREATED in the registry with a random pretty color and reported to the user ("new keys added: ..."). Registry colors render wherever keys surface in the UI (import report view, artifact views).
- A5 (created default): fall back to the source file's mtime before falling back to import day, and list affected files in the report.
- A2 (title double-prefix): ResolveTitle strips both em-dash and colon forms («Feature NN — X», «Feature NN: X»).
- A6 (first materialize needs force): the import report ends with an explicit next-step line ("run materialize --force" or the UI path); pre-seeding PlanningMaterializedFile rows from the importer is the cheaper fix and is the one we take — after import, the on-disk files ARE the tracked baseline, so the first materialize is a no-op.
- B2 (import CLI boots as host) is fixed as run-and-exit: the import command does its work against the DB and exits; B1 (host.port overwrite by the import instance) disappears with it — no Kestrel boot, no port file write. If the import needs the DB lock semantics the host provides, it takes the same SQLite path directly, not a web host.
- B3 (report to file): import CLI gains --report <path> writing the same report it prints.
</decisions>

<open-questions>
- none
</open-questions>

<requirements>
Importer (src/Overmind.Infrastructure/Planning/BacklogFeatureImport.cs + friends):
- ResolveTitle strips both separator forms (em-dash and colon), pinned by tests for «Feature 001 — X» and «Feature 001: X».
- Duplicate task order numbers within one feature: report + skip losers, never silent overwrite; test with two same-NN files.
- Unknown frontmatter keys: ALL keys round-trip (import stores them, materialize writes them back). Storage on the artifact is additive (new optional column — e.g. a JSON map of preserved frontmatter); the additive-only Host↔MCP contract allows added fields.
- Key registry: new persisted dictionary (key name → color), pre-seeded with the known ceremony keys + the Mindlash set (depends_on, phase, decision, created_during); unseen keys auto-register with a random color from a curated palette and are listed in the import report ("new keys added: ..."); colors surface in the UI where keys are shown.
- created fallback chain: explicit created token → source file mtime → import day; report lists files that fell back; test.
- After a successful import, pre-seed PlanningMaterializedFile rows for every written file (path + sha256), so the first materialize is a no-op and the report can say so.

Materializer (src/Overmind.Infrastructure/Planning/PlanningMaterializer.cs):
- Task filename pinned after first materialization: if a tracked row exists for the artifact, its RelativePath is reused; regeneration only for artifacts without a tracked row. Drift detection must not flag the pinned name as missing when the H1/title changed. Tests: rename-title-after-first-materialize → same filename, no missing/untracked pair.

Import CLI (the --import-backlog path in Overmind.Host or wherever it lives — find it):
- Run-and-exit: performs the import against the SQLite store and exits with the report code; no Kestrel, no tray, no host.port write (B1 dies with it).
- --report <path>: the full import report duplicated to a file (same text as stdout).
- Exit codes: 0 success, non-zero on failures; report distinguishes imported / skipped / failed per file as it does today.
</requirements>

<acceptance-ideas>
- Re-run the MindlashStudio repro (3 features / 37 tasks, /mnt/Ubuntu-Dev/Projects/AI-Tools/MindlashStudio, before/after commits 8501a30..392b339): zero silent drops, duplicates reported, double-prefix gone, created dates from mtimes, first materialize a no-op without force.
- The live Overmind 008 drift case: after the pin fix + a one-time adopt/reconcile, drift for BACKLOG/008-dogfood-hardening/tasks shows no false missing for files that exist under transliterated names (the reconcile itself is a manual/adopt step — document the command in the result artifact).
- Import CLI: `overmind --import-backlog <path> --report /tmp/r.txt` exits after printing; no host.port write (file mtime unchanged); report file == stdout content.
- dotnet test green incl. all new pinning tests; no new NuGet packages.
</acceptance-ideas>
