---
feature: 036
slug: agent-workflow-onboarding
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Spec 036 — Agent workflow onboarding: обучить агента без скиллов флою feature→spec→plan→tasks→result

<goal>
A skill-less agent (any runtime, any MCP client, no crew skills) reading only Overmind's own surfaces can run the full feature ceremony with its gates: feature → spec → human validation → plan → human validation → tasks as execution contracts → work loop with reviews → result → done tap. The teaching ships with Overmind itself — onboard quickstart, current_task stop/empty/held reasons, the session_bootstrap prompt, the create_feature description — as hint text on surfaces that already exist, with zero behavior change in v1.
</goal>

<scope>
Four text surfaces, verified in code:

1. **Onboard quickstart** — `OnboardQuickstart.Text` (`src/Overmind.Host/Api/OnboardQuickstart.cs`, served via `/api/onboard` and relayed verbatim by the MCP `overmind_onboard` tool). The primary payload: the numbered section grows from a bare call order into the gated ceremony — the tool named per step, the human gate named where one exists, tasks taught as execution contracts, the sub-agent feeding rule (a sub-agent receives ONLY its own task artifact via `planning_read_artifact`), and the close-out (result artifact + human done tap).
2. **current_task stop/empty/held reasons** — `src/Overmind.Host/Api/AgentPlanningApi.cs` (stop sites ~487–570, `ResultStateReason`, `SpecPlanWarnings` tags stay as-is). Each non-task state names the next ceremony action with the actual tool: spec/plan STOP → `overmind_notify(kind=approval, featureSlug=…)` and wait for the human's reply (validation is never agent-approvable); no-tasks empty → decompose the validated plan via `planning_upsert_tasks`; result states → write/validate/close per `ResultStateReason`; held → finish reviews and close tasks via `planning_set_status`.
3. **session_bootstrap prompt** — `src/Overmind.Mcp/SessionBootstrapPrompt.cs`: one new shared ceremony const (order + gates in compact form), included in BOTH branches (fallback and project-bound) alongside the existing routing consts.
4. **planning_create_feature description** — `src/Overmind.Mcp/OvermindPlanningTools.cs` (~line 212): the follow-up pointer extends from "write its spec" to the proof-gated spec step. No other descriptions change.
</scope>

<anti-scope>
- No new MCP tools, no `overmind://workflow` resource (the designed relief valve when text outgrows the quickstart — deferred, needs its own feature).
- No teaching-400s, no new refusal/STOP paths, no payload shape changes, no state checks added to tools — v1 is hints on existing text surfaces only. Aggressive refusals (e.g. task-create on a spec-less feature) need behavior decisions of their own and stay follow-up candidates.
- No crew-skill bundling and no dependency on any external skill or file: everything the agent needs ships with Overmind.
- No git/commit discipline in the teaching — that is host-machine convention, not Overmind's contract.
- No web UI changes (the UI serves humans); no lifecycle/status MODEL changes (032 owns the model — this feature teaches it, never changes it).
- Server instructions (`Program.cs`) stay lean pointer text — no ceremony block there.
- No wholesale ceremony duplication into tool descriptions (just-in-time teaching belongs to stop states; one-time orientation belongs to the quickstart).
</anti-scope>

<constraints>
- The workflow text must match ACTUAL tool names and REAL lifecycle/state names: feature draft → ready → inProgress → review → done; artifact statuses per kind (spec/plan/result: draft, validated; task: todo, inProgress, review, done, blocked; result has no done). Enforcement is mechanical: a new anti-drift contract test extracts every tool-like token (`overmind_*`, `planning_*`, `list_projects`, `list_entries`, `read_entry`, `issues_*`) from the quickstart and bootstrap texts and asserts each exists in the live ListTools result — a phantom tool name fails the build.
- Pins are the contract. The 032-pinned quickstart blocks (lifecycle arrow line, review invariant, auto-transitions, demote rule, per-kind status listing, claims block, edge rules, import lines, marker legend) stay byte-identical, or a moved phrase and its pin update in the same change. The per-kind status listing sentence must keep its parseable shape (`ListedStatusNames` splits on `;`/`.`) — the ceremony addition must not inject those characters inside it.
- The established proof-flow phrases are reused verbatim: "overmind_notify(kind=approval, featureSlug)", wait for the human's reply, then `planning_validate`; "every edit voids the reply".
- Mode-honest gate wording: stop-reason text teaches the notify→wait flow (true whether humanGate is on or off); it must not promise behaviors the current mode does not deliver. The existing humanGate note in the quickstart keeps explaining the STOP variant.
- English only, terse; the quickstart ceremony addition stays compact (~10 added lines; no wide tables).
</constraints>

<acceptance-criteria>
- [ ] OnboardQuickstart.Text names the ceremony order with the tool per step: create (`planning_create_feature`), spec (`planning_upsert_artifact` kind=spec), plan (kind=plan), tasks (`planning_upsert_tasks`), work loop (`planning_current_task` + `planning_set_status` with a logNote), result (kind=result) — pinned phrases in a new Mcp.Tests contract test.
- [ ] Both validation gates are taught with the proof flow using the established phrases, and the close-out names the human done tap; a skill-less agent reading ONLY the onboard payload can recite the ceremony order and each step's tool.
- [ ] The anti-drift guard passes: every tool-like token in OnboardQuickstart.Text and the session_bootstrap text exists in the live ListTools result.
- [ ] current_task stop/empty/held reasons name the next ceremony action with the actual tool (spec/plan STOP → notify and wait; no-tasks empty → `planning_upsert_tasks`; result states → write/validate/close; held → finish reviews + `planning_set_status`) — pinned in Overmind.Api.Tests; the moved "All tasks are done — write the result artifact." pin updates in the same change.
- [ ] session_bootstrap carries the shared ceremony const in BOTH branches, pinned per branch.
- [ ] planning_create_feature description points at the proof-gated spec step, pinned.
- [ ] Full suite green: `dotnet build Overmind.sln` + `dotnet test Overmind.sln`.
</acceptance-criteria>

<refs>
- 032-status-model-self-documentation (done) — the status-model text this feature builds next to; shares the OnboardQuickstart surface and pin file. Independent, no blocker.
- 023-feature-lifecycle-flow / task 08 — precedent for teaching through agent-door error text.
- 052-agent-briefing-tools (draft) — adjacent start-briefing idea; no coupling, but the ceremony const should stay extractable for it.
</refs>

<obstacles>
- none yet. Known deploy-time note: the MCP adapter picks up new text on restart/deploy (032 precedent) — the live-payload eyeball is post-deploy, contract tests carry the gate until then.
</obstacles>

<log>
- 2026-10-06 [agent] — spec drafted from the feature body + code walk; surfaces verified in OnboardQuickstart.cs, AgentPlanningApi.cs, SessionBootstrapPrompt.cs, Program.cs, OvermindPlanningTools.cs and the pin tests (StatusModelContractTests, PromptContractTests, AgentPlanningApiTests).
- 2026-10-06 [agent] — validated with human proof: notification cfbf8c03-b22a-41ca-a513-b9df41ecd0a4 replied 2026-10-06 13:05:11 UTC
</log>