---
feature: 110
slug: ready-means-ready-status-honesty
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 110 — Status honesty: spec/plan grooming keeps the feature ready; inProgress only when a task is taken

> Grooming 2026-10-04 (user in session, decision given directly).

## Goal

The lifecycle states stop lying: **ready** = ceremony artifacts may exist (spec/plan in drafting or validated, tasks maybe cut), but NOTHING is being worked on. **inProgress** = at least one task is taken into work (inProgress/review/done). Today «artifact create → inProgress» promotes a freshly drafted spec into «in progress» — the board shows work that isn't happening (028 dogfood: a pile of inProgress features that are really in ceremony).

## Decisions

- **Auto-transition rewrite:** artifact create / upsert NO LONGER promotes to inProgress; the ceremony keeps the feature in `ready` (draft → ready stays: the first spec draft makes it ready — «queued for work, being groomed»).
- **inProgress fires on task take:** the first task moving to inProgress (either door) promotes the feature ready → inProgress (the existing review-rollback machinery already returns features to inProgress from review — that path keeps working and now has honest company).
- **Demote is NOT automatic in the other direction** (a feature whose tasks are all blocked/todo after work started stays inProgress — the ceremony state, not a guess; challengeable).
- **One-time data-fix at deploy:** inProgress features with no task beyond todo (or no tasks) → ready; features with any taken task stay. [auto] log line per moved feature.
- Derivations that READ status keep reading it — 091's phase planner already prefers machine state over lifecycle for the label, so the ceremony context survives the shift.

## Non-goals

- Changing task-level statuses; changing review/done rules (109's territory); new statuses.

<log>
- 2026-10-04 [agent] — groomed with the user in session per their direct decision: artifact create keeps ready, task-take promotes to inProgress, one-time data-fix, no auto-demote.
- 2026-10-04 [agent] — validated with human proof: notification 4f14cb8d-22c2-4937-aa32-4547980fab75 replied 2026-10-04 12:03:14 UTC
</log>