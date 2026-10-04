---
feature: 091
task: 01
slug: board-card-brief-phase-status
status: done
review: light
created: 2026-10-03
brief: CeremonyPhase + BriefExcerpt derivation in Infrastructure, wired into board payload + queue entries, edge matrix
---

# Task 01 — Backend derivation: CeremonyPhase + BriefExcerpt

<goal>
One server-side derivation feeding both surfaces: phase label machine + brief excerpt, wired into the board payload and the queue projection.
</goal>

<scope>
- src/Overmind.Infrastructure/Planning/CeremonyPhaseService.cs (or a static planner per house idioms): Derive(feature) per the spec table (drafting / writing spec / planning / plan validated / in progress / review / done) from specStatus/planStatus/taskDone/taskTotal/status — no new persisted status; BriefExcerpt(feature) — first non-empty <goal> paragraph, cap ~150 chars with ellipsis, null when no goal/body.
- Wire into the board read payload (PlanningMapper/board path) and planning_task_queue projection (excerpt + phase label fields; 072 payload shape extends).
- Edge matrix unit tests: no goal, no body, validated-but-draft, done card, cap boundary on cyrillic/CJK, spec-validated-no-plan, all seven states.
</scope>

<anti-scope>
No web rendering (task 02), no MCP contract changes beyond the new queue fields (task 03), no persisted status changes.
</anti-scope>

<constraints>
English-only, no comments by default, no auto-commits; dotnet build + full test real numbers.
</constraints>

<acceptance-criteria>
- [x] dotnet build 0/0 + full dotnet test green
- [x] Phase machine: all 7 states pinned by tests
- [x] Excerpt: goal-paragraph derivation, 150-cap on cyrillic/CJK, absent goal → null (no empty string)
- [x] Board payload + queue entries carry both fields
- [x] Queue excerpt is null-safe for bodyless features
</acceptance-criteria>

<refs>
- Spec: d066ec12-2fcb-44cd-9e8a-51491ab205ab (Decisions + Edge cases).
- 072-planning-task-queue (payload shape being extended).
</refs>

<obstacles>
- Derivation order (specific → general) resolves the spec's ambiguity rows: done → review → inProgress-with-tasks-in-flight → drafting (no spec artifact) → writing spec (spec draft) → planning (spec validated, plan missing/draft) → plan validated (spec+plan validated, the 037-era validated-but-draft quirk included — the machine wins over the lifecycle). inProgress with taskTotal == 0 stays plan validated (row 4's "tasks not started" beats row 5).
- Planner is a static class (CeremonyPhasePlanner) — no state, no DI, house-idiom per the task's "static planner" option.
- Brief excerpt uses StringInfo text elements for the 150 cap — surrogate pairs (emoji/CJK astral) never split; the ellipsis adds one more element (151 max). Trailing whitespace lines are trimmed before the paragraph check so a goal of blank lines yields null (never an empty string).
- Cap boundary semantics: exactly 150 text elements → returned verbatim without ellipsis; 151+ → first 150 elements + «…».
</obstacles>

<log>
- 2026-10-03 [agent] — dispatched to dotnet-coder (same session — wave 028/099 patterns), 101 done unblocked 091 per the user's pipeline plan
- 2026-10-03 [agent] — Executed by dotnet-coder. New CeremonyPhasePlanner (static, Infrastructure/Planning): DeriveLabel machine (done/review/in-progress-with-tasks/drafting/writing-spec/planning/plan-validated — derivation order specific-to-general so the validated-but-draft 037 quirk lands on «plan validated») and BriefExcerpt (first non-empty <goal> paragraph via PlanningSections.Find, lines joined, StringInfo 150-text-element cap — surrogate pairs never split — ellipsis on overflow, null on absent goal/body/blank goal). Wired into PlanningContracts (PlanningFeatureBoardItem + PlanningQueueItem gain Phase + BriefExcerpt) and PlanningMapper (both items derive inline from existing artifact statuses + task totals; no signature changes; no persistence). Tests: CeremonyPhasePlannerTests 19 — all 7 states pinned, validated-but-draft edge, inProgress-without-tasks stays plan validated, done override, first-paragraph/multiline-join/absent-goal/null-body/empty-goal, cyrillic 200→150+ellipsis, CJK surrogate boundary (151 text elements, no lone high surrogate). Gates: dotnet build --no-incremental 0 errors / 0 warnings; full suite 1860 passed / 0 failed (Core 274, Infra 588, Api 582, Mcp 435); format clean outside Migrations.
- 2026-10-03 [agent] — task 01 executed to review by dotnet-coder: CeremonyPhasePlanner (static, derivation order specific-to-general covering all 7 spec states + the validated-but-draft 037 quirk) and BriefExcerpt (goal-paragraph, StringInfo 150-text-element cap — surrogate-safe — null on absent goal/body); wired into board item + queue item payloads via PlanningContracts/PlanningMapper. CeremonyPhasePlannerTests 19 pin the edge matrix. Gates: dotnet build --no-incremental 0 errors / 0 warnings; full suite 1860 passed / 0 failed (Core 274, Infra 588, Api 582, Mcp 435); format clean outside Migrations.
- 2026-10-03 [agent] — 2026-10-04 [agent] — reviewed by dotnet-code-reviewer: PASS — 091/01 phase machine matches the spec table row-by-row incl. 037 quirk and inProgress gate, BriefExcerpt grapheme-safe with null-not-empty pinned, payloads wired on both surfaces with zero new queries; build 0/0, full suite 1879/1879 re-verified (Infra 588 incl. 19 planner tests); report audits/review-091-01.md.
- 2026-10-03 [agent] — review PASS (audits/review-091-01.md), 0 blockers 0 nits; AC 5/5; 1879/1879 re-verified. Closing as done — 02/03 unblock for parallel dispatch.
</log>