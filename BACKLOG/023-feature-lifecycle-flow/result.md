---
feature: 023
slug: feature-lifecycle-flow
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 023 — Feature lifecycle state machine + board filter persistence

<outcome>
023-feature-lifecycle-flow shipped: the feature lifecycle became an honest state machine — draft → ready → inProgress → review → done — with a new Review status (enum value 4, additive, no migration), an explicit Core transition policy enforced by UpdateAsync (typed 400 InvalidFeatureTransition, no partial writes, importer bypassed — disk is truth), event-driven auto-promotions in PlanningArtifactService (artifact create on draft/ready → inProgress; last task done with all tasks done + spec + plan → review; never auto-done; no version bumps), and the web fully transition-aware (types/tone/lists gain review; both status selects offer only legal targets from a shared mirror map, done locks the select; filter chips picked review up automatically).

Board filters: hide-done default ON (URL records deviations only, hidedone=0); explicit status selection wins over hide-done; the full filter object persists across project switches in sessionStorage (restore on a blank URL). The three-copy Write/Preview extraction and shared/lib session-json helpers rode along as hygiene.

Suite 802 → 831 (+29). Reviews: 01 PASS (2 NITs — AllowedTargets immutability fixed in-tree; validate-then-mutate → microbatch), 02 PASS (4 INFO), 03 PASS (3 INFO), 04 PASS (3 INFO).
</outcome>

<evidence>
- LIVE lifecycle walk green on an isolated dev host (scratch DB in a tmp cwd, port 4799 via Overmind__BindUrl; the production 0.1.7 host untouched and verified alive after): draft → manual ready → spec create auto-flips inProgress → last task done auto-flips review → manual review→inProgress rollback → task reopen + re-done auto-flips review again → manual done. Denied draft→done → 400 with the typed title. Bonus live proof: feature-021 task brief rode the wire through the new build ('walk the lifecycle').
- dotnet build 0w/0e; dotnet test 831/831 (Core 106, Infra 283, Mcp 203, Api 239): all 20 ordered transition pairs pinned (9 allowed + 11 denied with status-unchanged asserts), auto-promotion triggers + negatives, materializer/importer round-trip of status: review without downgrade, README marker [~].
- Web: lint 0 errors / tsc 0 / build green (10/10 routes) across 03+04; Record compile-safety for the new status proven experimentally by the reviewer (TS2741 on a missing key).
- Reviews: audits/review-023-01..04.md — no blockers anywhere.
- Existing features' statuses untouched by the deploy (draft stays draft — no data migration; the 16 pre-existing done features simply hide behind the new default filter).
</evidence>

<deferred>
- Microbatch: validate-then-mutate ordering in UpdateAsync; AsNoTracking-hazard comment on the load-bearing siblings query (identity resolution is required); workspace select lacks isPending disable (022 asymmetry); done-lock hardcoded in two components instead of deriving from empty allowedTransitions; hide-done chip dimming under explicit status selection; Ready→Review auto-path asymmetry (sanctioned, recorded against false bug reports); no auto-demotion on task reopen (per spec — rollback is manual).
- Agent door still has no PUT UpdateFeature mirror (deliberate non-goal — manual transitions are human-owned; agents get auto-promotions for free through the shared services).
- UI visual pass + packed release (0.1.8) pending — same posture as 021/012/017/022.
</deferred>
