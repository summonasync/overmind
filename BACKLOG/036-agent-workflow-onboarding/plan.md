---
feature: 036
slug: agent-workflow-onboarding
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Plan 036 — Agent workflow onboarding: обучить агента без скиллов флою feature→spec→plan→tasks→result

## Approach

One change-class only: string constants on four existing surfaces, plus the pins that hold them. Zero behavior, zero payload, zero new surfaces. The ceremony text is written once as the reference order — feature → spec → human gate → plan → human gate → tasks (execution contracts; sub-agent gets only its own task artifact) → work loop with reviews → result → human done tap — and each surface quotes the slice its moment needs: the quickstart carries the whole order (orientation), stop/empty/held reasons carry just the next action (just-in-time), the bootstrap prompt carries a one-line const (session start), the create_feature description carries one follow-up pointer.

Gate wording rule: any sentence about validation says notify → wait for the human's reply → `planning_validate` (true in both humanGate modes); no sentence promises a STOP the current mode does not deliver.

## Files

| File | Change |
|------|--------|
| `src/Overmind.Host/Api/OnboardQuickstart.cs` | Numbered section rework: steps 1–3 unchanged (onboard; board/queue+claim; current_task+STOP); step 4 becomes the gated ceremony (spec → validate with proof flow → plan → validate → `planning_upsert_tasks` execution contracts + sub-agent-gets-only-its-task rule → work loop: `planning_current_task`, implement, verify, `planning_set_status` with a logNote, review before a task closes); step 5 becomes close-out (`planning_upsert_artifact(kind=result)`, human done tap). The 032 blocks below the numbered list stay byte-identical. ~10 added lines, no tables. |
| `src/Overmind.Host/Api/AgentPlanningApi.cs` | Reason strings only: spec STOP (~498) and plan STOP (~505) gain "send overmind_notify(kind=approval, featureSlug='<slug>') and wait for the human's reply"; no-tasks empty (~518) gains "decompose the validated plan into task artifacts via planning_upsert_tasks — each task an execution contract (goal, scope, anti-scope, acceptance-criteria, refs)"; `ResultStateReason` (~596) lines gain the tool per state (write via `planning_upsert_artifact(kind=result)` / validate via the proof flow / close needs the human's done tap); held (~567) gains "finish reviews, close tasks with planning_set_status". `SpecPlanWarnings` tags untouched. |
| `src/Overmind.Mcp/SessionBootstrapPrompt.cs` | New shared const (e.g. `CeremonyRouting`): one sentence with the order + gates, reusing "overmind_notify(kind=approval, featureSlug)" and "planning_validate"; concatenated into BOTH `FallbackText` and the project-bound string, after the existing routing consts. |
| `src/Overmind.Mcp/OvermindPlanningTools.cs` | `planning_create_feature` Description only (~212): "Follow with planning_upsert_artifact to write its spec" → "Follow with planning_upsert_artifact(kind=spec), then validate via planning_validate — send overmind_notify(kind=approval, featureSlug) and wait for the human's reply first." |
| `tests/Overmind.Mcp.Tests/WorkflowTeachingContractTests.cs` | New file: (a) quickstart ceremony pins — one ShouldContain per stage+tool mapping and per gate phrase; (b) the anti-drift guard — regex-extract tool-like tokens (`(overmind\|planning)_[a-z_]+`, plus `list_projects`, `list_entries`, `read_entry`, `issues_[a-z_]+`) from `OnboardQuickstart.Text` AND the bootstrap prompt text (both branches), assert each is in the live `ListToolsAsync` name set; (c) asserts 032's pinned sentences are still present (regression mirror of StatusModelContractTests' key pins, so a ceremony edit cannot silently break them). |
| `tests/Overmind.Mcp.Tests/PromptAndFailureModeTests.cs` | PromptContractTests: ceremony-line pins for both branches (fallback + single-project). |
| `tests/Overmind.Api.Tests/AgentPlanningApiTests.cs` | New pins for the extended stop/empty/held/result reasons; update the existing pin at ~743 ("All tasks are done — write the result artifact.") in the same change. |
| `tests/Overmind.Mcp.Tests/PlanningToolContractTests.cs` | One pin: create_feature description carries the proof-gated spec pointer. |

`Canned.cs` is NOT touched — the adapter stub quickstart is pass-through text, independent of the host string; its rendering tests keep their canned values.

## Pin strategy

- Pins are the contract: a pinned phrase the new text moves is updated in the same commit as the text — the suite never goes red across a change. Known-moved pins: `AgentPlanningApiTests` ~743 (result reason). Known-intact pins: every 032 phrase in `StatusModelContractTests` (lifecycle line, review invariant, per-kind listing sentence, claims block, edge rules, import lines, marker legend) — the ceremony addition goes into the numbered section only.
- The per-kind status listing sentence must keep its parseable shape: `ListedStatusNames` cuts at the first `;`/`.` — no such characters may appear inside that sentence.
- The anti-drift guard makes tool names self-healing: any future rename that outlives the teaching text fails `dotnet test` with the offending token named.
- Proof-flow phrases reuse the already-pinned canonical forms (`PromptContractTests`: "overmind_notify(kind=approval, featureSlug)", "the human's bell reply → planning_validate") so the two surfaces cannot drift apart.

## Suggested task slice (post-validation breakdown)

1. **Quickstart ceremony** (`OnboardQuickstart.cs`) + `WorkflowTeachingContractTests.cs` (ceremony pins, anti-drift guard, 032-regression mirror). Verify: `dotnet build Overmind.sln`; `dotnet test Overmind.sln --filter WorkflowTeachingContractTests` (plus StatusModelContractTests — shares the surface).
2. **current_task reasons** (`AgentPlanningApi.cs`) + `AgentPlanningApiTests` pins (new + the moved ~743 pin). Verify: build; `dotnet test Overmind.sln --filter AgentPlanningApiTests`.
3. **Adapter surfaces** (`SessionBootstrapPrompt.cs` const + `create_feature` description) + `PromptAndFailureModeTests` / `PlanningToolContractTests` pins. Verify: build; `dotnet test Overmind.sln --filter "PromptContractTests|PlanningToolContractTests"`.
4. **Full gate + live walk**: `dotnet build Overmind.sln` then full `dotnet test Overmind.sln`; run the host and eyeball the real `/api/onboard` payload (ceremony block present, 032 blocks intact). Adapter-restart pickup is deploy-time (spec obstacle note) — contract tests carry the gate until then.

Slices 1–3 are text-disjoint (host-quickstart / host-api / adapter) and could parallelize, but they share the ceremony wording — sequential in one coder is safer for phrasing consistency; slice 4 closes.

## Verification gates

- `dotnet build Overmind.sln` after every slice.
- Full `dotnet test Overmind.sln` on slice 4 — green is the done bar (known cold-CLR flake per AGENTS.md: `dotnet build-server shutdown`, retry).
- No frontend, no npm gates (backend-only change).

## Risks / notes

- Quickstart length: the ceremony block is the one place text may bloat — the ~10-line budget is a hard review criterion; `overmind://workflow` is the designed relief valve and stays anti-scope.
- Phrasing drift across the four surfaces is the main quality risk — the plan orders slices sequentially in one coder for that reason, and the shared-phrase pins catch the rest.
- The stop-reason strings are user-visible tool output; keep every addition one sentence, tool-name-exact, no prose padding.
- Status honesty: the ceremony text must not imply the agent can flip features through the lifecycle freely — agent-door targets remain done/inProgress only (existing rule, already documented; do not re-teach it differently).

<log>
- 2026-10-06 [agent] — plan drafted; file-level approach verified against OnboardQuickstart.cs, AgentPlanningApi.cs (stop sites 487–570, ResultStateReason), SessionBootstrapPrompt.cs, OvermindPlanningTools.cs and the pin files (StatusModelContractTests, PromptAndFailureModeTests, AgentPlanningApiTests ~743, Canned.cs pass-through).
- 2026-10-06 [agent] — validated with human proof: notification cfbf8c03-b22a-41ca-a513-b9df41ecd0a4 replied 2026-10-06 13:05:11 UTC
</log>