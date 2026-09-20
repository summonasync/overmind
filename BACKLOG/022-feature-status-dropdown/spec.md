---
feature: 022
slug: feature-status-dropdown
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Spec 022 — Feature status dropdown on the dashboard card

<decisions>
- The dropdown is a compact styled native Select rendered directly on the board card, replacing the static status Badge — values draft/ready/inProgress/done, current selected. No separate edit-modal trip (decision delegated by the user 2026-09-20).
- Submission rides the existing human door: PUT /projects/{projectId}/planning/features/{slug} with {status} (updatePlanningFeature API fn already exists) — no backend changes in this feature.
- Transitions are unrestricted today (backend SetStatus has no transition rules) — the dropdown offers all statuses; 023-feature-lifecycle-flow will add the state machine and this dropdown will then respect/reflect its rules. No validation logic duplicated client-side.
- Optimistic update on the React Query board cache; onError: rollback + error toast; onSuccess: invalidate the board key. Foreign (MCP-side) status changes keep requiring manual refresh until 017-live-ui-sync lands — this feature does not add polling.
- The card keeps the status color language: the Select is styled to carry the current status tone (featureStatusTone) so the visual identity of the badge survives.
</decisions>

<open-questions>
- none (user delegated autonomous decisions 2026-09-20)
</open-questions>

<requirements>
Web only (features/planning slice):
- feature-board.tsx: replace the status Badge (:76) with a compact Select — same visual weight as the card header allows, tone-colored border/text via featureStatusTone, options draft/ready/inProgress/done, disabled while a mutation for that card is in flight.
- New useMutation (or extension of the existing update flow): updatePlanningFeature(projectId, slug, {status}) — optimistic set on the board list cache, rollback + toast on error, board invalidation on settle. Follow the slice's existing mutation/invalidation/toast patterns.
- a11y: the Select carries an aria-label ("Feature status") since the card has no visible field label.
- types: no new response fields needed (board item already has status).
Verification:
- npm run lint && npx tsc --noEmit && npm run build green; backend untouched (no dotnet changes).
</requirements>

<acceptance-ideas>
- Change a card's status from draft to inProgress → card updates immediately, board reflects it after settle; no edit-modal opened.
- Kill the host (or force a 4xx) → the change rolls back visually with an error toast.
- Status tone colors remain visible on the select for each status value.
- The workspace header Select (existing) and the card dropdown agree after either one changes (shared invalidation).
</acceptance-ideas>
