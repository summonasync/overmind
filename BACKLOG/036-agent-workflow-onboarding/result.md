---
feature: 036
slug: agent-workflow-onboarding
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Result 036 — Agent workflow onboarding: обучить агента без скиллов флою feature→spec→plan→tasks→result

## What shipped

A skill-less agent is now taught the feature ceremony on 4 existing text surfaces — metadata/advisory text only, zero behavior change:

1. **Onboard quickstart** (`src/Overmind.Host/Api/OnboardQuickstart.cs`) — step 4 is now the gate ceremony (spec → validation with the proof flow → plan → same gate → `planning_upsert_tasks` execution contracts with the sub-agent-gets-only-its-task rule → work loop: `planning_current_task`, implement, verify, `planning_set_status` with a logNote, review before a task closes); step 5 is the close-out (`planning_upsert_artifact(kind=result)`, human done tap). A tool is named for every step; the 032 blocks below stay byte-identical.
2. **current_task reasons** (`src/Overmind.Host/Api/AgentPlanningApi.cs`) — stop/empty/held/result states each name the next ceremony step with the real tool; the spec/plan STOP texts interpolate the feature's real slug into the `overmind_notify(kind=approval, featureSlug=…)` instruction.
3. **session_bootstrap** (`src/Overmind.Mcp/SessionBootstrapPrompt.cs`) — a shared `CeremonyRouting` const added to BOTH branches (fallback and project-bound), reusing the canonical proof-flow phrases.
4. **planning_create_feature description** (`src/Overmind.Mcp/OvermindPlanningTools.cs`) — the follow-up pointer now names the proof-gated spec step.

## Safety net

The anti-drift contract guard (`WorkflowTeachingContractTests`): every tool-like token in the taught text is checked against the live tool list — a phantom tool name fails the build. The guard follows the text into both bootstrap branches, so the quickstart and the prompt cannot drift from the real toolset (or from each other).

## Verification

- Tasks 01–03: light review, all APPROVED (spec-compliance + code quality).
- Task 04 full gate: green — 2185 passed / 44 expected-red (the 049-lane excluded legitimately) of 2229 total, with the reviewer's count correction applied.
- Pin integrity: 032's pinned quickstart blocks and the per-kind status listing shape untouched; the moved current_task reason pin updated in the same change as the text (pin = contract).

## Commits

`ad50c68` (quickstart + teaching pins), `5ba84f9` (current_task reasons), `5c336b3` (adapter texts), + the gate run.

## For the human

The live-walk verification — an eyeball of the real `/api/onboard` payload carrying the taught texts — ships with the NEXT adapter deploy (032 precedent: adapter text picks up at deploy/restart). Contract tests carry the gate until then; afterwards that eyeball closes task 04's AC3 (tracked as pending-deploy in the task's obstacles).

<log>
- 2026-10-06 [agent] — result drafted from the completed tasks (01 ad50c68, 02 5ba84f9, 03 5c336b3 — light APPROVED; 04 gate green 2185/44/2229) and the coordinator's close-out notes; feature in review.
</log>