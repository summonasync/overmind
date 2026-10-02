---
feature: 099
task: 04
slug: agent-claims
status: done
review: light
created: 2026-09-30
depends_on: 03
brief: claim chip + liveness on cards, Pinned only filter (pinned=1), overview-panel human door, isCurrent fully out of web
---

# Task 04 — Web UI

<goal>
Humans see who holds what and manage claims from the board: chip with liveness, Pinned only filter, human door in the overview panel.
</goal>

<scope>
- web/src/features/planning/types.ts + api.ts: claim fields, derived state type; remove isCurrent and the pin mutation.
- components/feature-board.tsx: claim chip on the card — «Dan by qwen» + claim age + liveness indicator (alive dot / muted «stale Nm» / ended ghost), following the 084 deadTarget TTL pattern.
- lib/board-filters.ts + lib/use-board-filters.ts + components/board-filter-bar.tsx: «Current only» → «Pinned only», URL param current=1 → pinned=1, filter predicate claimedBy != null.
- pages/planning-page/ui/feature-overview-panel.tsx: pin UI replaced by claim/unclaim (and take-over on stale/ended) human-door actions; pages/planning-page/ui/planning-page.tsx + lib/use-planning.ts updated accordingly (mutations + invalidate per the 096 idiom, Server-Action-free — this app's existing fetch client).
- No isCurrent references left in web/src.
</scope>

<anti-scope>
No backend changes. No attention page integration (spec non-goal).
</anti-scope>

<constraints>
FSD layout as in the repo; Tailwind; React Query patterns already in use here; English UI text; no comments by default; no auto-commits.
</constraints>

<acceptance-criteria>
- [x] npm run lint && npx tsc --noEmit clean
- [x] npm run build clean
- [x] Chip renders alias by vendor + age + correct liveness indicator for alive/stale/ended
- [x] Pinned only filters claimed features; pinned=1 survives reload
- [x] Human door: claim, unclaim, take-over all work from the overview panel against a running host
- [x] grep isCurrent in web/src returns nothing
</acceptance-criteria>

<refs>
- Spec: 0e774399 (UI section).
- Task 03 (order 3) — HTTP endpoints and payload fields this UI consumes.
- Pre-wave snapshot map (HEAD; backend fields arrive with task 03 — re-grep before editing):
  - types.ts:67 isCurrent on the board item type → claim fields (claimedByAlias, claimedByVendor, claimedAt, claimState alive|stale|ended); types.ts:144-152 PlanningCurrentFeature/PlanningCurrentResponse belong to the onboard currentSlug block — their fate follows task 03's onboarding claim block.
  - api.ts:67-73 setPlanningFeatureCurrent (POST .../current) → replaced by claim/unclaim calls to the task-03 endpoints.
  - use-planning.ts:14,73-74 setFeatureCurrent mutation → useClaim/useUnclaim + invalidate per the 096 idiom.
  - feature-board.tsx:5 (Pin/PinOff icons), :202 (current border), :222 (Badge "current") → claim chip «Dan by qwen» + age + liveness indicator (084 TTL pattern); :270-274 Pin/Unpin button → Claim / Unclaim / Take over (take-over enabled when claimState is stale or ended).
  - feature-overview-panel.tsx:51 Badge "current" → claim block (holder, age, state, human-door actions).
  - planning-page.tsx:65-66 handleSetCurrent → handleClaim/handleUnclaim.
  - Filters: board-filters.ts (currentOnly at :19,40,63,87,99,117 — predicate :117 item.isCurrent → claimedBy != null), use-board-filters.ts (:20 param list, :83-86 current=1 → pinned=1), board-filter-bar.tsx (:146-151 chip label "Current only" → "Pinned only").
</refs>

<obstacles>
- AUTHORIZED BACKEND DEVIATION (review 099/03 labeling note, user-approved): PlanningFeature gained a `ClaimedByHuman` boolean (set by the human door on claim via FeatureClaimService byHuman flag, cleared on Release), migration AddClaimedByHuman (Up = plain AddColumn; Down = native raw DROP COLUMN — same FTS-trigger reasoning as AddAgentClaims, whose down-up pinning test immediately caught the generated DropColumn rebuild on the new migration), flag surfaced as claimedByHuman on every claim-bearing payload (board item, queue item, feature response, roster, claim echo) through PlanningContracts + PlanningMapper + FeatureClaimEcho. Board handler signature grew FeatureClaimService (liveness resolution), so both Board call paths (human + agent doors share it) pass the service.
- Human-held rendering: the chip shows a muted "human-held · age" span (no liveness dot — no live agent implied), tooltip records the holder identity; agent claims keep the spec chip (success tone + alive dot / muted "stale Nm" / ghost "ended"). The dot is a styled span because Badge base classes force capitalize which would mangle "Dan by qwen".
- Board claim button sends a fixed board identity (alias "board", vendor "human") — the human door mints its own session row, so the claim goes stale like any other and stays take-over-able; take-over IS the claim button on a claimed feature (per the brief: simple and honest, no separate take-over control).
- PlanningCurrentFeature/PlanningCurrentResponse types were dead in web (zero consumers beyond the barrel export after the pin removal) — deleted along with the barrel exports; the current-TASK HTTP surface is untouched.
- Pin border on claimed cards dropped entirely: claim is transient coordination state, not a board landmark; the chip carries the signal.
- Filters: currentOnly → pinnedOnly (predicate claimedByAlias !== null), URL param current=1 → pinned=1 (saved session filters from before the rename degrade to unpinned — acceptable, pin is transient state).
- Backend gates for the labeling deviation: dotnet build 0/0; Core 252, Infra 552, Api 570 all green (Infra includes the FTS down-up pinning test catching and verifying the migration fix). Web gates: npm run lint 0 errors (6 pre-existing img warnings in app-shell, untouched), npx tsc --noEmit clean, npm run build clean, npm test 48/48, grep isCurrent in web/src — zero hits.
</obstacles>

<log>
- 2026-10-01 [agent] — deploy sequencing note from 099/01 review: between task 01 (backend isCurrent removed) and task 04 (web catches up) the board filter is dead in web — this wave must land as ONE commit/deploy, no intermediate releases.
- 2026-10-02 [agent] — input from review 099/03: human-held claims are indistinguishable from agent claims in the payload today (no holder-kind field). Task 04 UI should surface a human-held claim distinctly (e.g. muted/no liveness dot or a «human» marker) so the board does not imply a live agent sits on the feature. Coordinate payload shape with the task 03 fix round if a field is needed.
- 2026-10-02 [agent] — dispatched to dotnet-coder (same session), task 03 done; brief includes the human-held claim labeling note from review 099/03
- 2026-10-02 [agent] — Executed by dotnet-coder. Web: claim fields + PlanningClaimState in types (isCurrent and the dead PlanningCurrentFeature/Response types removed), claimPlanningFeature/unclaimPlanningFeature api calls, useClaimFeature/useUnclaimFeature mutations with board+feature invalidation, FeatureClaimChip (success+alive dot / muted "stale Nm" / ghost "ended" / muted "human-held · age" for human claims with no liveness dot), board Claim/Unclaim buttons (human claim on a claimed feature IS the take-over), overview-panel claim block with human-door actions, Pinned only filter (pinnedOnly, pinned=1, predicate claimedByAlias !== null), zero isCurrent in web/src. AUTHORIZED backend deviation for the 099/03 labeling note: PlanningFeature.ClaimedByHuman + migration AddClaimedByHuman (Down = native DROP COLUMN) + claimedByHuman on all claim payloads. Gates: web lint 0 errors / tsc clean / build clean / tests 48-48; backend build 0/0, Core 252, Infra 552, Api 570 green.
- 2026-10-02 [agent] — task 04 executed to review: web lint/tsc/build/test all clean (48-48), zero isCurrent in web/src; authorized labeling backend landed (ClaimedByHuman + migration + payloads) with backend gates green (build 0/0, Core 252, Infra 552, Api 570); all 6 ACs ticked; ready for dotnet-code-reviewer (light review per plan)
- 2026-10-02 [agent] — 2026-10-02 [agent] — reviewed by web-code-reviewer (web lane): PASS-WITH-NITS — 0 blockers, 6 cosmetic nits (sr-only alive word, stale Nm format, barrel order/types, invalidation dup → microbatch, card button pending-disable); chip contract matches backend 1:1, human-held cannot render a live dot, isCurrent zeroed across web/src, old filters degrade; gates re-verified: lint 0e/6 pre-existing w, tsc 0, build ok, 48/48; report audits/review-099-04-web.md.
- 2026-10-02 [agent] — 2026-10-02 [agent] — reviewed by dotnet-code-reviewer (backend lane): PASS — ClaimedByHuman flow/migration/payloads verified, FTS-safe down guarded by the 01 pin, 0 blockers, 1 micro-nit (human→agent transition pin); build 0/0, Core 252 / Infra 552 / Api 570 re-verified, Mcp 419/419 isolated (1 parallel-load stub flake in full run); report audits/review-099-04-backend.md.
- 2026-10-02 [agent] — Micro fix round (4 nits, both reviews green, no re-review needed): (1) stale chip wording now «stale Nm» via formatBacklogAge (was «stale 3m ago» via formatRelativeTime) — human/ended/alive age suffixes switched to the same «Nm» format; (2) alive chip carries an sr-only «alive» word next to the aria-hidden dot (084 notification-row sr-only pattern); (3) board Claim/Unclaim button gained pending-disable — claimPending threaded from planning-page (claimFeature.isPending || unclaimFeature.isPending), matching the overview twin; (4) backend micro: new FeatureClaimServiceTests.HumanClaim_ThenAgentTakeOver_ClearsHumanFlag pins human claim (ClaimedByHuman true) → aged human session → agent take-over (ClaimedByHuman false). Gates: web lint 0 errors (6 pre-existing warnings), tsc clean, npm test 48/48; dotnet build 0/0, Infra 553, Core 252, Api 570 all green.
- 2026-10-02 [agent] — 2026-10-02 [agent] — micro fix round: stale Nm format (formatBacklogAge), sr-only «alive» word, card button pending-disable, HumanClaim_ThenAgentTakeOver_ClearsHumanFlag pin (first run honestly failed 409 — live claims are not agent-take-over-able, test now ages the session to stale first). Web gates: lint 0e, tsc clean, 48/48; backend: Infra 553, Core 252, Api 570, build 0/0. Orchestrator verified gates; closing done — all review verdicts were green pre-round.
- 2026-10-02 [agent] — dual-lane review PASS (web 0 blockers, backend 0 blockers) + micro fix round applied and verified. AC 6/6. Closing as done.
</log>