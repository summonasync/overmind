---
feature: 110
slug: ready-means-ready-status-honesty
status: review
priority: p1
version: 1
created: 2026-10-04
---

# Feature 110 — Status honesty: spec/plan grooming keeps the feature ready; inProgress only when a task is taken

<goal>
Feature status honesty: writing the spec and plan keeps the feature in READY, not inProgress. inProgress means ONLY «tasks are cut and at least one is taken into work». Today «artifact create → inProgress» fires the moment a spec draft exists — a feature being groomed looks «in progress» while nothing is being worked on.
</goal>

<context>
User decision 2026-10-04 in session: «когда мы пишем спеку и план, таска должна быть в ready, а не inProgress — inProgress ТОЛЬКО когда задачи написаны и уже взяты в работу». Current auto-transition (artifact create → inProgress) is the offender; the 028 dogfood showed the board littered with inProgress features that are really drafts-in-ceremony.
</context>

<open-questions>
Data-fix at deploy: features currently inProgress with NO task beyond todo (or no tasks at all) roll back to ready; features with a taken task (inProgress/review/done) stay. Default: yes, one-time data-fix. Challengeable on grooming.
</open-questions>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
- 2026-10-05 [agent] — Van by claude claimed the feature
- 2026-10-05 [auto] — all tasks done + spec + plan validated → review
</log>
