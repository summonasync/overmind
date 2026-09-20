---
feature: 023
slug: feature-lifecycle-flow
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Spec 023 — Feature lifecycle state machine + board filter persistence

<decisions>
- `Review` joins PlanningFeatureStatus as a new member (value 4) — additive to the enum, integer-persisted by EF convention (no migration), serialized as "review" by the existing camelCase JsonStringEnumConverter. All five exhaustive map sites are patched in the same change: PlanningMaterializer.FeatureStatus (currently THROWS on unknown — would break materialization), BacklogImporter.MapFeatureStatus (currently silently downgrades unknown to draft — round-trip loss), ManagedBlock.StatusMarker (review renders `[~]`, same as inProgress — README marker stays coarse), web types.ts union + PLANNING_FEATURE_STATUSES, web status-tone (review: "info", the artifact-status precedent).
- The lifecycle is an explicit allowed-transitions map in Core (single source, e.g. PlanningFeatureTransitions): draft→{ready, inProgress}, ready→{inProgress, draft}, inProgress→{review, ready, draft}, review→{done, inProgress}, done→{} … backward review→inProgress allowed (new work appeared), done reachable ONLY by explicit human/UI action (auto-transitions never set done). Disallowed transition → PlanningFailureKind.InvalidInput → 400. Validation lives in PlanningFeatureService.UpdateAsync only — the importer writes status from disk and bypasses transition checks (disk is the source of truth on import).
- Auto-transitions are event-driven in the Core services (both doors covered implicitly since they share the services): creating any artifact (spec/plan/task) on a draft/ready feature → inProgress (hook: PlanningArtifactService.UpsertAsync create+retry branches, feature already loaded there); a task reaching Done with ALL sibling tasks Done and no spec/plan gaps → review (hook: SetStatusAsync after the task save — load siblings+feature, mirror the AgentPlanningApi.Current all-done evaluation). Auto-moves never touch Version (SetStatus semantics today — preserved).
- done semantics: the human door PUT stays the only manual feature-status path; the agent door does NOT get a PUT UpdateFeature mirror in this feature (manual transitions are a human concern; agents get auto-transitions for free). Recorded as a deliberate non-goal.
- Web dropdowns become transition-aware: the 022 board-card Select and the workspace header Select show only allowed targets (+ current) from a shared lib map; on 400 the select rolls back (existing 022 rollback path).
- Board filters: hide-done joins the filter model (PlanningBoardFilters + EMPTY_BOARD_FILTERS + filterBoardItems + board-filter-bar toggle), DEFAULT ON — the URL param writes the deviation (hidedone=0 to show done); parseBoardFilters gains review as a valid status value.
- Filter persistence: sessionStorage under one key (overmind.board.filters) holding the full filter object — restored on mount when the URL carries no explicit filter params, written on every setFilters (tree-session.ts readJson/writeJson precedent); scope is the session across project switches, per the feature body.
</decisions>

<open-questions>
- none (user delegated autonomous decisions 2026-09-20)
</open-questions>

<requirements>
Backend:
- PlanningFeatureStatus.Review (+ nothing else in the enum); five map-site patches listed above.
- PlanningFeatureTransitions (Core): allowed-transition map + IsAllowed(current, target); PlanningFeatureService.UpdateAsync validates via it (typed 400 on violation); importer path untouched.
- PlanningArtifactService: create-branch + unique-retry-branch promote draft/ready → inProgress (only when the feature is in those states); SetStatusAsync promotes to review when the changed task is Done AND all sibling tasks are Done AND at least one task exists AND spec+plan exist (ceremony shape — mirrors the "write the result artifact" evaluation; result artifact presence not required).
- Tests (Core/Infra/Api): transitions allowed/denied matrix; auto-promotions from both triggers (artifact create; last task done) incl. the not-all-done negative and the draft/ready-only guard; importer round-trip of status: review; materializer emits status: review; existing UpdateFeature status tests extended.
Web:
- types.ts + status-tone + board-filters parse gain review; transition map in features/planning/lib (single source mirroring the backend matrix).
- Board-card Select + workspace Select filter options to allowed targets (+current); 022 rollback covers the new 400s.
- Hide-done toggle in board-filter-bar (default ON, reflects in URL as deviation); filter object persisted to sessionStorage (restore-on-mount when URL params absent).
Verification:
- dotnet build/test green; web lint + tsc + build green; live: full lifecycle walk draft → ready → inProgress (auto on spec) → review (auto on last task) → done (manual) on a scratch feature.
</requirements>

<acceptance-ideas>
- Scratch feature: create spec → feature flips to inProgress automatically; finish all tasks → review; UI dropdown offers done (+inProgress rollback); done locks the dropdown (no targets).
- PUT status done from draft → 400 InvalidInput; PUT review→inProgress works (rollback path).
- Feature.md round-trips status: review both directions; README block shows [~] for review.
- Board hides done features by default; toggling off persists across project switches within the session.
- Existing features' statuses untouched by the deploy (draft stays draft).
</acceptance-ideas>
