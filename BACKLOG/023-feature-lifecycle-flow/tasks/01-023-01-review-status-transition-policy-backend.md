---
feature: 023
task: 01
slug: feature-lifecycle-flow
status: todo
review: light
created: 2026-09-20
---

# 023/01 — Review status + transition policy (backend)

<goal>
PlanningFeatureStatus gains Review (value 4); all five exhaustive map sites are patched; a Core transition policy (PlanningFeatureTransitions) defines the legal lifecycle and PlanningFeatureService.UpdateAsync enforces it with a typed 400. The importer bypasses transition checks (disk is truth).
</goal>

<context>
Feature 023, backend half 1. Exploration-verified break sites: PlanningMaterializer.FeatureStatus (505–512, THROWS on unknown), BacklogImporter.MapFeatureStatus (397–415, silently downgrades unknown to draft), ManagedBlock.StatusMarker (105–111, disc → "[ ]"), web types.ts union + PLANNING_FEATURE_STATUSES, web status-tone Record (compile error). Web sites are task 03's — this task only adds the enum member so the web keeps compiling against the OLD deployed API shape (the web union is a client concern; adding the enum member now is safe because JSON strings flow generically).
</context>

<scope>
- src/Overmind.Core/Enums/PlanningFeatureStatus.cs — Review = 4.
- PlanningMaterializer.FeatureStatus — emit "review".
- BacklogImporter.MapFeatureStatus — accept "review".
- ManagedBlock.StatusMarker — review → "[~]".
- New src/Overmind.Core/.../PlanningFeatureTransitions (placement per sibling policy types): allowed map draft→{ready,inProgress}, ready→{inProgress,draft}, inProgress→{review,ready,draft}, review→{done,inProgress}, done→{} + IsAllowed(current, target).
- PlanningFeatureService.UpdateAsync (59–87): when status.HasValue && != current → guard via transitions; violation → PlanningFailureKind.InvalidInput (new failure code InvalidFeatureTransition or reuse InvalidInput with a clear message — follow the BriefNotAllowedForKind precedent) → 400 both doors (same handler). ImportAsync path NOT guarded.
- Tests: transitions matrix (allowed set + denied 400s) via API PUT; materializer/importer round-trip status: review; existing UpdateFeature status tests stay green (draft→ready allowed; check UpdateFeature_LeavingDraft_BumpsVersion still passes — leaving draft is legal).
</scope>

<anti-scope>
- No auto-promotions (task 02), no web changes (task 03), no agent-door PUT mirror.
- No changes to artifact statuses (PlanningArtifactStatus already has its own review).
</anti-scope>

<constraints>
- .NET 10 / C# 14; no new packages; no comments; English-only.
- dotnet build + test green (baseline ~802 + 012 additions; record counts).
</constraints>

<acceptance-criteria>
- [ ] PUT status review from inProgress → 200; from draft → 400 typed.
- [ ] PUT done from review → 200; from any other state → 400.
- [ ] Feature.md with status: review round-trips (materialize → import) without downgrade; README marker [~].
- [ ] dotnet test green incl. matrix tests.
</acceptance-criteria>

<refs>
- Spec: feature 023, spec artifact (validated 2026-09-20) — transition matrix + map-site list.
- Pattern refs: PlanningFeatureStatus.cs, PlanningMaterializer.cs:505–512, BacklogImporter.cs:397–415, ManagedBlock.cs:105–111, PlanningFeatureService.UpdateAsync:59–87, PlanningErrors/PlanningFailureKind (typed 400 precedent: BriefNotAllowedForKind from 021).
</refs>

<obstacles>
</obstacles>

<log>
</log>
