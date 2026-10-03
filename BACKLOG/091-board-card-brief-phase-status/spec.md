---
feature: 091
slug: board-card-brief-phase-status
status: validated
created: 2026-10-03
validated: 2026-10-03
---

# Spec 091 — Board card: feature brief excerpt + ceremony phase status (spec/plan progress visible on the card)

## Goal

The board card answers "what is this feature and where is it in the ceremony" in full: a one-line brief between the priority/version row and the task counter, plus a ceremony phase label derived from existing spec/plan/task state — so `draft`/`inProgress` no longer collapse "writing spec", "planning", and "spec validated, writing the plan" into one blob.

## Decisions (user-approved in chat, 2026-10-04)

- **Brief derivation:** server-side, from the first non-empty paragraph of the body's `<goal>` section, capped at ~150 characters with an ellipsis. Empty goal / no body → no brief line rendered at all (no placeholder stubs). Derived once server-side so both the web board payload and the agent-facing queue payload stay cheap.
- **Excerpt also ships in the agent queue payload** (`planning_task_queue` entries) — agents get the same one-glance context.
- **Phase label machine** — derived from existing specStatus/planStatus/taskDone/taskTotal/readiness (072), NO new persisted status:

| State | Card label |
|---|---|
| draft, no spec artifact | `drafting` |
| spec draft (exists, being written) | `writing spec` |
| spec validated, no plan / plan draft | `planning` |
| spec + plan validated, tasks not started | `plan validated` |
| inProgress (tasks in flight) | `in progress` |
| review | `review` |
| done | `done` |

## Surfaces

- Dashboard board card: brief between priority/version row and task counter; phase label next to the status column (both surfaces agree on labels).
- `planning_task_queue` entries: excerpt field + phase label, same derivation.
- Done features: phase shows `done` (or the card suppresses the phase — follow the existing done-card treatment; do not invent a second done representation).

## Edge cases

- Feature without a body/goal → brief line absent, phase still derived from artifact states.
- Spec/plan validated but feature still draft (the 037-era no-plan quirk) → phase follows the machine (plan validated ≠ in progress); lifecycle status stays the source of truth for the status column.
- Phase derivation lives server-side (shared by both surfaces) — one function, tested.

## Non-goals

- New status enum in the DB; full body on the card; task-queue semantic changes beyond the excerpt field.

<log>
- 2026-10-04 [agent] — spec drafted from Socratic gate: goal-paragraph derivation capped 150, phase machine table approved (drafting separate), excerpt in queue payload, edge cases pinned.
- 2026-10-03 [agent] — validated with human proof: notification f4570cde-e50b-4021-b12b-cbce1f43e7d5 replied 2026-10-03 21:25:14 UTC
</log>