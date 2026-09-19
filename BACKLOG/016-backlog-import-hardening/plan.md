---
feature: 016
slug: backlog-import-hardening
status: validated
created: 2026-09-19
validated: 2026-09-19
---

# Plan 016 — Backlog import pipeline hardening (Mindlash + Squeezr dogfood findings)

<approach>
Storage-first: the key registry (new entity + migration) is the foundation A1 round-tripping hangs on, so it lands first alone. Then the importer fidelity batch (double-prefix strip, loud duplicates, mtime fallback, unknown-key round-trip via the registry + preserved-frontmatter column) as one coherent importer change. Then the materializer name-pin plus import pre-seeding — the two halves of "files never go false-missing". Then the CLI becomes run-and-exit with --report (B1 dies structurally). Closeout re-runs the Mindlash repro on a throwaway profile and reconciles the live 008 drift case. UI note: no existing surface renders frontmatter keys today — 016 ships the registry + additive API exposure; colored chips land where keys first surface (012 color work or later).
</approach>

<tasks>
- [ ] 01-key-registry — new FrontmatterKeyRegistry entity (Id, Name unique, Color) + EF migration; curated palette + deterministic-random assignment on auto-register (hash the name → palette index, stable across runs); seed with the ceremony keys (status, review, version, created, feature, task, slug, validated) + the Mindlash set (depends_on, phase, decision, created_during) each with a fixed pleasant color; registry service (GetOrRegister returns color + isNew flag); additive DTO exposure on the import report (newKeys: [{name, color}]) and a GET endpoint for the registry (future UI) → tasks/01-key-registry.md. Verify: dotnet test Overmind.sln
- [ ] 02-importer-fidelity — BacklogImporter/BacklogMarkdown/BacklogNaming: ResolveTitle strips «NN — X» AND «NN: X»; duplicate task order numbers → losers skipped with a report entry (no silent last-wins); created fallback chain explicit-token → file mtime → import day with report listing; PlanningArtifact gains a preserved-frontmatter JSON column (additive migration) — importer stores every key it doesn't map structurally, materializer writes them back on emit; every auto-registered key lands in the registry and the report's newKeys list → tasks/02-importer-fidelity.md. Verify: dotnet test Overmind.sln incl. new pinning tests for all four behaviors
- [ ] 03-name-pin-preseed — PlanningMaterializer: task filename pinned after first materialization (tracked RelativePath wins over H1 regeneration; drift must not flag the pinned name as missing when the title changed); BacklogImporter pre-seeds PlanningMaterializedFile rows (path + sha256) for every file it writes → first materialize is a no-op, force unneeded; tests: rename-title-after-first-materialize keeps filename + no missing/untracked pair; import → materialize round trip is a byte-stable no-op → tasks/03-name-pin-preseed.md. Verify: dotnet test Overmind.sln
- [ ] 04-import-cli-run-exit — BacklogImportCommand + Program.cs wiring: --import-backlog runs the import against the SQLite store and EXITS (no Kestrel, no tray, no host.port write); --report <path> duplicates the report to a file identical to stdout; exit codes 0/non-zero preserved; smoke-level proof: run import against a throwaway profile, assert host.port mtime unchanged and process exits → tasks/04-import-cli-run-exit.md. Verify: dotnet test + the smoke proof
- [ ] 05-live-closeout — full gates; Mindlash repro rerun on a THROWAWAY ~/.overmind profile copy (never the live DB): zero silent drops, duplicates reported, no double-prefix, mtime dates, first materialize no-op; live Overmind: reconcile the 008 drift (document the exact adopt/materialize command sequence in the result artifact, run it, drift clean); docs touch (CONTEXT.md import section if it claims otherwise); pack 0.1.6 + deploy ritual; materialize BACKLOG, result artifact with before/after evidence, close feature, commit+push → tasks/05-live-closeout.md. Verify: live evidence + dotnet test Overmind.sln
</tasks>

<risks>
- Preserved-frontmatter JSON column: serialization must be ordered/stable or round-trips show phantom diffs in drift (serialize keys in a fixed order — registry order or ordinal).
- Pre-seeding sha256 must hash EXACTLY what materialize would write (byte-stable) or the first materialize reports a wave of false "modified" drift instead of being a no-op — task 03 acceptance pins byte-stability.
- Run-and-exit CLI: the host boot currently does import THEN serving in one composition — splitting must keep the boot-import path (host startup import of BACKLOG/) intact; the CLI flag becomes a separate thin path, not a refactor of boot.
- Mindlash repro mutates their project data if run against the live DB — throwaway profile only (OVERMIND_HOME or equivalent env; find the actual profile-path knob, do not assume).
- EF migration on the live dogfood DB: additive (new table + new nullable column), auto-applied at boot — snapshot ~/.overmind before the first 0.1.6 boot (ritual exists).
</risks>

<verification>
- dotnet build Overmind.sln && dotnet test Overmind.sln — green incl. all new pinning tests (baseline 677 + new)
- bash build/smoke.sh — green (watch guard included)
- Live: Mindlash repro clean on throwaway profile; 008 drift reconciled and verified empty; import CLI exits without touching host.port; 0.1.6 live
- BACKLOG/016-backlog-import-hardening materializes clean
</verification>
