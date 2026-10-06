---
feature: 125
slug: microbatch-cleanup
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Result 125 — Microbatch cleanup wave: close the accumulated review-findings ledger as one batch

<goal>
Wave summary for human validation: the groomed slice of the microbatch ledger (10 pinned fixes + the #137 transitive close) landed as 4 focused commits in strict order, full gates green, 12 ledger items tombstoned with their wave commit hashes, zero behavior change beyond the pinned fixes.
</goal>

## Scope delivered

10 fixes across 4 commits, strict sequence T1→T4, one reviewable commit per area:

| Commit | Task | Items |
|--------|------|-------|
| `86e8eed` | T1 (standalone, first) | #139 hidden-proof pin test in the 077 validation-proof suite (approval → reply → Hide() → ValidateAsync still finds the proof line); Hide() gains its first caller — #137 closed transitively. No prod code. |
| `2fa454c` | T2 (Host/backend) | #232 number-prefix trim in ProjectValidators (+pin), #241 status-parse extracted from the three issues list endpoints, #52 warning denominator counts only `Checked.HasValue`, #53 pollution + human-door warning pins, #193 decline path single-load. |
| `04104b9` | T3 (MCP adapter) | #127 explicit required-arg error text for planning_* tools (ContractViolation pattern), #159 S/P/R multi-match diagnostics in ResolveRosterMatch; pins in PlanningToolContractTests / Mcp.Tests. |
| `217e0bd` | T4 (web) | #242 `projectName` dropped end-to-end (Host contract + projection + web types), #243 attention prime gated on `isPending || error`, #195(a) `validateArtifact.error` reset on tab change. |

Reviews: light (T1/T4) and hard (T2/T3, contract/pin semantics) — zero blockers across all four rounds; T4 approved as `217e0bd`.

## Verification

- Full serial `dotnet` suite green at every round (serial per infra note #158): final **2218/0 across 4 projects** + **498** in Mcp.Tests.
- Web gates green: `npm run lint`, `npx tsc --noEmit`, **243 web tests**, `npm run build`.
- `dotnet format --verify-no-changes` clean on the touched projects.

## Ledger

- **12 items tombstoned** with their wave commit hashes in the same work-step as each fix commit: #139, #137, #52, #53, #193, #232, #241, #127, #159, #242, #243, #195(a)-only.
- New findings filed back to the ledger instead of ride-along fixes: **#244** (STRUCTURE.md:67 drift), **#245** (planning_materialize mutation guard), **#246** (dead JOIN + field + asserts bundle).
- Still open by design: #240 (growth-trigger note), #233–#239 (not in the body's scope), #195(b) (harmless race, recorded).

## Recorded decisions

- **#193** — landed via a private `AppendLogAsync` overload reusing the already-loaded entity, instead of the blessed skip; the hard review upheld the overload as seam-clean (no public-method additions, 7-method ceiling respected).
- **#52** — an extra pin was kept beyond the minimum: the only test that discriminates the denominator change (lines without checkboxes do not inflate the count).
- **#127** — scope settled as solo-required-arg read doors only; multi-arg doors would be a wider contract change and went back with a faithful-fix verdict.
- **#195(b)** — the decline/validate double-click race stays open in the ledger (harmless, log-only), untouched by T4 per anti-scope.
- **#127 plan wording** — «no schema changes» was read as backend-schema: removing required-ness from the tools/list schema was the only mechanism available to AC#1 (wire-level required-removal), documented in the task's obstacles.

## Acceptance cross-check (feature body)

- Ledger entries tombstoned with commit hashes in-wave — **met** (12 tombstones, hash per commit, same work-step).
- Full suites green — **met** (dotnet serial 2218/0 + 498, web lint/tsc/243/build, format clean).
- No behavior change beyond the pinned fixes — **met**; reviewers swept each commit for ride-alongs (#233–#239 files untouched).

<refs>
- Spec: cdb6854b-01ac-410c-82fc-8f13e706cb7d (validated) — the wave's execution contract.
- Plan: e3cbeb50-ac62-4d36-8fc8-d4cbb379dd3d (validated) — commit sequence and verification gates.
- Tasks: T1 8b68c19b, T2 4092d7dd, T3 34cd3e66, T4 047ce833 — all done.
- Ledger: `/home/johan-boolman/.claude/projects/-mnt-Ubuntu-Dev-Projects-AI-Tools-Overmind/memory/overmind-microbatch-queue.md` — 12 tombstones + new #244–#246.
- Wave commits: `86e8eed` → `2fa454c` → `04104b9` → `217e0bd` (local main, unpushed — push stays human-only).
</refs>

<obstacles>
- #127's plan constraint «no schema changes» collided with the only viable mechanism (tools/list required-removal is itself a wire-schema change); resolved as backend-schema reading and documented in T3's obstacles — flagged here so the human validates the interpretation, not just the code.
- #193's fix took the overload route rather than the plan's pre-approved skip; the hard review ratified it, noted here for provenance since the plan named the skip as the fallback, not the landing shape.
- Tombstone mechanics executed as «same work-step post-commit» (the ledger file lives outside the repo) — the spec's literal «inside the fix commit» is not physically possible for a file outside git; recorded as the standing interpretation for future waves.
</obstacles>

<log>
- 2026-10-06 [agent] — result v1 drafted after T4 approval (217e0bd, light review 0 blockers); 4 commits, 12 tombstones, 3 new ledger items (#244–#246), all gates green; awaiting human validation tap (notify goes from the main session).
</log>
