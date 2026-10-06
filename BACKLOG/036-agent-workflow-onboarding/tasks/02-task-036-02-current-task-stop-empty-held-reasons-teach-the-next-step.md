---
feature: 036
task: 02
slug: agent-workflow-onboarding
status: todo
review: light
created: 2026-10-06
brief: current_task teaching: extend stop/empty/held reasons in AgentPlanningApi.cs (~498/505/518/567, ResultStateReason ~596) to name the next ceremony action with the real tool (STOP → overmind_notify approval + wait; empty → planning_upsert_tasks; result → write/validate/close; held → reviews + planning_set_status); AgentPlanningApiTests pins incl. the moved ~743 pin in the same change.
---

# Task 036/02 — current_task stop/empty/held reasons teach the next step

<goal>
Every non-task state of planning_current_task names the next ceremony action with the actual tool, so a stuck agent knows what to do without external skills: spec/plan STOP → send overmind_notify(kind=approval, featureSlug=…) and wait for the human's reply (validation is never agent-approvable); no-tasks empty → decompose the validated plan via planning_upsert_tasks — each task an execution contract (goal, scope, anti-scope, acceptance-criteria, refs); result states → write via planning_upsert_artifact(kind=result) / validate via the proof flow / close needs the human's done tap; held → finish reviews, close tasks with planning_set_status.
</goal>

<scope>
- src/Overmind.Host/Api/AgentPlanningApi.cs — reason strings only: spec STOP (~498), plan STOP (~505), no-tasks empty (~518), held (~567), ResultStateReason (~596). One sentence per addition, tool-name-exact. SpecPlanWarnings tags and all state logic untouched.
- tests/Overmind.Api.Tests/AgentPlanningApiTests.cs — new pins for every extended reason; the existing pin at ~743 ("All tasks are done — write the result artifact.") updates in the same change (pin = contract, never red across a change).
</scope>

<anti-scope>
- No state-machine, gating, or payload changes; no new stop conditions; SpecPlanWarnings tags unchanged; Canned.cs (adapter stub) untouched — pass-through text is independent of host strings.
</anti-scope>

<constraints>
- Mode-honest: STOP text teaches notify → wait (true in both humanGate modes); it must not promise behaviors the current mode does not deliver. Existing STOP semantics ("Reads stay open; task issuance stops...") preserved.
- Reuse the canonical proof-flow phrases ("overmind_notify(kind=approval, featureSlug)").
- Keep every addition one sentence — these strings are user-visible tool output; no prose padding.
</constraints>

<acceptance-criteria>
- [ ] Each of the five state sites names its next ceremony action with the real tool name.
- [ ] AgentPlanningApiTests pins every extended reason; the moved ~743 pin is updated in the same change and green.
- [ ] No state logic touched — current_task behavior (states, warnings, blocking) unchanged; full AgentPlanningApiTests green.
- [ ] dotnet build Overmind.sln green; dotnet test Overmind.sln --filter AgentPlanningApiTests green.
</acceptance-criteria>

<refs>
- Spec: 036/S (a8d38f0d-37c9-4513-948e-30a227c1a566, validated). Plan: 036/P (66a76b70-ca36-46a8-ada4-6527042ad4ab, validated) — slice 2.
- 023-feature-lifecycle-flow / task 08 — precedent for teaching through agent-facing text.
</refs>

<obstacles>
- none
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan 66a76b70 (slice 2).
</log>