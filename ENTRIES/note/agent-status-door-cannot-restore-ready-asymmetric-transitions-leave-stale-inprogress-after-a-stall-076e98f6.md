---
id: 076e98f6-ad51-41db-9910-9d0c158a67de
type: note
title: Agent status door cannot restore `ready` — asymmetric transitions leave stale inProgress after a stall
tags: agent-api, from-squeezr-dogfood, planning, status-transitions
createdAt: 2026-10-04T12:03:42Z
updatedAt: 2026-10-04T12:03:42Z
---

## The problem

`planning_set_feature_status` (agent door) accepts exactly two targets: `done` (from any non-done status) and `inProgress` (rollback). Every other manual move belongs to the human UI door. The board now also uses a `ready` band — "validated and not yet started" — which agents can read but cannot write.

Consequence: the moment an agent dispatches work on a `ready` feature it must move it to `inProgress` (otherwise the board says "not started" while an agent is editing the tree — this actually happened on 2026-10-04 with Squeezr feature `064-secret-path-guard`: the human moved it to `ready` while its task 1 was already being executed). But if that work then stalls halfway — reviewer returned blockers, the coder crashed, the session ended, the machine rebooted — the feature is stuck in `inProgress` and **only a human hand can put it back to `ready`**. Nobody watches the UI overnight, so the stale status persists and the queue lies again.

## Asymmetry, stated precisely

| transition | risk | who can do it today |
|---|---|---|
| `ready` → `inProgress` | none (nothing is being destroyed, work is starting) | agent |
| `inProgress` → `ready` | none — pure demotion of an overstatement, no artifact changes | **human only** ← this is the gap |
| anything → `done` | high (closes the feature, hides it from the queue) | agent |
| `done` → `inProgress` | high (reopens closed work) | agent |

The risk ordering is inverted relative to the permissions: the two destructive-ish transitions are open to agents, while the safe, purely corrective one is not. That makes an autonomous pipeline structurally unable to leave the board in an honest state, which is the whole point of having a source-of-truth planner.

## Proposed fix (either shape works)

1. Add `ready` to the agent-allowed targets of `planning_set_feature_status`, under the same rule as `inProgress`: requires a `logNote` with provenance, so every move is still auditable. Promotion *into* `done` can stay the gated one.
2. Or, if `ready` is meant to be human-only by design, give agents a distinct non-status flag meaning "started, not making progress" (e.g. `held`/`paused` on the task level) so the honest state is expressible without a UI round-trip.

Worth noting alongside: `planning_current_task` on a feature whose tasks are all `ready` hands out work regardless of the feature-level status, so the status band is presentational for the queue but load-bearing for humans reading it — which is exactly why a wrong band matters, and why letting the agent that created the mismatch fix it is the cheap correction.

## Repro from tonight

Squeezr project `31f33e30-6056-40a3-8447-c43d889fee9f`, feature `064-secret-path-guard`: plan validated 2026-10-04 11:33 UTC, six tasks created, task 1 dispatched to a coder agent 11:37 UTC; the human moved the feature to `ready` afterwards; the agent could only correct it forward into `inProgress` (log note: "moved on the user's standing rule… the board said ready while an agent was already editing the tree"). Had the coder failed at that moment, there would have been no way to restore `ready`.
