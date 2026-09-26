---
feature: 068
task: 03
slug: mutation-transparency
status: done
review: hard
created: 2026-09-26
brief: Feature demotes review→inProgress when its spec/plan drops below validated (D1); quickstart states the review invariant
---

# 068/03 — Feature demote on spec/plan invalidation

<goal>
User decision D1 (2026-09-26): a review feature whose spec/plan stops being validated flips to inProgress in the same SaveChanges (spec R3 / OM-12b). Mirror of 023/06 (task-create demote). Invariant to document: review ⟺ spec+plan validated AND all tasks done.
</goal>

<scope>
- In the artifact-demotion branch (PlanningArtifactService.UpdateAsync): kind ∈ {spec, plan} demoted + feature.Status == Review → feature.SetStatus(InProgress) + [auto] line on the feature log ("feature demoted: review → inProgress (spec no longer validated)").
- Done features never resurrect; non-review features untouched.
- Artifact response surfaces the feature consequence (feature status field or statusChanged note — follow the echo idiom from task 02).
- Quickstart/onboard text states the invariant and the demote rule.
</scope>

<anti-scope>
- No manual-transition matrix changes (review→inProgress already legal); no artifact-demotion semantics change; no UI work.
</anti-scope>

<constraints>
- .NET 10 / C# 14; no comments; English-only; no git commit/push; dotnet build + dotnet test green.
</constraints>

<acceptance-criteria>
- [x] Walked-to-review feature + spec demote → feature inProgress, [auto] line present (test pin, legitimate walk like review-023-06)
- [x] Same for plan demote
- [x] Done feature + spec demote → feature stays done; inProgress feature untouched
- [x] Response surfaces the feature status change
- [x] Quickstart carries the invariant line
- [x] dotnet build + test green
</acceptance-criteria>

<refs>
- Spec 9f1fa31e R3/D1; 023/06 pattern (TransitionOnArtifactCreate); task 02 echo idiom.
</refs>

<obstacles>
- Result-kind demotion does NOT pull a review feature to inProgress — D1/scope names kind ∈ {spec, plan} only; a demoted result artifact on a review feature leaves the feature status untouched (flag back to the user if result should count too).
- Concurrent-door case (per coordinator's ask): the guard reads feature.Status from the tracked instance at mutation time; a concurrent door flipping the same feature between load and SaveChanges resolves last-write-wins on the Status column (no concurrency token) — both log lines still land, no exception. Recorded here per instructions, not "fixed".
- Import path (PlanningFeatureService re-import UpdateBody) also demotes artifacts but does NOT invoke the feature demote — import restores statuses verbatim from files and owns its own semantics (043); routing it through this rule would contradict verbatim restore.
- The scope names PlanningArtifactService.UpdateAsync; in-tree the artifact update/write path is UpsertAsync (+ PatchAsync) — the demote hook rides both mutation branches, which is the same demotion branch the scope means.
</obstacles>

<log />

<log>
- 2026-09-26 [agent] — execution started: feature demote review→inProgress on spec/plan invalidation (D1), same-SaveChanges + [auto] line, response echo, quickstart invariant
- 2026-09-26 — 068/03 implemented. Core: PlanningArtifact gained [NotMapped] FeatureStatusChanged + const FeatureDemotedStatusEcho ("review → inProgress") + RecordFeatureDemotion(); ApplyBodyMutation resets it with each body mutation. Infrastructure: PlanningArtifactService.DemoteReviewFeatureOnDocumentInvalidation — called after UpdateBody (UpsertAsync) and after PatchSection/TickAc (PatchAsync); fires when StatusChanged != null AND kind ∈ {spec, plan} AND feature.Status == Review → feature.SetStatus(InProgress, "feature demoted: review → inProgress ({kind} no longer validated)", Auto) + artifact.RecordFeatureDemotion(); same SaveChanges (mirrors TransitionOnArtifactCreate); done (terminal) and non-review features untouched by the guard. Response: PlanningArtifactResponse + PlanningArtifactPatchResponse carry optional featureStatusChanged (JsonIgnore WhenWritingNull); MCP CompactArtifact echoes it, planning_patch_artifact text appends ", featureStatusChanged 'review → inProgress'". Quickstart: "Review invariant: review ⟺ spec+plan validated AND all tasks done." + demote rule line (done never resurrected). Pins (6 new Infra tests with legitimate task-completion walk to review — review-023-06 style): spec demote via patch → feature inProgress + [auto] feature log line + echo; plan demote (plan wording); done feature + spec demote → stays done, no feature log growth; inProgress feature untouched (no log growth); upsert-path demote; non-demoting task patch on review → feature stays review. Api: full agent-door walk (spec+plan+2 tasks done → review, human-door freeze, patch) → patch response featureStatusChanged "review → inProgress" + feature GET status inProgress + [auto] line in feature body. Mcp: canned demoted fixture extended with featureStatusChanged, echo + patch-text pins. Quickstart invariant pinned in StatusModelContractTests.Quickstart_StatesTheReviewInvariantAndDemoteRule. Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1466 passed / 0 failed (Core 215, Infrastructure 479, Api 423, Mcp 349); format clean on all touched files.
- 2026-09-26 [agent] — 068/03 done: all 6 AC ticked. Review feature whose spec/plan is demoted by patch/upsert flips to inProgress in the same SaveChanges with the [auto] line "feature demoted: review → inProgress ({kind} no longer validated)"; done (terminal) and non-review features untouched; artifact responses + MCP echoes carry featureStatusChanged "review → inProgress"; quickstart states the review invariant and the demote rule. Hard-review diligence: 6 legitimate-walk Infra pins (spec/plan/done/inProgress/upsert-path/non-demoting), full agent-door walk Api pin, canned echo pins, quickstart pin. Edge cases recorded in <obstacles> (result-kind excluded per scope; concurrent-door last-write-wins; import path keeps verbatim restore). Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1466 passed / 0 failed (Core 215, Infrastructure 479, Api 423, Mcp 349); format clean on touched files. Review label: hard.
- 2026-09-26 — Fix-round for hard-review APPROVED-WITH-WARNINGS (audits/review-068-03-hard.md). FIX A — TryPromoteToReviewAsync now requires spec AND plan VALIDATED (existence no longer sufficient); [auto] promotion line reworded to "all tasks done + spec + plan validated → review"; quickstart auto-transitions line updated to the enforced truth; new pin SetStatusAsync_LastTaskDone_DraftSpecAndPlan_NoPromotion (all tasks done + draft spec/plan → stays inProgress, no [auto] review line). FIX B — SetStatusAsync: kind ∈ {spec, plan} transitioning away from Validated (wasValidated && status != Validated) on a review feature runs DemoteReviewFeatureOnDocumentInvalidation (feature review→inProgress, same SaveChanges, [auto] feature log line); return type changed to new PlanningArtifactStatusResult(Warnings, FeatureStatusChanged) — the echo is wired explicitly since artifact.SetStatus never sets StatusChanged; SetPlanningArtifactStatusResponse carries optional featureStatusChanged (both doors). Helper refactor: DemoteReviewFeatureOnDocumentInvalidation no longer gates on StatusChanged — Upsert/Patch callers guard with `StatusChanged is not null`, SetStatusAsync with `wasValidated && status != Validated`. Test expectations changed honestly (walk reworked to validated spec+plan before the all-done promotion): SetStatusAsync_LastTaskDone_WithSpecAndPlan_PromotesToReview (renamed WithValidatedSpecAndPlan), LastTaskDone_WritesAutoReviewLine (new line text), UpsertArtifact_CreateTask_OnReviewFeature_DemotesToInProgress, the fifth-task reopen test (freeze + 3 line-text pins), UpsertArtifact_CreateResult_OnReviewFeature_StaysReview, UpsertTasks_CreateFromReview_SingleReopenLineNotOnePerTask, my four 068/03 walk tests (validate both docs before the last done). New FIX B pins (Infra): manual validated→draft on spec of review feature → feature inProgress + feature log line + result echo; done feature untouched (echo null); inProgress untouched (echo null); validated→validated no-op untouched (echo null). Api: new agent-door pin SetArtifactStatus_ManualValidatedToDraft_OnReviewFeature_PullsFeatureBackToInProgress; my patch-walk test reworked to validated spec+plan. Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1472 passed / 0 failed (Core 215, Infrastructure 484, Api 424, Mcp 349); format clean on touched files.
- 2026-09-26 — Fix-round-2 for re-review addendum (audits/review-068-03-hard.md, RESIDUAL 1): planning_set_status no longer drops featureStatusChanged — the confirmation JsonObject now copies the field from the agent-door payload when present via new CopyFeatureStatusChanged (next to CopyWarnings); absent stays absent (existing no-warnings pin extended to also assert featureStatusChanged absent). New pin PlanningSetStatus_ApiReportsFeatureDemotion_EchoesFeatureStatusChanged (stub returns {"featureStatusChanged":"review → inProgress"} → confirmation carries it). RESIDUAL 2 (promotion re-check from ValidateAsync) left untouched per instruction — microbatched. Evidence: dotnet build 0 warnings / 0 errors; dotnet test 1473 passed / 0 failed (Core 215, Infrastructure 484, Api 424, Mcp 350); format clean on the two touched files.
</log>
