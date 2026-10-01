---
feature: 099
task: 04
slug: agent-claims
status: todo
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
- [ ] npm run lint && npx tsc --noEmit clean
- [ ] npm run build clean
- [ ] Chip renders alias by vendor + age + correct liveness indicator for alive/stale/ended
- [ ] Pinned only filters claimed features; pinned=1 survives reload
- [ ] Human door: claim, unclaim, take-over all work from the overview panel against a running host
- [ ] grep isCurrent in web/src returns nothing
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

<obstacles></obstacles>

<log></log>

<log>
- 2026-10-01 [agent] — deploy sequencing note from 099/01 review: between task 01 (backend isCurrent removed) and task 04 (web catches up) the board filter is dead in web — this wave must land as ONE commit/deploy, no intermediate releases.
</log>
