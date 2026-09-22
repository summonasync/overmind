---
feature: 039
slug: artifact-patch-write
status: draft
created: 2026-09-22
---

# Result 039 — Artifact patch write: append `<log>` / replace XML-секции / tick AC — без перезаливки тела

## What shipped

- `PlanningSections` (Core root): anchored-XML-section grammar generalised out of `PlanningLog` — `Find`/`TryFind`/`Replace`/`List` + `ParseAcceptanceCriteria`; `<log>` is a special case of it (task 01).
- `PlanningArtifact.PatchSection` / `TickAc` + `PlanningArtifactService.PatchAsync` over `PlanningArtifactPatch`: snapshot + `Version++` on every body mutation including `AppendLog` (D4), frozen demotion with mandatory `logNote` (D5), strict 400s naming what was searched, idempotent re-tick (D6) (task 02).
- Agent endpoint `POST /api/agent/planning/artifacts/{id}/patch` + MCP tool `planning_patch_artifact` with the D7 compact echo `{artifactId, kind, section, version, bodyChars}`; D9 cost-tier descriptions on all three write tools + `SessionBootstrapPrompt` pick-by-cost routing line (task 03).
- Closeout (task 04): live e2e through materialisation, clobber-guard and import round-trip proofs, write-cost docs in `CONTEXT.md`/`STRUCTURE.md`, this result.

## E2E evidence (live isolated host, 2026-09-22)

Isolated host pattern (015/033): scratch `HOME=/tmp/overmind-039-t04/home`, `Overmind__BindUrl=http://127.0.0.1:4891`, `--no-tray`, killed strictly by the port PID (215721) after the run; the production host on 4761 and the real `~/.overmind` were never touched (`host.port` still 4761, prod `/api/health` 200 throughout). Raw captures: `/tmp/overmind-039-t04/evidence/` (host.log, numbered json/txt per step).

- **Patch → disk.** Created project `039-e2e` (diskPath = scratch repo), workflow `materialization=on`, feature `e2e-patch-roundtrip`, task artifact (v1, 378 chars). `POST /api/agent/planning/artifacts/{id}/patch` `{op:"replace_section",section:"scope",...}` → 200 echo `{section:"scope",version:2,bodyChars:464}`; the materialised `BACKLOG/e2e-patch-roundtrip/tasks/00-*.md` carries the new scope and the `logNote` line, `diff -u` against the pre-patch baseline shows hunks inside `<scope>` and `<log>` only — everything else byte-intact (evidence 05/06).
- **Tick → file.** `{op:"tick_ac",acIndex:0}` → 200, version 3; the file shows `- [x] patched section lands on disk` while item 1 stays `[ ]` (evidence 07).
- **Log → version.** `POST .../log` → board version 4; `GET /api/planning/artifacts/{id}/versions` lists snapshots v1(378)/v2(464)/v3(464) — D4 snapshot discipline including the new AppendLog bump (evidence 08–10). Deviation from the task text: artifact `version:` is a board-level field, it is NOT materialised into task/spec frontmatter (only `feature.md` carries the feature version) — emitting it would change materialisation output, which D8 froze. The bump is therefore proven at the board/API surface, not in frontmatter.
- **Clobber-guard regression (033 unchanged).** Hand-edited the managed file on disk → patch via the agent door still 200 (board v5) with the sync swallowed-and-logged (`GuardLocalEditsAsync` warning in host.log, hand edit intact — a patch is an ordinary mutation, D8); `POST materialize {"force":false}` → 409 listing the path + transcription hint, file intact; drift GET → `modified`; `{"force":true}` → 200 with `Overwrote locally modified file` warning, file regenerated from board truth (hand edit gone, all patches present) (evidence 11–15).
- **Import round-trip (disk==board).** `--import-backlog` CLI against the same scratch DB: `1 features (0 new), 0 artifacts` — board body and version 5 identical after re-import, no rollback (evidence 16–18).
- **Import precedence (stale disk, 034 direction pin).** Rewound the task file to the pre-patch baseline and re-imported: disk wins — board body rolls back to the baseline content, version moves forward 5→6 via UpdateBody snapshot (a stale file can revert a body but never roll the version number back); the post-import sync refused through the same LocalEdits guard (evidence 19/20).

## Gates

- `dotnet build Overmind.sln` — 0 warnings, 0 errors.
- `dotnet test Overmind.sln` — 1102/1102 green (Core 175, Infrastructure 346, Api 326, Mcp 255), 0 skipped.

## Deviations

- E2E ran as a scripted live host (not an integration test): the task's own preference, and the live path exercises the real Program.cs bootstrap (scratch HOME, single-instance lock, port file) that WebApplicationFactory skips.
- Artifact version in frontmatter: not emitted by design — see the log section above; task text's "frontmatter version bumped" is satisfied at the API/board surface.
- No MCP-stdio leg in the live run: task 03's Mcp.Tests contract suite already pins `planning_patch_artifact` (echo, descriptions, idempotent flag); the e2e focused on the HTTP chain through materialisation.

## Known follow-ups

- `PlanningArtifactService` now has 6 public methods — one over the god-service guidance; patch-path extraction recorded in `STRUCTURE.md` as the split candidate.
- Read-side cost symmetry (cheap partial reads) is feature 040; version diff is 045.
