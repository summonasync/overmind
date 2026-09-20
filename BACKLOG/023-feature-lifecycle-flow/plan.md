---
feature: 023
slug: feature-lifecycle-flow
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Plan 023 — Feature lifecycle state machine + board filter persistence

<approach>
Backend-first in two steps: (1) the enum member + all five map sites + the transition policy with UpdateAsync validation — this is the contract everything else hangs on; (2) the event-driven auto-promotions in the artifact service. Then web in two steps: (3) review status + transition-aware dropdowns (rides 022's select), (4) hide-done filter + session persistence. Closeout is a live lifecycle walk on a scratch feature. The importer bypass and the no-MCP-mirror decision keep the doors honest: humans steer manually, agents get promotions for free.
</approach>

<contract>
- PlanningFeatureStatus.Review = 4; PlanningMaterializer.FeatureStatus emits "review"; BacklogImporter.MapFeatureStatus accepts "review"; ManagedBlock.StatusMarker renders [~]; web types/tone/lists gain review (tone "info").
- PlanningFeatureTransitions (Core): draft→{ready,inProgress}, ready→{inProgress,draft}, inProgress→{review,ready,draft}, review→{done,inProgress}, done→{}; UpdateAsync enforces (typed 400 InvalidInput); importer bypasses; auto-promotions bypass (they ARE legal moves by construction).
- PlanningArtifactService: artifact create (incl. unique-retry branch) on draft/ready feature → inProgress; task SetStatusAsync → review when that task is Done AND all sibling tasks Done AND ≥1 task AND spec+plan exist. Never auto-done. No Version bumps.
- Web: features/planning/lib/feature-transitions.ts mirrors the matrix; both selects (board card from 022, workspace header) show allowed targets + current; 400 rollback rides the 022 path.
- Filters: hide-done in the model, default ON, URL writes deviations (hidedone=0), board-filter-bar toggle; full filter object persisted to sessionStorage key overmind.board.filters, restored on mount when URL has no filter params (tree-session.ts helpers).
</contract>

<tasks>
- [ ] 01-review-status-and-transitions — enum member, five map-site patches, PlanningFeatureTransitions, UpdateAsync validation + typed 400, tests (matrix, round-trip) → tasks/01-review-status-and-transitions.md
- [ ] 02-auto-promotions — artifact-create → inProgress (both branches), last-task-done → review (siblings+ceremony guard), tests both triggers + negatives → tasks/02-auto-promotions.md
- [ ] 03-review-status-web — types/tone/lists gain review; feature-transitions lib; both selects transition-aware with rollback on 400 → tasks/03-review-status-web.md
- [ ] 04-hide-done-filter-persistence — filter model + default ON + URL deviation + bar toggle + sessionStorage persistence → tasks/04-hide-done-filter-persistence.md
- [ ] 05-lifecycle-closeout — live lifecycle walk on a scratch feature (draft → ready → auto inProgress → auto review → manual done + rollback), result artifact, feature done, materialize, commit → tasks/05-lifecycle-closeout.md
</tasks>

<risks>
- The 022 board-card select and this feature's task 03 touch the same file (feature-board.tsx) — sequence strictly after 022 is committed to avoid stomping.
- Auto-promotion on task-done must not fire from the importer's task-status writes (import is a bulk restore; disk is truth) — guard the promotion to the SetStatusAsync service path only, or skip when an import is in flight (choose the service-path-only option; importer writes statuses directly, not via SetStatusAsync — verify that assumption in code).
- Done features with artifacts edited later: editing an artifact of a done feature must NOT resurrect it (auto-promotion guard: only draft/ready → inProgress; review→inProgress only via the task-done trigger — actually spec says review rolls back only manually; artifact-create on review does nothing).
- Existing repos: 13 done features re-materialize unchanged; draft features stay draft — no data migration.
- PLANNING_FEATURE_STATUSES order drives UI option order — review slots before done in display order.
</risks>

<verification>
- dotnet build + test green (record counts; baseline ~801 + 012 additions).
- web lint + tsc + build green.
- Live walk (task 05): scratch feature goes draft → ready (manual) → inProgress (auto on spec create via MCP) → review (auto on last task done via MCP) → done (manual via board dropdown); review→inProgress rollback exercised; denied transition 400s surfaced by the select rollback.
- Feature.md round-trip with status: review; README marker [~].
</verification>
