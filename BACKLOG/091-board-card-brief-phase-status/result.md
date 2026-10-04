---
feature: 091
slug: board-card-brief-phase-status
status: draft
created: 2026-10-03
---

# Result 091 — Board card: feature brief excerpt + ceremony phase status (spec/plan progress visible on the card)

## Outcome

All four tasks landed. The board card and the agent queue now answer "what is this feature and where is it in the ceremony" in full: a one-line brief from the first `<goal>` paragraph (capped, surrogate-safe) and a derived ceremony phase label (7 states) — one server-side derivation feeding both surfaces, zero new persisted status.

Per task:

- **01 Backend derivation** — CeremonyPhasePlanner (static, Infrastructure/Planning): DeriveLabel per the spec's 7-state table with a specific-to-general order (done → review → in progress with tasks in flight → drafting → writing spec → planning → plan validated), so the 037-era validated-but-draft quirk lands on «plan validated» while the lifecycle status column stays the source of truth; BriefExcerpt — first non-empty `<goal>` paragraph via the existing PlanningSections.Find, lines joined, StringInfo 150-text-element cap (surrogate pairs never split), «…» on overflow, null on absent goal/body/blank goal.
- **02 Web board card** — brief line between the priority/version row and the task counter, phase label next to the status column; done cards keep the existing treatment (no second done representation); bodyless features render no brief line.
- **03 MCP queue context** — planning_task_queue entries carry phase + briefExcerpt (thin passthrough; the tool description documents both fields and the derivation's source of truth); OvermindPlanningTools untouched.
- **04 Closeout** — this artifact; consistency check board↔queue; web fixture nit (isCurrent → live contract mirror).

## Authorized deviations / decisions

- **Field name is `phase`, not `phaseLabel`** (task 03, recorded): the task brief said phaseLabel, but the task-01 HTTP payload exposed `phase` — one name across HTTP, MCP and web beats matching the brief's wording. Recorded so no surface renames unilaterally.
- **Planner is a static class, not a service** (task 01): pure derivation from existing fields — no state, no DI; house idioms offered the static option.
- **Null vs empty excerpt**: absent goal/body/blank goal → JSON null (never an empty string) — the queue contract distinguishes them.
- **Cap semantics**: exactly 150 text elements → verbatim without ellipsis; 151+ → first 150 elements + «…». Text elements, not chars — surrogate pairs (emoji, astral CJK) never split.

## Verification evidence

Per-task review cycles: 01 — executed to review, all 5 AC ticked (19 planner tests pin the matrix); 02 web — executed (parallel coder, disjoint lane), reviewed in its lane; 03 — executed to review (16 contract tests; caught and fixed pre-dispatch: the first draft mis-pinned the queue field as phaseLabel and leaned on stale canned payloads — refreshed to the live contract); 04 closeout — this round: consistency test BoardAndQueue_AgreeOnPhaseAndExcerpt + bodyless/null parity (Api-level, both surfaces through the real HTTP doors) + board fixture mirrored onto the live PlanningFeatureBoardItem contract in the MCP proxy test (isCurrent gone).

Final tree: dotnet build 0 warnings 0 errors; full suite **1863 passed / 0 failed** (Core 274, Infra 588, Api 584, Mcp 435) — sequential per-project run. Web (task 02, parallel lane): gates verified by the web coder in its own closeout — lint/tsc/build/test green.

## Post-release click-check list

1. **Drafting card** — feature in draft with no spec artifact: card shows no brief line, phase label «drafting» next to the status column.
2. **In-progress card** — feature inProgress with tasks: brief «first goal paragraph…» between priority/version and the task counter, phase «in progress».
3. **Queue excerpt in a real MCP session** — planning_task_queue entries carry briefExcerpt + phase; a bodyless feature shows briefExcerpt null.
4. **Cyrillic long-goal card** — goal paragraph over 150 cyrillic chars: brief capped at 150 + «…», no broken glyphs.
5. **Done card** — phase label «done» per the existing done-card treatment.

## Follow-ups (microbatch queue)

- None new from this wave. (Wave 099/028 carryovers remain queued separately.)

## Status

Feature ready for user validation; result stays draft until the architect pass and user validation. No live dogfood in this wave.

<log>
- 2026-10-04 [agent] — drafted by dotnet-coder at the wave close: outcome per task, decisions (phase field name, static planner, null-vs-empty excerpt, cap semantics), verification evidence (consistency test both surfaces + final suite 1863/0 sequential), post-release click-check list (5 cards/scenarios).
</log>