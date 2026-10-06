---
feature: 036
task: 03
slug: agent-workflow-onboarding
status: todo
review: light
created: 2026-10-06
brief: Adapter texts: shared CeremonyRouting const in SessionBootstrapPrompt.cs (both branches, reuses the pinned proof-flow phrases); planning_create_feature description extended to the proof-gated spec step; pins in PromptAndFailureModeTests (per branch) + PlanningToolContractTests. No other descriptions change.
---

# Task 036/03 — session_bootstrap ceremony const + create_feature description

<goal>
The MCP session_bootstrap prompt carries the ceremony order + gate discipline at session start (both branches), and planning_create_feature's description points at the proof-gated spec step — so an agent that never reads the full quickstart still meets the gates at the moment of first contact.
</goal>

<scope>
- src/Overmind.Mcp/SessionBootstrapPrompt.cs — one new shared const (e.g. CeremonyRouting): one sentence with the order (spec → validate with proof → plan → validate → planning_upsert_tasks execution contracts → work loop with planning_current_task/planning_set_status → result → done tap) reusing "overmind_notify(kind=approval, featureSlug)" and "planning_validate" verbatim; concatenated into FallbackText AND the project-bound string after the existing routing consts.
- src/Overmind.Mcp/OvermindPlanningTools.cs — planning_create_feature Description ONLY (~212): the follow-up pointer becomes the proof-gated spec step ("Follow with planning_upsert_artifact(kind=spec), then validate via planning_validate — send overmind_notify(kind=approval, featureSlug) and wait for the human's reply first.").
- tests/Overmind.Mcp.Tests/PromptAndFailureModeTests.cs — ceremony-line pins for BOTH branches (fallback + single-project). tests/Overmind.Mcp.Tests/PlanningToolContractTests.cs — one pin for the create_feature description pointer.
</scope>

<anti-scope>
- No other tool descriptions change; no server-instructions (Program.cs) changes; no new tools/resources/refusals; response shapes untouched (CompactFeature output stays as-is).
</anti-scope>

<constraints>
- The const reuses the established phrasing of the existing routing consts (ValidationProofRouting already carries the notify→reply→validate flow — do not contradict it, complement it with the ORDER).
- Tool names exact — the task 036/01 anti-drift guard scans this text; a phantom name fails the build.
- MachineSurfaces_NeverSayFrozen stays green (no "frozen" wording).
</constraints>

<acceptance-criteria>
- [ ] Both bootstrap branches carry the ceremony const; PromptAndFailureModeTests pins each branch and passes.
- [ ] planning_create_feature description carries the proof-gated spec pointer; PlanningToolContractTests pin passes.
- [ ] Existing prompt/handshake pins stay green (SessionBootstrap tests, HandshakeContractTests, MachineSurfaces_NeverSayFrozen).
- [ ] dotnet build Overmind.sln green; dotnet test Overmind.sln --filter "PromptContractTests|PlanningToolContractTests|HandshakeContractTests" green.
</acceptance-criteria>

<refs>
- Spec: 036/S (a8d38f0d-37c9-4513-948e-30a227c1a566, validated). Plan: 036/P (66a76b70-ca36-46a8-ada4-6527042ad4ab, validated) — slice 3.
- 052-agent-briefing-tools (draft) — keep the const extractable for a future start-briefing tool.
</refs>

<obstacles>
- none
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan 66a76b70 (slice 3).
</log>