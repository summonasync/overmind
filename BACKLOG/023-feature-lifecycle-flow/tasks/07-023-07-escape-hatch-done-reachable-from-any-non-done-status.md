---
feature: 023
task: 07
slug: feature-lifecycle-flow
status: done
review: light
created: 2026-09-20
brief: Escape hatch: done reachable from any non-done status (user decision 2026-09-20); Core matrix + web mirror + inverted negatives + spec amendment.
---

# 023/07 — Escape hatch: done reachable from any non-done status

<goal>
Feature status done becomes reachable from draft/ready/inProgress/review via the human door (UI dropdown + PUT) — bury a feature in one click. Auto-transitions still never set done; done remains terminal.
</goal>

<context>
User decision 2026-09-20 (escape-hatch confirmed after the second "done не ставится" occurrence; burying a draft feature like 007 required a 4-step walk draft→ready→inProgress→review→done). Supersedes the 023 spec matrix rule "done reachable ONLY from review" — recorded as a project decision entry; the spec amendment rides this task. Queue slot per user: AFTER 025/02 web+review, BEFORE 025/02 live e2e.
</context>

<scope>
- Core PlanningFeatureTransitions: draft→{ready,inProgress,done}, ready→{inProgress,draft,done}, inProgress→{review,ready,draft,done}, review→{done,inProgress}, done→{} — done joins every non-done row, listed last (display order).
- Web feature-transitions.ts FEATURE_TRANSITIONS mirrors 1:1; both selects (board card + workspace header) gain done for every live feature via the shared map. If the done-lock is hardcoded per component (review-023-03 INFO noted it in two places), derive it from empty allowedTransitions instead — the map stays the only target list.
- Tests: transitions-matrix tests on both sides updated — old negatives (draft→done → 400) invert to positives; the denied set shrinks to done→anything; API 400-evidence tests updated to a still-illegal move (done→inProgress).
- Spec artifact amendment: 023 spec matrix paragraph updated via planning_upsert_artifact with logNote referencing the 2026-09-20 decision entry; the review-only rule is tombstoned, not silently deleted.
- Verify: dotnet build/test green; web lint/tsc/build green; live check on a scratch feature: draft→done in one step via API, board hides it afterwards (hide-done default ON).
</scope>

<anti-scope>
- No new status member (no "abandoned"), no board filter changes, no auto-transition changes (023/06 stays a separate task), no importer/materializer changes (done round-trips already).
</anti-scope>

<constraints>
- done stays terminal (done→{} unchanged); auto-promotions never set done (unchanged).
- Transition map remains the single source of targets per side; no duplicate target lists in components.
- .NET 10 / C# 14; no new packages; no comments; English-only; TypeScript strict on web.
</constraints>

<acceptance-criteria>
- [ ] Core matrix: done allowed from draft/ready/inProgress/review; denied from done; Core/Infra/Api transition tests green with inverted expectations.
- [ ] Web FEATURE_TRANSITIONS mirrors the Core matrix exactly; both selects offer done on every live feature; done features stay locked.
- [ ] No test anywhere pins the old matrix (draft→done 400 gone).
- [ ] Spec artifact amended with logNote; decision entry on the board records the supersession.
- [ ] dotnet test green; web lint/tsc/build green; live one-step draft→done verified.
</acceptance-criteria>

<refs>
- Spec: 023 feature-lifecycle-flow (matrix paragraph — superseded by this task); project decision entry "escape-hatch done-from-any-status" 2026-09-20.
- Code: src/Overmind.Core/PlanningFeatureTransitions.cs:10-15; web/src/features/planning/lib/feature-transitions.ts:3-9; feature-board.tsx + feature-workspace.tsx selects; transitions tests Core/Infra/Api + web.
- Reviews: audits/review-023-03.md (matrix-parity diff method), review-023-01/02.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-20 — Task carved after user confirmed the escape-hatch shape (done from any status) and the queue slot (after 025/02 web+review, before its live e2e).
- 2026-09-20 — Done: Core matrix + web mirror landed (review PASS-WITH-NITS, audits/review-023-07.md — parity exact, additive, auto-transitions untouched, 919 tests green; web lint/tsc/build green). Spec amended (v2, validated) with tombstone of the review-only rule. Done-lock derived from map (closed review-023-03 INFO-2). Live one-step draft→done check rides the 025/02 e2e host session (new build), noted as INFO by reviewer.
</log>
