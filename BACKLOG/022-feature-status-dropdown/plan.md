---
feature: 022
slug: feature-status-dropdown
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Plan 022 — Feature status dropdown on the dashboard card

<approach>
Single-surface web feature: the status Badge on the board card becomes an inline Select with optimistic mutation + rollback. No backend, no new contract — everything rides the existing human door and board cache. One coding task, light review, orchestrator closeout.
</approach>

<contract>
- feature-board.tsx card: tone-styled Select (draft/ready/inProgress/done) replaces the status Badge; aria-label "Feature status"; disabled while that card's mutation is in flight.
- Mutation: updatePlanningFeature(projectId, slug, {status}) — optimistic cache update on the board list key, rollback + error toast on failure, invalidate board on settle.
- No backend changes; no polling (017's job later); transition rules deferred to 023.
</contract>

<tasks>
- [ ] 01-status-dropdown-board-card — Select on the card, optimistic mutation, rollback/toast, verification → tasks/01-status-dropdown-board-card.md
</tasks>

<risks>
- Optimistic update must scope to the mutated card (slug) in the board list array — a naive whole-list replace can clobber concurrent foreign changes visible in the cache.
- The workspace header Select and the card dropdown share the board invalidation — verify both stay consistent after a card change.
</risks>

<verification>
- lint + tsc + build green; manual sanity: change a status from the card → board updates without modal; failure path rolls back with toast.
</verification>
