---
feature: 125
slug: microbatch-cleanup
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Plan 125 — Microbatch cleanup wave: close the accumulated review-findings ledger as one batch

One wave, four focused commits: #139 standalone first, then the backend/Host commit, the MCP-adapter commit, the web commit. Every commit is reviewable on its own; every closed ledger item gets its tombstone in the same work-step as its commit (mechanics below). Anything that outgrows a one-commit fix goes back to the ledger — never expanded in place (spec constraint).

## Commit 1 — priority: #139 hidden-proof pin (standalone, backend tests)

- **File:** `tests/Overmind.Infrastructure.Tests/PlanningServiceTests.cs` (the 077 validation-proof suite, `ValidateAsync_WithRepliedApproval_*` tests at ~:1736).
- **Change:** add the hidden sibling — approval → reply → `Hide()` on the notification → `ValidateAsync(requireHumanProof: true)` still finds the proof line. Name it after the invariant, e.g. `ValidateAsync_WithHiddenRepliedApproval_StillFindsProofLine`. `Hide()` thereby gets its first caller (closes #137); do not hunt for other Hide() callers.
- **Why first:** the priority invariant; a future `HiddenAt == null` filter on the proof path (`PlanningArtifactService` proof search, locate by symbol — ledger line numbers drifted) must now fail loudly.
- **Sanity check (local, not committed):** temporarily add a `HiddenAt == null` filter to the proof predicate, watch the new test go red, revert.
- **Tombstones:** #139, #137.
- **Verify:** `dotnet test --filter PlanningServiceTests` green; commit stands alone (nothing else in the diff).

## Commit 2 — Host/backend services and validators

- **#232 (trim desync):** `src/Overmind.Host/Api/Validation/ProjectValidators.cs` — trim the number-prefix input in the FluentValidation rule so the validator and the entity agree on `" OM "`-style prefixes; pin test next to the existing project-validator tests (tests/Overmind.Api.Tests) covering the previously-400 untrimmed form.
- **#241 (status-parse ×3):** extract the duplicated issues status-parsing block from `src/Overmind.Host/Api/IssuesApi.cs`, `src/Overmind.Host/Api/AgentIssuesApi.cs`, `src/Overmind.Host/Api/CrossProjectIssuesApi.cs` into one shared helper (new small file under `src/Overmind.Host/Api/Contracts/`, e.g. `IssueStatusParser.cs`, or a co-located static class — coder's call, one file either way). Behavior unchanged, all three endpoints keep their contracts.
- **#52 (warning denominator):** `src/Overmind.Infrastructure/Planning/PlanningArtifactService.cs:338` — the denominator must count only acceptance items with a checkbox: `items.Count(i => i.Checked.HasValue)` instead of `items.Count`. Numerator logic untouched; zero behavior change on real bodies.
- **#53 (warning pins):** two tests — pollution case (all-ticked body + logNote containing `[ ]` → no warnings; service level, PlanningServiceTests) and the human-door warning pin (upsert via the API door surfaces the same warning; shim is identical to MCP, the pin is the point — tests/Overmind.Api.Tests).
- **#193 (decline double-load):** `src/Overmind.Infrastructure/Planning/PlanningArtifactService.cs` decline path (~:462 Kind-check load, ~:484 second load inside `AppendLogAsync`) — reuse the already-loaded entity if it does not complicate the seam; if it does, skip and record the decision in the task's obstacles (pre-approved escape hatch from the spec).
- **Tombstones:** #232, #241, #52, #53, #193 (skip-accepted for #193 keeps the item open in the ledger with a dated note instead).
- **Verify:** `dotnet test --filter` for PlanningServiceTests + the issues/validator test classes; no contract-shape changes in the issues endpoints.

## Commit 3 — MCP adapter

- **#127 (required-arg error text):** `src/Overmind.Mcp/OvermindPlanningTools.cs` — explicit required-argument checks for planning_* tools with contract text naming the argument («projectId is required (required for planning_* tools — entries tools resolve from cwd, planning tools do not)»), following the existing `ContractViolation` pattern (grep `QueueMoveContractError` for the in-repo precedent). Minimum coverage: the projectId-bearing reads from the field case; same-shaped one-liners may ride where trivial.
- **Pin:** `tests/Overmind.Mcp.Tests/PlanningToolContractTests.cs` — `planning_task_queue` / `planning_list_features` without projectId return the explicit error, not the bland wrapper text.
- **#159 (S/P/R multi-match diagnostics):** `src/Overmind.Mcp/OvermindPlanningTools.cs`, `ResolveRosterMatch` — duplicate S/P/R documents (spec@0 + spec@1, data-legal) must produce explicit diagnostics naming the candidates instead of a silent first-match pick. No schema change, no uniqueness constraint, no `ArtifactAliasResolver` extraction (#165a stays out).
- **Pin:** Mcp.Tests — alias resolution against a feature with two specs yields the diagnostic.
- **Tombstones:** #127, #159.
- **Verify:** `dotnet test` on Overmind.Mcp.Tests; existing alias-resolution tests stay green (first-match behavior for non-duplicated data unchanged).

## Commit 4 — web

- **#242 (dead client field):** drop `projectName` end to end — `src/Overmind.Host/Api/Contracts/IssueContracts.cs` (remove from `CrossProjectIssueRow`), `src/Overmind.Host/Api/CrossProjectIssuesApi.cs` (drop from the projection), `web/src/features/issues/types.ts` (+ `api.ts` if referenced); confirm `web/src/pages/issues-page/ui/global-issue-row.tsx` never reads it (it resolves names via `useProjectName`). Update the endpoint's wire-shape pins in tests/Overmind.Api.Tests.
- **#243 (attention prime gate):** `web/src/widgets/app-shell/lib/use-attention-surfaces.ts` — gate the prime on `isPending || error` so a failed first sync followed by a successful hidden refetch primes instead of leaving stale data; add a test in web/tests following the house patterns (logic test or the `readFileSync` source-pin precedent from approval-bubble.test.mts).
- **#195(a) (stale approveError):** `web/src/features/planning/components/feature-workspace.tsx` — reset `validateArtifact.error` in `setTab` (~5 lines; ledger refs :276/:514). Only (a); the (b) race stays out.
- **Tombstones:** #242, #243, #195(a) (the ledger line is #195 — mark it closed with a note that only (a) landed and (b) remains recorded).
- **Verify:** `npm run lint && npx tsc --noEmit` + web test suite.

## Ledger tombstone mechanics (per commit)

The ledger lives outside the repo (`~/.claude/projects/-mnt-Ubuntu-Dev-Projects-AI-Tools-Overmind/memory/overmind-microbatch-queue.md`), so «same commit» executes as: fix commit lands → hash is known → the ledger line is marked `[CLOSED 2026-10-06, коммит <hash> — <what landed>]` in the same work-step, before the next item starts. Tombstones are never batched to the end of the wave. The final gate cross-checks the ledger against `git log` — every closed item carries the hash of the commit that fixed it, and no closed item remains unmarked. If an item is skipped (only legitimate case: #193 seam), the ledger line stays open with a dated note instead of a tombstone.

<verification>
Full gates, in order, before the wave is called done:

1. **Build:** `dotnet build` clean after each backend/adapter commit (PostToolUse hook also enforces per-edit).
2. **Backend suite:** full `dotnet test` at the end of the wave — run serially or with capped parallelism (infra note #158: solution-wide parallel runs have OOM-flaked on this host).
3. **Web gates:** `npm run lint && npx tsc --noEmit` + web tests, in the web project directory.
4. **Wave integrity:** `git log` for the wave shows exactly the focused commits — #139 first and standalone, no mega-commit, no push (push stays human-only); conventional English messages.
5. **Ledger cross-check:** every item fixed by the wave is tombstoned with its commit hash; #233–#239, #240, #195(b) untouched in the ledger.
6. **Diff hygiene:** the wave diff contains no ride-along edits beyond the pinned fixes (spot-check: no #233–#239 files touched — e.g. usePlanningBoard polling, settings preview, sidebar URL, chevron padding stay as-is).
7. **Behavior honesty:** no contract or behavior change beyond the pinned fixes — issues endpoints byte-compatible minus the dropped `projectName`, warning text unchanged apart from the corrected denominator, alias resolution unchanged for non-duplicated data.
</verification>

<log>
- 2026-10-06 [agent] — plan v1 drafted against validated spec: 4 focused commits (#139 standalone → Host/backend → MCP adapter → web), per-item files and verification steps, ledger tombstone mechanics spelled out (memory file lives outside the repo — tombstone lands in the same work-step as its commit, hash recorded post-commit); status draft, awaiting human validation tap.
- 2026-10-06 [agent] — validated with human proof: notification 40a4f035-5d29-4c82-acc3-e714c701a556 replied 2026-10-06 11:23:12 UTC
</log>
