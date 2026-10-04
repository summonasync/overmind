---
feature: 110
task: 01
slug: ready-means-ready-status-honesty
status: todo
review: light
created: 2026-10-04
brief: Backend: transition rewrite (ceremony=ready, take=inProgress) + startup data-fix + tests
---

# Task 01 — Transition rewrite + data-fix

<goal>
ready means «not being worked on»; inProgress means a task is taken.
</goal>

<scope>
- Auto-transition «artifact create → inProgress» removed: ceremony writes (spec/plan upserts, validations) keep/make the feature ready (draft → ready on the first spec draft stays).
- First task-take (either door, task → inProgress) promotes the feature ready → inProgress with an [auto] line; review-rollback path untouched.
- One-time data-fix on startup: inProgress features with no taken task (none, or all todo) → ready with an [auto] log line; features with any taken task stay.
- No auto-demote (challengeable default recorded).
- Tests: ceremony keeps ready; take promotes; data-fix matrix (no tasks / all todo / one taken); review paths intact.
</scope>

<acceptance-criteria>
- [ ] Spec/plan upserts + validation leave the feature ready — pinned.
- [ ] Task-take promotes ready → inProgress with [auto] line — pinned.
- [ ] Data-fix matrix pinned (startup migration).
- [ ] Review/rollback paths green.
</acceptance-criteria>

<refs>
- Spec c323a41a; plan 9f3b98c1 (task 1); 091 phase planner (machine-state preference).
</refs>