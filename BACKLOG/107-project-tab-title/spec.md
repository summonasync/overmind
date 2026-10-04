---
feature: 107
slug: project-tab-title
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 107 — Tab title shows the open project («{Project} — Overmind»)

> Grooming 2026-10-04 (user in session, p1 intake same day).

## Goal

Browser tab reads «{Project name} — Overmind» when a project is open (resolved from the `?project=` URL parameter — the name resolver from the 101 wave already exists); plain «Overmind» elsewhere. The awaiting counter (087/093) keeps composing on top.

## Decisions

- **One title owner:** a single hook computes `document.title = f(projectName, awaitingCount)` — the awaiting hook and the project name stop being two independent writers. This resolves microbatch #134's latent constraint (base captured once on mount goes stale the moment the base is dynamic): the base is now reactive, ownership is singular.
- Unresolvable project id → plain «Overmind» fallback (graceful, no «Unknown project» noise in the title).
- Tests: title for project routes, fallback, awaiting count composing over a project base.

## Non-goals

- Per-page titles beyond the project segment (feature name in the tab is a future thought); favicon changes.

<log>
- 2026-10-04 [agent] — groomed with the user in session: single title owner f(projectName, awaitingCount), #134 constraint resolved by design, graceful fallback.
- 2026-10-04 [agent] — validated with human proof: notification 7a943573-4bd9-43e5-80ca-9462bfbe3402 replied 2026-10-04 12:00:47 UTC
</log>