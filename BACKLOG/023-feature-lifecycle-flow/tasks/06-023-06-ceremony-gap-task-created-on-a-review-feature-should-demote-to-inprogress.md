---
feature: 023
task: 06
slug: feature-lifecycle-flow
status: done
review: light
created: 2026-09-20
---

# 023/06 — Ceremony gap: task created on a review feature should demote to inProgress

<goal>
When a task artifact is created on a feature in `review`, the feature should auto-demote to `inProgress` (the mirror of the last-task-done promotion). Today the feature silently stays in `review` with open work.
</goal>

<context>
Bug found live 2026-09-20 during the 024/025 session: feature 025 reached `review` (task 01 done, spec+plan present — correct per the rules at that moment), then task 02 was carved 44 seconds LATER; the feature stayed in `review` with a todo task. The promotion rule is blind to tasks that appear after the flip. The spec's manual-rollback stance covers the human path, but the orchestrator/board flow creates tasks in waves — the gap hits every multi-wave feature.
</context>

<scope>
- PlanningArtifactService.PromoteOnArtifactCreate (or its caller): when feature.Status == Review and a TASK artifact is created → feature.SetStatus(InProgress) in the same SaveChanges. Non-task artifact creates on review leave the status alone (docs edits during review are expected).
- Test: feature in review (walked legitimately) + create a task → status flips to inProgress; create a spec-edit (update) on review → unchanged; done features still untouched (no resurrection).
- Log the demotion via the materializer sync as usual.
</scope>

<anti-scope>
- No manual-transition matrix changes (review→inProgress stays legal manually), no UI work, no demotion on artifact DELETE.
</anti-scope>

<constraints>
- .NET 10 / C# 14; no comments; English-only; dotnet build + test green (record counts).
</constraints>

<acceptance-criteria>
- [ ] Task create on a review feature → inProgress (pinned by test).
- [ ] Non-task artifact create on review → unchanged (pinned).
- [ ] Done features never resurrect (pinned).
</acceptance-criteria>

<refs>
- Spec 023: auto-transition decisions (event-driven; rollback was manual-only — this generalizes the rollback to the same event family).
- Pattern refs: PromoteOnArtifactCreate (PlanningArtifactService.cs:207-213), TryPromoteToReviewAsync guard shape.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Done (re-recorded — an orchestrator id mix-up flipped the WRONG artifact on the first attempt): TransitionOnArtifactCreate — Review→inProgress demote on Task create only (non-task creates on review untouched, done never resurrects), both create branches + same SaveChanges; 3 tests, 922/922. Review PASS (audits/review-023-06.md, 3 INFO). Proven live in the 023/05 walk: task 3 created on a review feature → instant demote. INFO-1 follow-up candidate (reopen-demotion) noted in the feature result.
</log>
