---
feature: 006
task: 10
slug: closeout-e2e-docs
status: todo
review: hard
created: 2026-09-08
---

# 006/10 — Closeout: e2e dogfood, docs, hard review

<goal>
Prove the feature works end to end on this repo's own BACKLOG without touching the user's real database, bring the living docs in line with what was built, record the new architectural decision, and pass a hard architect review.
</goal>

<context>
Tasks 01–09 are in: domain, two HTTP doors, workflow settings, materialization, importer, MCP surface, and the full web UI. Requirement R8 demands both toolchains green; R7 demands a lossless import of the repo's own BACKLOG.

**Never run the e2e against `~/.overmind/overmind.db`.** The Host takes `Overmind:DatabasePath` from configuration (`DependencyInjection.AddOvermind`), so an e2e run uses a temp DB and a temp `OVERMIND_WEB_ROOT`; the real repo folder used for materialization must be a **copy** of `BACKLOG/` inside a temp directory, never the working tree.

Docs to update live at the repo root: `CONTEXT.md` (system as built), `STRUCTURE.md` (layout + conventions), `TECHNICAL-SOLUTIONS.md` (ADRs, newest first), `BACKLOG.md` (index statuses). House rule: docs change in the same commit-worthy change as the code they describe — this task is where the feature-level write-up lands.
</context>

<scope>
E2E dogfood (script it as a repeatable set of shell steps recorded in this file's `<log>`, not as a committed test):
1. Start the Host with a temp `Overmind:DatabasePath`, `--no-tray`, and a temp web root.
2. Register a project whose `DiskPath` is a temp copy of this repo (or at least of `BACKLOG/` + `BACKLOG.md`).
3. Run `--import-backlog <copy>` → verify R7 spot-checks against the source files: feature statuses and versions, `spec.md` `FrozenAt` from the `validated:` date, task `Order` from `NN-` prefixes, `<log>` lines preserved verbatim, `review:` levels (006/01 = hard, 006/03 = light), `requirements.md` ignored.
4. Flip the project's materialization to `on` → compare written files against the originals in the copy: layout, frontmatter fields, `BACKLOG.md` managed block inserted with all hand-written content outside the markers preserved byte-for-byte.
5. Edit a materialized file by hand → `GET .../planning/drift` reports `Modified` → `POST .../materialize` `{force:false}` → 409 → `{force:true}` → clean drift.
6. Drive one task through the agent door: `current` → `set_status in_progress` → `append_log` → `set_status done`; verify the board counters move.
7. Flip `requireHumanValidation` to `on` (project level) → `POST /api/agent/planning/artifacts/{id}/validate` → 409 `HumanValidationRequired` with the STOP text; `current` → `state: stop`; the human door's validate still succeeds.
8. Flip materialization to `archive` → files appear under the temp data directory and the repo copy is cleaned; flip to `off` → tracked files and rows are gone, `BACKLOG.md` block removed.
9. Run the real MCP adapter (`overmind-mcp`) against this Host and list tools/resources; call `planning_current_task` and read `overmind://projects/{id}/planning/features` to confirm the rendered markdown.

Docs:
- `CONTEXT.md`: new "Planning (the ceremony as an entity)" section — model summary, the two HTTP doors and why the agent door exists, gate resolution, materialization strategies + managed-block hashing, importer flag and its declared losses, MCP planning surface; update "Projects and roles" (new folders/files), "Communication rules" if the agent door changes anything, "Development phases" (006 done), "Storage" (new tables).
- `STRUCTURE.md`: repository layout additions (`Infrastructure/Planning/`, `Host/Api/Planning*Api.cs`, `AgentPlanningApi.cs`, `BacklogImportCommand.cs`, `web/src/features/planning`, `pages/planning-page`, `app/planning`), plus a line about the shared-handler rule for the two doors.
- `TECHNICAL-SOLUTIONS.md`: new ADR "Two HTTP doors for one aggregate: `/api/planning/*` and `/api/agent/planning/*`" (status Accepted; context = the validation gate must distinguish agent from human while R2 demands one service path; decision = separate route prefix mapped onto shared `internal static` handlers, gate enforced only on the agent door; alternatives = actor flag in the body/query, `X-Overmind-Actor` header — both rejected because they make the gate a parameter any caller can omit; consequences = the agent door is the place to add agent-scoped policy later, and new planning routes must be mapped on both doors deliberately).
- `TECHNICAL-SOLUTIONS.md`: new ADR "Planning ceremony mutations live in `Infrastructure/Planning` services" (status Accepted; context = three writers — human door, agent door, CLI importer — trip the extraction trigger of the vertical-slice ADR, and a per-handler `SyncAsync` convention could be silently forgotten, diverging disk from DB; decision = `PlanningFeatureService` + `PlanningArtifactService` own load → domain → save → sync → failure translation, API handlers stay thin, `PlanningException`/`PlanningFailureKind` is the single error vocabulary; alternatives = keep pure vertical slices with a shared conflict helper — rejected because the importer would still duplicate upsert logic; consequences = this supersedes the vertical-slice ADR **for the planning bounded context only**, Entries/Kits stay as they are).
- `TECHNICAL-SOLUTIONS.md` / `STRUCTURE.md`: record the canonical version-numbering convention (plan A5 — "a version row labelled N holds the body that was live at version N; a fresh aggregate is version 1"), noting that `ContextEntry`/`EntryVersion` predate it and are tracked as hardening; record the enum posture (explicit values on the six planning enums, no DB CHECK constraint, vocabulary enforced in the domain — local trusted SQLite file); record the multiple-cascade-paths caveat on `PlanningMaterializedFiles` (fine on SQLite, needs `Restrict` on SQL Server) in the storage section of `CONTEXT.md` rather than only in a task file; add the STRUCTURE line that the Core root holds domain helpers/services (`PlanningLog` is the first).
- `BACKLOG.md`: 006 → `[x]` with a one-line outcome; add Deferred entries for (a) FTS5 indexing of planning bodies, (b) a version-history/diff UI, (c) normalizing `ContextEntry.UpdateContent`/`CurrentVersion` to the canonical numbering (touches `EntryApiTests`, `EntryMapper`, the web badge), (d) moving `MaterializedFile` into `Core/Entities` now that a correct twin exists; move any new hardening nits found during this task into Deferred with their source noted.
- `BACKLOG/006-planning/feature.md`: no change unless the scope actually moved (then bump `version` and say why).

Full verification: `dotnet build && dotnet test` (record the counts), `cd web && npm run lint && npx tsc --noEmit && npm run build` (record warnings/errors). Then run the hard `architect-review` over the feature diff and fix or record every BLOCKER/IMPORTANT it raises; write its report to `audits/architect-review-2026-09-XX.md`.
</scope>

<anti-scope>
- No new features, no scope growth beyond R1–R8; anything discovered goes to `BACKLOG.md` Deferred with a source note.
- No writes to the user's real `~/.overmind` database or to the working tree's `BACKLOG/` during e2e.
- No git commits (the user commits); no rewriting of earlier task files beyond appending `<log>`/`<obstacles>` lines.
</anti-scope>

<constraints>
- Both toolchains green, with the actual test counts recorded in `<log>`.
- Docs are written in English, in the existing voice and structure of each file (no new top-level sections unless the file's pattern calls for one).
- The ADR follows the established `## Title — date` + Status/Context/Decision/Alternatives/Consequences shape, newest first.
- The e2e must leave no temp processes running and no files outside temp directories.
</constraints>

<acceptance-criteria>
- [ ] Every e2e step above ran and its outcome is recorded in `<log>` (including the R7 spot-checks).
- [ ] `dotnet build && dotnet test` green with counts recorded; `npm run lint && npx tsc --noEmit && npm run build` clean.
- [ ] `CONTEXT.md`, `STRUCTURE.md`, `TECHNICAL-SOLUTIONS.md` (new ADR) and `BACKLOG.md` all match the shipped code.
- [ ] Hard architect review completed, report in `audits/`, BLOCKERs fixed or explicitly recorded.
- [ ] No mutation of the user's real database, working tree `BACKLOG/`, or git history.
</acceptance-criteria>

<refs>
- @../spec.md — Requirements R1–R8
- @../plan.md — Task 10 + Risks
- @./05-planning-materialization.md, @./06-backlog-importer.md, @./07-mcp-planning-surface.md — the surfaces being exercised
- CONTEXT.md, STRUCTURE.md, TECHNICAL-SOLUTIONS.md, BACKLOG.md
- audits/architect-review-2026-09-07.md — report shape and severity vocabulary
</refs>

<obstacles>
</obstacles>

<log>
2026-09-08 — task created from the validated plan.
</log>
