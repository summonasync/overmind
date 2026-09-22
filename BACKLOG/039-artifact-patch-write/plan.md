---
feature: 039
slug: artifact-patch-write
status: validated
created: 2026-09-22
validated: 2026-09-22
---

# Plan 039 — Artifact patch write: append `<log>` / replace XML-секции / tick AC — без перезаливки тела

<approach>
Four sequential tasks, backend-first: parser → domain/service → API+MCP surface → e2e closeout. TDD applies to tasks 01–02 (domain logic and a parser are exactly that); 03–04 are surface and verification. No new NuGet or npm packages — everything rides the existing stack.

Key design consequences from spec.md:

- **The parser comes first and nothing else moves until its behaviour is pinned (D3).** `PlanningLog` has three live consumers today (`UpdateBody`, `SetStatus`, `AppendLog` service paths); refactoring its anchored-pair logic into `PlanningSections` without characterization tests first is how a cheap feature becomes an expensive regression. Task 01 pins the current `PlanningLog.Append`/`Parse` behaviour with tests BEFORE the refactor, then generalises.
- **Snapshots are the only behavioural change to an existing tool (D4).** `append_log` starts versioning. Version is a plain int and `PlanningArtifactVersion` is append-only, so no migration is needed — but materialised frontmatter `version:` values will jump on projects that log often, and the importer is disk-wins. Task 04 explicitly round-trips an imported file after a patch to prove no rollback.
- **The cost-transparent surface is textual, not structural (D9).** No renames, no new parameters on existing tools — descriptions and the bootstrap routing line only. This keeps 03 within the 037/032 pin-test pattern (assert descriptions via a live `ListToolsAsync`).
- **Ambiguous text matches must fail loudly (extends D6).** A task-AC list can contain two identical item texts; `tick_ac` by text with multiple matches is a 400 listing indices, nudging the agent to `acIndex`. Decided here, not in the task, so the reviewer can hold the line.
</approach>

<tasks>
- [ ] 01-sections-parser — Core: pin current `PlanningLog.Append`/`Parse` behaviour with characterization tests, then extract the anchored-pair logic into `PlanningSections` (`Find(name)`, `Replace(name, content)`, `List()`, AC-list parse) and rebase `PlanningLog` on it; parser unit suite per R3 (single, missing, duplicate names — last anchored pair wins, blank lines inside content, CRLF); verify: `dotnet build` + `dotnet test` green with the new suite → tasks/01-sections-parser.md
- [ ] 02-patch-domain-service — Core: `PlanningArtifact.PatchSection/TickAc` (snapshot + Version++ per D4, frozen-demotion per D5); `AppendLog` brought under snapshot discipline (D4, called out in its artifact log); Infrastructure: `PlanningArtifactService.PatchAsync` with strict `PlanningException`s per D6 (missing section, not-found AC item with visible items listed, ambiguous text → 400 with indices, idempotent re-tick = no-op); unit tests: frozen demotion requires logNote, tick idempotency, diagnostics content; verify: build + test → tasks/02-patch-domain-service.md
- [ ] 03-patch-api-mcp — Host: `POST /api/agent/planning/artifacts/{id}/patch` (R1 contract, 400 diagnostics); Mcp: `planning_patch_artifact` (R2, D7 echo `{artifactId, kind, section, version, bodyChars}`, tick `Idempotent = true`); D9 descriptions for all three write tools + `SessionBootstrapPrompt` pick-by-cost routing line (R7); Api endpoint test + Mcp description pin-test (032 pattern) + echo compactness pin; verify: build + full test run → tasks/03-patch-api-mcp.md
- [ ] 04-e2e-closeout — e2e through materialisation: patch a section on a real project → materialiser writes the patched body to disk, clobber-guard semantics untouched; import round-trip after a patch (disk-wins precedence intact, version bump survives); full `dotnet test`; docs: write-cost model into `CONTEXT.md`/`STRUCTURE.md`, result artifact; review: hard → tasks/04-e2e-closeout.md
</tasks>

<risks>
- **Refactoring `PlanningLog` breaks its live consumers.** Mitigation: characterization tests precede the extraction in task 01; the task ends on a full green run, not on "the new tests pass".
- **`append_log` snapshotting changes observable version numbers.** Materialised frontmatter `version:` jumps on log-heavy projects; the importer is disk-wins, so an old file imported after a patch could roll the number back. Mitigation: task 04's import round-trip check.
- **Field bodies are written by many agents with many styles.** Sections with indentation noise, CRLF, or same-name sections in one body must parse identically to today's `<log>` handling. Mitigation: R3 parser suite runs against realistic fixtures from `BACKLOG/` files, not synthetic minimums.
- **Tick-by-text ambiguity.** Duplicate AC item texts resolve to a 400 with indices (approach note above), never to a guess.
</risks>

<verification>
- `dotnet build Overmind.sln` + `dotnet test Overmind.sln` green after every task.
- Characterization suite proves `PlanningLog` behaviour unchanged after the `PlanningSections` extraction (task 01).
- Api + Mcp tests: patch endpoint contract, description pin, echo compactness (task 03).
- Materialisation round-trip and import precedence proven live in task 04.
- Reviews: light on 01–03, hard on 04 (closeout).
</verification>