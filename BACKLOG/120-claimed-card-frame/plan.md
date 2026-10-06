---
feature: 120
slug: claimed-card-frame
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Plan 120 — Board card: claim frame (pin-frame successor)

<goal>
Ship the claimed-card presentation per spec 120: claim line immediately left of the status column + `border-primary` frame for actively held claims. Presentation only — every data field already arrives on PlanningFeatureBoardItem.
</goal>

<scope>
Two tasks, three files, no backend. Task breakdown (as task artifacts) happens after this plan is validated.

**Task 1 — helpers + pin (lib-first, red-green):**
- web/src/features/planning/lib/board-card-context.ts:
  - `cardClaimLine(item): string | null` → `` `claimed by ${alias} from ${vendor}` `` when `claimedByAlias !== null`, else `null`. Vendor null-safety: if `claimedByVendor` is null, fall back to rendering without the `from` tail (`claimed by {Alias}`) rather than the literal "null".
  - `cardClaimActive(item): boolean` → `claimedByHuman === true || claimState === "alive"`; unclaimed → false; null claimState → false.
- web/tests/board-card-context.test.mts — pin in the existing node:test + node:assert strict style: exact format string («claimed by Van from claude»), null when unclaimed, vendor-null fallback, and the active matrix (alive → true, stale → false, ended → false, null → false, human → true regardless of state).
- Verify: `node --test tests/board-card-context.test.mts`.

**Task 2 — card rendering (web/src/features/planning/components/feature-board.tsx):**
- Header row (the `flex items-start justify-between gap-2` div): insert the claim line as a middle child between the title link and the right cluster — `ml-auto min-w-0 truncate text-xs`, tone `text-success` when `cardClaimActive(item)` else `text-muted`. Middle child + ml-auto pins it against the status column («слева от статуса») while keeping truncation under pressure; the title link keeps its `min-w-0`.
- Card root `li` className ternary gains: `cardClaimActive(item) && "border-primary"` — the same class the pre-099 `isCurrent` pin frame used (commit 1686abd^, line ~202). Drop-target `ring-2 ring-primary/40` untouched.
- Render nothing (no element) when `cardClaimLine(item)` returns null — unclaimed cards stay pixel-identical.
- Verify: `npm run lint && npx tsc --noEmit && npm test && npm run build` in web/.
</scope>

<anti-scope>
- No API/DTO/backend work — verified: web/src/features/planning/types.ts (PlanningFeatureBoardItem claim fields) and src/Overmind.Host/Api/Contracts/PlanningContracts.cs already carry all five fields; both stay untouched.
- No changes to FeatureClaimChip, feature-overview-panel, task-board, or the brief line's position (117's lane).
- No new dependencies, no test-runner changes.
</anti-scope>

<constraints>
- Review level: light on both tasks (small surface, no backend, no auth).
- Pin strategy: lib-level pure-function pins only, matching the project style — components are not unit-tested in this repo (no DOM harness); rendered behavior is verified by build + human dogfood on the live board.
- Verification gates per task as listed in scope; the full web gate (lint + tsc + tests + build) must pass before review.
</constraints>

<acceptance-criteria>
- [ ] Both tasks are executable from their artifacts alone, touching only the three listed files.
- [ ] Task 1's pin test pins the exact claim-line format and the active matrix before any component change lands.
- [ ] Task 2's full web gate is green (lint, tsc --noEmit, node --test suite, next build).
- [ ] Dogfood on the board: the claimed 120 card itself shows the line + frame once deployed.
</acceptance-criteria>

<refs>
- Spec 120 (c6f38160-de30-46c8-9538-c02c429df482) — binding requirements and the liveness decision.
- web/src/features/planning/lib/board-card-context.ts + web/tests/board-card-context.test.mts — Task 1 files.
- web/src/features/planning/components/feature-board.tsx — Task 2 file; card root `li` (~line 210) and header row (~line 217) as of drafting.
- Commit 1686abd^ — the removed `isCurrent` pin frame (`border-primary` ternary) this feature succeeds.
- 117-phase-label-as-tag-chip (draft) — same header row; no ordering dependency, trivial rebase either way.
</refs>

<obstacles>
None at draft time. Carried from spec: the human's slot interpretation («brief line's slot» vs header row left of status) is resolved in the spec's obstacles note and must be confirmed at the validation tap.
</obstacles>

<log>
- 2026-10-06 [agent] — Plan drafted alongside the spec: two tasks (lib+pin, card rendering), three files, no API work; gates lint/tsc/tests/build; light review both.
- 2026-10-06 [agent] — validated with human proof: notification d4acfe49-3448-4a8f-855e-ecf55ab6b1d9 replied 2026-10-06 13:05:10 UTC
</log>