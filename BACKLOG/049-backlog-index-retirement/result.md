---
feature: 049
slug: backlog-index-retirement
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Result 049 — Retire BACKLOG.md generated index: состояние — только борда (UI/MCP), на диске — дерево артефактов

Status: CODE-COMPLETE. The feature stays `inProgress` by design — task 05 (live migration) is open and deploy-gated; the done-flip lands after task 05 executes post-deploy.

## What landed

- Writer removed from the materializer — pure removal: `BuildBacklogPlan`, its `plans.Add` call and the planned-path BACKLOG.md branch in `HashTrackedDiskCopyAsync` are gone (`PlanningMaterializer.cs`); the legacy cleanup surface is intact byte-exact (const + block-hash branches in `DetectDriftAsync` / `InspectTrackedCopyAsync` / `RemoveTrackedCopyAsync` / `ClearAsync`), so existing tracked index rows still migrate on the next pass with 033 semantics (drift refuses, force warns, hand prose survives).
- `ManagedBlock` trimmed to the legacy-extraction surface: `Render` / `Merge` / `StatusMarker` deleted; markers + `TryExtract` + `Remove` kept.
- All tests migrated to the index-free end-state: materializer pins reworked plus migration pins a–f (strip keeps prose, drift refusal, force warning, no-markers NotFound, fresh project creates no index, archive switch, no false drift); consumer tests (Entry/Import/RoundTrip/Guard/Api/Mcp `Canned.cs`) flipped; `CrudRoundtripTests` and `InstallCommandOnboardTests` deliberately untouched (legacy row shape + conflict-scanner fixture remain valid).
- Docs consistent with the end-state: README, CONTEXT (tombstone comment :131 untouched), STRUCTURE, AGENTS; new ADR «BACKLOG.md index retired: the board is the only state copy (049)» in TECHNICAL-SOLUTIONS.md (old ADR lines untouched per ADR immutability); crew kit ×8 — 4 contract files + overmind-ceremony SKILL ×4 ports, sentence swap verified identical across ports.
- Grep sweep clean: `BACKLOG.md` residues only where intended (materializer legacy code + tests, scanner + fixture, kit tombstone header, FEATURELIST history, materialized trees).

## Verification

Full serial suite on the 049 lane: 2229 passed / 0 failed (Core/Infrastructure/Api/Mcp). All five executed tasks (01–04, 06) closed done with light reviews APPROVED.

## Commits

- `4470663` — task 01, writer removal
- `d063aea` — task 02, materializer migration pins
- `8905e1d` — task 03, consumer tests
- `8a0f9fc` — task 04, repo docs + ADR
- `c79e072` — task 04, crew kit ×8

## Human decisions surfaced for result validation

1. Task 05 live migration is DEPLOY-GATED: restart of the serving host included — runs on the human's explicit go with the next release; the main session schedules it.
2. Pointer-file fate (decided: POINTER): the ~89 hand-written lines (legacy index header + Deferred ledger) move to the board and a static pointer replaces `BACKLOG.md` — this happens IN task 05, not before.
3. Strip-on-force for legacy index rows ships as non-goal behavior (ledger #263) — production follow-up if wanted.
4. Global CLAUDE.md + deployed kit copies (`~/.claude` and sibling runtime homes) are outside this repo — syncing the ×8 edits is the human's separate act at result validation.

## Open item

Orphan sweep (Deferred-ledger entries: macOS `.pkg` half, P0 `overmind.localhost` proxy check, zero-byte `overmind.db` boot crash, + anything else open) must land on the board BEFORE the pointer swap — task 05 carries it as a precondition of step 4.

<refs>
- Tasks: 01 `5294463f`, 02 `d20f43b5`, 03 `7329df84`, 04 `5df6855e`, 05 `8bf564ce` (open, deploy-gated), 06 `da09eafc`.
- Spec: cc4c4392-566e-4ff3-a84a-ff6aa61914fd; plan: cf343934-d939-482d-9dd2-36dc8e41287b.
- 033-materializer-clobber-guard; 042-onboard-observability; 075-materialization-git-hygiene; 116-crewkit-in-repo; 125-microbatch-cleanup.
</refs>

<log>
- 2026-10-06 [agent] — result drafted at code-complete: tasks 01–04+06 done (commits 4470663, d063aea, 8905e1d, 8a0f9fc, c79e072), suite 2229/0, task 05 open deploy-gated by design
</log>
