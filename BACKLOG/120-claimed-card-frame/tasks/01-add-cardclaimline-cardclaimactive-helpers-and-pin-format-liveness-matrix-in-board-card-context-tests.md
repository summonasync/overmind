---
feature: 120
task: 01
slug: claimed-card-frame
status: done
review: light
created: 2026-10-06
brief: Add cardClaimLine/cardClaimActive helpers and pin format + liveness matrix in board-card-context tests.
---

<goal>
Add the claim-line presentation helpers and pin them: `cardClaimLine(item)` returns the exact text `claimed by {Alias} from {Vendor}` for any claim; `cardClaimActive(item)` decides whether the claim is actively held — task 02 consumes both for the card frame and line tone.
</goal>

<scope>
- web/src/features/planning/lib/board-card-context.ts:
  - `cardClaimLine(item: PlanningFeatureBoardItem): string | null` — `claimed by ${item.claimedByAlias} from ${item.claimedByVendor}` when `claimedByAlias !== null`, else null. Vendor-null fallback: render without the `from` tail (`claimed by {Alias}`), never the literal "null".
  - `cardClaimActive(item: PlanningFeatureBoardItem): boolean` — `item.claimedByHuman === true || item.claimState === "alive"`; unclaimed → false; null claimState → false.
- web/tests/board-card-context.test.mts — pin tests in the file's existing node:test + node:assert strict style (reuse the `item()` fixture factory):
  - exact format: `claimed by Van from claude`;
  - unclaimed → null;
  - vendor null → `claimed by Van` (no dangling `from`);
  - active matrix: alive → true; stale → false; ended → false; null claimState → false; human → true regardless of claimState; unclaimed → false.
</scope>

<anti-scope>
No component changes (feature-board.tsx is task 02), no FeatureClaimChip changes, no type changes, no backend work.
</anti-scope>

<constraints>
- Claim line text is invariant — no state words, no age suffix (age and state words stay on FeatureClaimChip).
- Match the file's existing style; no comments; English only.
</constraints>

<acceptance-criteria>
- [x] Both helpers exported from board-card-context.ts with the signatures above.
- [x] Pin tests cover exact format, unclaimed null, vendor-null fallback, and the full active matrix (alive / stale / ended / null / human / unclaimed).
- [x] `node --test tests/board-card-context.test.mts` green in web/.
</acceptance-criteria>

<refs>
- Plan bb7b524e-180a-4ce9-95c7-94709aa73b64 (task 1); spec c6f38160-de30-46c8-9538-c02c429df482 (binding: exact format + liveness decision).
- web/src/features/planning/types.ts — PlanningFeatureBoardItem claim fields (already on the payload, nothing new).
- 099-agent-claims (done) — liveness semantics (stale/ended/human) this predicate encodes.
</refs>

<obstacles>
None at creation.
</obstacles>

<log>
- 2026-10-06 [agent] — Task created from the validated plan, task 1.
- 2026-10-06 [agent] — task 01 coding round started
- 2026-10-06 [agent] — cardClaimLine/cardClaimActive implemented in board-card-context.ts; 8 pin tests added covering exact format, unclaimed null, vendor-null fallback and the full active matrix. Gates green: node --test 13/13, full suite 254/254, lint 0 errors, tsc clean, next build ok. Commit e580235.
- 2026-10-06 [agent] — coding round complete — light review gate
- 2026-10-06 [agent] — light review APPROVED — 0 blockers, 1 SUGGESTION (line-for-stale/ended invariant un-pinned — filed to ledger #249). Mutation check clean, spot-check 13/13, anti-scope 2 files. Commit e580235.
</log>