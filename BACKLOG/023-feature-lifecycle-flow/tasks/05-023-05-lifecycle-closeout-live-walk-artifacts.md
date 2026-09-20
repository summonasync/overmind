---
feature: 023
task: 05
slug: feature-lifecycle-flow
status: done
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
- 2026-09-20 — Closeout started — orchestrator runs the live lifecycle walk against an isolated dev host (scratch DB in a tmp cwd, port 4799 via Overmind__BindUrl; the production 0.1.7 host and its DB untouched).
- 2026-09-20 — Live walk PASSED on an isolated dev host (scratch DB, port 4799 via Overmind__BindUrl; production 0.1.7 host and DB untouched, verified alive after): draft → manual ready (200) → spec create AUTO→inProgress → last task done AUTO→review → manual review→inProgress rollback (200) → task reopen+re-done AUTO→review again → manual done (200, final). Denied: draft→done PUT → 400. Bonus live proof: task brief on the wire ('walk the lifecycle' — feature 021 end-to-end in the new build). Scratch host killed by exact PID; scratch project+feature died with the tmp DB.
- 2026-09-20 — Live walk PASSED end-to-end on the deployed 0.1.9 build (version 288e7b5, host 4761), scratch feature zz-lifecycle-walk, evidence per step: draft (create); draft→ready manual PUT 200; ready→inProgress AUTO on spec create via MCP; task1 done → stays inProgress; task2 done → AUTO review (spec+plan present); rollback review→inProgress manual PUT 200; task2 reopen+re-done → AUTO review again; task3 CREATED on review → AUTO demote to inProgress (023/06 fix proven live — the exact 025 incident scenario); inProgress→done one PUT 200 (escape hatch 023/07); done→inProgress → 400 InvalidFeatureTransition "allowed targets: ." (terminal); scratch deleted 204. All lifecycle paths exercised green.
- 2026-09-20 — Done: live walk executed with full evidence (see log) on the deployed build — every auto-promotion, the manual rollback, the 023/06 demote path, the escape-hatch close, and the denied terminal transition. Contract satisfied; cleanup done.
</log>
