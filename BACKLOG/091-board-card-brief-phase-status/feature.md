---
feature: 091
slug: board-card-brief-phase-status
status: inProgress
priority: p1
version: 1
created: 2026-09-27
---

# Feature 091 — Board card: feature brief excerpt + ceremony phase status (spec/plan progress visible on the card)

<goal>
The board card answers "what is this feature and where is it in the ceremony" only partially today: it shows number/title/priority/version/task counts, but (1) there is no one-line brief of what the feature is about — the agent-facing queue payload carries no excerpt either, and (2) the status column collapses the whole planning ceremony into `draft`/`inProgress`, so "writing the spec", "planning", "spec validated, writing the plan" all look identical.
</goal>

<requirements>
- Card brief: a short feature excerpt rendered between the priority/version row and the task counter — derived server-side (e.g. first paragraph of the body's `<goal>` section, capped) so the list payload stays cheap; exact derivation rule is a spec question.
- Phase status: render the ceremony phase on the card from data that already ships in the board/queue payloads (specStatus, planStatus, readiness from 072) — e.g. drafting → writing spec → spec validated / planning → planned / ready → in progress → review → done; label wording is a spec question (user sketched: "specing / writing spec / planning / plan validated").
- Both surfaces that render feature cards (dashboard board, task queue projection) should agree on the phase labels.
</requirements>

<non-goals>
- New status enum in the DB — phases are derived from existing spec/plan/task state, not a new persisted status.
- Full body on the card (click-through to the feature workspace already exists).
</non-goals>

<refs>
- User intake 2026-09-27 mid-session ("бриф описания фичи между приоритетом, версией и количеством задач… статусы - если draft -> in progress, то там либо planning/plan mode…").
- 072-planning-task-queue readiness ordinal (0-3) — existing derived decorator.
- 083-feature-body-update (in flight) — its title/body edit tool will keep the brief source fresh.
</refs>

<log>
- 2026-10-03 [auto] — artifact created → inProgress
</log>
