---
feature: 023
task: 05
slug: feature-lifecycle-flow
status: todo
review: light
created: 2026-09-20
---

# 023/05 — Lifecycle closeout (live walk + artifacts)

<goal>
The lifecycle walks live on a scratch feature — draft → ready (manual) → inProgress (auto on spec create via MCP) → review (auto on last task done via MCP) → done (manual via board dropdown) — with the review→inProgress rollback exercised; then result artifact, feature done, materialize, single feat-commit (no push).
</goal>

<context>
Feature 023 closeout. Tasks 01–04 done and reviewed first. Orchestrator-driven; MCP-side steps use the running host once it carries the new build (dev run or 0.1.8 pack — orchestrator's call; if the live host cannot be upgraded mid-session, the walk runs against a locally started host on a scratch DB).
</context>

<scope>
- Live walk on a scratch feature in the Overmind project (create via MCP, then drive): every step's evidence captured (status before/after).
- review→inProgress rollback: reopen a task via MCP after review, flip back manually if the auto path doesn't cover it (per spec, rollback is manual — dropdown).
- Denied transition check: attempt draft→done via API → 400 evidence.
- Cleanup: scratch feature deleted after evidence.
- Write the 023 result artifact (outcome, evidence, deferred), validate, feature done via human door, unpin current, re-materialize, report changed files, single feat-commit per the standing instruction (no push).
</scope>

<anti-scope>
- No new code beyond fix-loops for live findings (through the normal task status flow).
- No 0.1.8 pack unless the orchestrator decides it is needed for the walk — prefer the local dev host.
</anti-scope>

<constraints>
- The local dev host must not touch the production ~/.overmind DB if a scratch DB is used — check the host's DB resolution before starting it.
- pkill exit codes lie (-1) — verify via health curl.
</constraints>

<acceptance-criteria>
- [ ] Full lifecycle walk green with captured evidence at each transition (2 auto + 3 manual + 1 rollback + 1 denied).
- [ ] Result artifact written + validated; feature done via human door; planning re-materialized; single feat-commit, no push.
</acceptance-criteria>

<refs>
- Spec + plan: feature 023 (verification section lists the walk).
- tasks/01–04 for the implemented behavior.
</refs>

<obstacles>
</obstacles>

<log>
</log>
