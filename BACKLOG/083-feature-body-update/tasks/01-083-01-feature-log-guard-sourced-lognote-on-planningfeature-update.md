---
feature: 083
task: 01
slug: feature-body-update
status: done
review: light
created: 2026-09-27
brief: Core: 078 guard ported into PlanningFeature.Update + sourced logNote append; #74 seam closed
---

# 083/01 — feature log guard + sourced logNote on PlanningFeature.Update

<goal>
Port the 078 artifact log-guard onto the feature entity and let Update carry a sourced logNote line — the seam that closes microbatch #74 for BOTH doors (human PUT included) and enables the agent-door edit (tasks 02-03).
</goal>

<context>
Executor reads ONLY this file. Guard shape to port: `PlanningArtifact.RequireLogHistoryAppendable` (src/Overmind.Core/Entities/PlanningArtifact.cs:309-332) — stored `<log>` (PlanningLog.Parse, trim-normalized) must be an exact ordered prefix of the incoming body's log; empty stored log passes; violation → `PlanningException.LogHistoryNotAppendable(matchedIndex, storedCount, incomingCount)`. Feature entity: src/Overmind.Core/Entities/PlanningFeature.cs — `Update(title, body)` is partial (null keeps the old value); existing tests: tests/Overmind.Core.Tests/Entities/PlanningFeatureTests.cs (Update_TitleOnly_KeepsBody, SetPriority_ChangesPriorityTouchesUpdatedAtKeepsVersion etc. — all must stay green).
</context>

<scope>
- src/Overmind.Core/Entities/PlanningFeature.cs:
  - private `RequireLogHistoryAppendable(string incomingBody)` — verbatim 078 shape (PlanningLog.Parse prefix check), called from `Update(...)` BEFORE mutating the body. Applies regardless of which fields change (title-only edits with an untouched log also pass — the guard checks the log tail, not the diff).
  - extend `Update` to accept an optional log note + `PlanningLogSource`: when the note is non-empty, append ONE dated sourced line to the body's `<log>` via `PlanningLog.Append` (same grammar as artifacts: `- YYYY-MM-DD [source] — note`). When absent (human PUT today) — no line appended, guard still applies.
  - `PlanningFeature.Update` behavior on Title only / Body only must stay byte-compatible with current tests.
- Pin current Version behavior of Update in a characterization test (whatever it does today — bump or keep — assert it explicitly and record the fact in <obstacles> for the plan's risk follow-up; do NOT change Version semantics in this task).
- Tests (tests/Overmind.Core.Tests/Entities/PlanningFeatureTests.cs or a sibling): history rewrite rejected (middle line mutated), tail deletion rejected, pure append accepted + note line lands with the right source marker, empty stored log accepts anything, title-only edit with intact log passes, existing suite green.
</scope>

<anti-scope>
- No Infrastructure/Host/Mcp changes (later tasks). No human logNote REQUIREMENT (absence is legal). No Version-semantics change. No web.
</anti-scope>

<constraints>
- No comments; English strings; no new packages; NO git commits.
- `dotnet build Overmind.sln` 0W/0E; full `dotnet test` green (baseline 1533).
- Diff confined to src/Overmind.Core + tests/Overmind.Core.Tests.
</constraints>

<acceptance-criteria>
- [x] Log history rewrite (stored line mutated) → PlanningException.LogHistoryNotAppendable from PlanningFeature.Update.
- [x] Log tail deletion → same failure; pure append + note lands as one dated sourced line.
- [x] Empty stored log accepts any incoming body; title-only edit with intact log passes.
- [x] Existing PlanningFeatureTests stay green unchanged; current Update→Version behavior pinned and recorded.
- [ ] dotnet build 0W/0E + full suite green; diff confined to Core + Core tests.
</acceptance-criteria>

<refs>
- src/Overmind.Core/Entities/PlanningArtifact.cs:309-332 (guard to port)
- src/Overmind.Core/Entities/PlanningFeature.cs (Update/SetPriority/SetStatus)
- src/Overmind.Core/PlanningLog.cs (Parse/Append grammar, sourced lines since 043)
- tests/Overmind.Core.Tests/Entities/PlanningFeatureTests.cs
</refs>

<obstacles>
- Version semantics pinned, NOT changed: `PlanningFeature.Update` bumps Version only when Status != Draft (Draft edit keeps it); the same rule now also covers note-append updates. Pinned explicitly by `Update_VersionPinnedToBumpOnlyOutsideDraft` plus existing Update_InDraft/AfterLeavingDraft pins. Plan's Version-risk follow-up can rely on this.
- Import door collision (scope deviation, test-only): the new guard in `Update` broke `BacklogImporterTests.Import_ArtifactOnlyInDatabase_KeptAndReported`. Root cause: `UpsertAsync` → `TransitionOnArtifactCreate` appends an `[auto] artifact created → inProgress` line to the feature body; the test's hand-written feature.md did not carry it, so import was silently DROPPING the board log line — exactly seam #74, now rejected on the import door too. Fix touched only the test fixture: feature.md now mirrors the feature's actual board log lines verbatim (dates copied from the entity, nothing hardcoded); test semantics (kept task 5, reported) unchanged. No production Infrastructure code touched.
- Heads-up for tasks 02-03: with the guard live in `Update`, any "board ahead of disk" feature import now throws PlanningException.LogHistoryNotAppendable from `PlanningFeatureService.ImportAsync`. If the migration/import door must tolerate stale disk bodies, that bypass needs to be designed in task 02/03 deliberately.
- Test-authoring quirk: first draft of the pure-append test miscounted (incoming body already carried a new log line, plus the note = 4 lines); reduced it to a clean note-append against the stored body.
</obstacles>

<log>
- 2026-09-27 — Task created from validated plan 19f231e5; dispatch first (bottom-up).
- 2026-09-27 [agent] — 083/01 executed: 078 guard ported verbatim into PlanningFeature.Update (runs before any mutation, on body ?? Body so title-only edits are covered); Update extended with optional logNote + PlanningLogSource — non-empty note appends one dated sourced line via PlanningLog.Append, absent note appends nothing (human PUT path) with the guard still active. Version semantics untouched (bump only outside Draft) and pinned by Update_VersionPinnedToBumpOnlyOutsideDraft. 10 new tests in PlanningFeatureTests cover rewrite/tail-deletion/pure-append+source/empty-log/title-only/note-theory/version-pin; guard-before-mutation asserted via untouched Title/Body/Version/UpdatedAt. Deviation: guard broke BacklogImporterTests.Import_ArtifactOnlyInDatabase_KeptAndReported (UpsertAsync auto-status line missing from hand-written feature.md = stale disk body dropping a board log line, i.e. seam #74 on the import door) — fixed in the test fixture only (disk body mirrors the entity's actual log lines), production Infrastructure untouched, details in <obstacles>. Verification: dotnet build Overmind.sln 0W/0E; full dotnet test green 1543 (225 Core + 496 Infrastructure + 451 Api + 371 Mcp; baseline 1533 + 10 new). AC 1-4 ticked; AC5 left unticked — build/suite green but diff includes one Infrastructure test file. Status → review.
- 2026-09-27 [agent] — Implementation complete, build 0W/0E, full suite 1543 green (baseline 1533 + 10 new); moved to review per task dispatch contract — reviewer should pay attention to the test-fixture deviation on the import door (see <obstacles>).
- 2026-09-27 [agent] — light review PASS-WITH-NITS (0 BLOCKER / 0 IMPORTANT / 2 NIT no-action) — audits/review-083-01.md; независимые гейты: build 0W/0E, 1543/1543; гвард — байт-порт 078, version-пин честный, импортер-девиация тест-only признана легитимной (корень #74 на двери импорта подтверждён кодом); AC5 остался нетикнутым осознанно (декларированный выход в Infrastructure-тесты); done by orchestrator after review
</log>