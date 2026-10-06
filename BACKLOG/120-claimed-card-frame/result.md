---
feature: 120
slug: claimed-card-frame
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Result 120 — Board card: claim frame (pin-frame successor)

<goal>
The user's 2026-10-04 design landed on the board card: a claimed feature reads at a glance — claim line `claimed by {Alias} from {Vendor}` in the header row immediately left of the status column, and a `border-primary` frame around actively held cards (the successor of the pre-099 pinned-card frame). Both tasks shipped and were approved on light review, 0 blockers each.
</goal>

## What landed

- **Claim line** (commit 3f706cc): rendered for ANY claim — stale and ended claims keep naming their holder per the 099/04 chip semantics; the text is invariant, no state words, no age (state words stay on FeatureClaimChip). Vendor-null fallback lives in the lib: `claimed by {Alias}` without a dangling `from`.
- **Liveness presentation**: success tone on the line + `border-primary` frame ONLY for actively held claims (alive agent claim or human-held); stale/ended/null-liveness render the line muted with no frame.
- **Unclaimed cards pixel-identical**: no element, no reserved space, classes untouched; drop-target ring and hover affordances unchanged.
- **Helpers, not component formatting**: `cardClaimLine` / `cardClaimActive` in `web/src/features/planning/lib/board-card-context.ts`; the component consumes them verbatim — formatting logic stays out of `feature-board.tsx`.
- **Pin coverage** (commit e580235): 8-pin matrix over format + liveness (exact string, unclaimed null, vendor-null fallback, alive/stale/ended/null/human/unclaimed) in `web/tests/board-card-context.test.mts`, existing node:test style.

## Verification

- Gates green in web/: lint, `tsc --noEmit`, node --test suite (254), `next build`.
- Light reviews: task 01 APPROVED (0 blockers), task 02 APPROVED (0 blockers); task 02's claim-line/border classes additionally verified against the built chunk (CSS order confirmed).
- Commits: e580235 (task 01), 3f706cc (task 02). Work preservation commits only, nothing pushed.

## Notes for the human

- Eyeball the claimed-card look in the browser: success-toned claim line hugging the status column + purple frame on the actively claimed card; unclaimed cards should look exactly as before. (The 120 card itself is the live specimen once the host runs this build.)
- SUGGESTION filed to ledger #249: pin the stale/ended line-rendering behavior (that the holder name still shows muted) — batched for a later microbatch pass, not a blocker.

<refs>
- Spec c6f38160-de30-46c8-9538-c02c429df482 (validated) — binding format + liveness decision.
- Plan bb7b524e-180a-4ce9-95c7-94709aa73b64 (validated) — two-task breakdown, both executed as written.
- Tasks 01 (98f158ea) and 02 (bd3bc555) — done, review APPROVED ×2.
- 099-agent-claims (done) — claim payload fields and liveness semantics presented here.
- 117-phase-label-as-tag-chip (draft) — same header row region; sequenced separately by the main session, no conflicts encountered.
</refs>

<obstacles>
None during execution. The spec's slot interpretation («left of the status column», brief line untouched) was confirmed by approval of task 02. Ledger #249 carries the one open suggestion (see Notes).
</obstacles>

<log>
- 2026-10-06 [agent] — Result drafted from approved tasks 01/02 (commits e580235, 3f706cc): claim line for any claim, frame + success tone for alive/human only, 8-pin matrix, gates green, reviews 0 blockers ×2.
</log>