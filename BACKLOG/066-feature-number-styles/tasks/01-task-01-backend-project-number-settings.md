---
feature: 066
task: 01
slug: feature-number-styles
status: done
review: light
created: 2026-10-04
brief: Backend: numberStyle+numberPrefix fields, migration, settings API, validation + tests
---

# Task 01 — Backend: project number settings

<goal>
Per-project number style + prefix, stored and validated.
</goal>

<scope>
- Project.numberStyle (hash default | key | plain) + numberPrefix (required non-empty when key; [A-Z][A-Z0-9_]{1,15}); migration AddProjectNumberStyle (zero-impact defaults).
- Settings update path (project settings API) + validation errors; contract pins; unit tests on validation/defaults.
</scope>

<acceptance-criteria>
- [x] Migration up+down green; defaults zero-impact — pinned.
- [x] Validation: key without prefix rejected, bad prefix charset/length rejected — pinned.
- [x] Settings API round-trip — pinned.
</acceptance-criteria>

<refs>
- Spec e3f93cc8; plan 29745c88 (task 1).
</refs>

<obstacles>
- FluentValidation `.Must()` does not skip null inputs (unlike built-in validators): the first NumberStyle rule 400'd every legacy PUT that omitted the field. Fixed with an explicit `style is null ||` guard in the predicate. Any future optional-field Must rule here needs the same null guard.
- Conservative semantics chosen (spec silent): (1) PUT without numberStyle/numberPrefix leaves numbering untouched (backward compatible for existing web/agent clients; task 02's form sends both fields). (2) numberPrefix sent while effective style is not `key` -> 400 "only used with numberStyle 'key'" (no silent inert storage); (3) entity invariant: NumberPrefix is non-null only under Key style — switching to hash/plain always clears it.
- dotnet ef 10.0.12 emits block-scoped namespaces for generated migrations while .editorconfig enforces file_scoped:error; all existing migration .cs files are file-scoped but every Designer.cs (auto-generated header) in the repo is block-scoped and dotnet format skips them. Followed that split: migration .cs normalized to file-scoped, Designer left as EF emitted.
- Migration filename timestamp 20261005160124 comes from dotnet ef wall-clock at generation.
</obstacles>

<log>
- 2026-10-05 [agent] — taken by orchestrator — dispatching dotnet-coder (sonnet) for backend number settings
- 2026-10-05 [agent] — coding round complete: Core enum ProjectNumberStyle (Hash=0/Key/Plain) + Project.SetNumbering guard (prefix required non-empty for Key, ^[A-Z][A-Z0-9_]{1,15}$, cleared on non-Key); EF config HasDefaultValue+HasSentinel(Hash) mirroring ClaimStaleThresholdMinutes; migration 20261005160124_AddProjectNumberStyle generated via dotnet ef (NumberStyle INTEGER NOT NULL default 0 = zero-impact, NumberPrefix TEXT NULL maxLength 16), files normalized to repo style; UpdateProjectRequest += NumberStyle/NumberPrefix (string, null=keep), ProjectResponse += NumberStyle enum/NumberPrefix, mapper+validator extended; ProjectsApi.Update parses style with ValidationProblem naming allowed values, enforces prefix-only-with-key and key-needs-prefix (effective prefix falls back to stored one so key->key PUTs are idempotent). Gates: dotnet build 0w/0e; tests Core 365 passed, Infrastructure 662 (incl 3 new migration tests: legacy-row defaults, key round-trip, down-to-AddProjectManifest drops columns then re-up -> Hash), Api 650 (incl 8 new ProjectApiTests), Mcp 486; dotnet format --verify-no-changes green on Core/Host/Infrastructure + all 3 touched test projects. Not committed.
- 2026-10-05 [agent] — light review PASS-WITH-NITS 0 blockers (spec-compliance full, migration reversible, validation fail-closed, tests pin all three ACs). Orchestrator fix round: nit 1 — prefix regex moved to [GeneratedRegex] Project.NumberPrefixRegex (repo convention), validator switched to .Must with explicit null-guard; pattern single-sourced in the attribute. Gates re-run honestly after an MSB4166 build-node crash forced a clean rebuild: build 0w/0e exit 0, Core 365, Api 650, format green (Core+Host). Nit 2 (validator rejects untrimmed ' OM ' while entity trims) deferred to task 03 closeout scope. Nits not fixed: none remaining here; review nit 3 informational.
</log>
