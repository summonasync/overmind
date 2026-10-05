---
feature: 105
task: 02
slug: register-all-task-sections
status: done
review: light
created: 2026-10-04
depends_on: 01
brief: Door pins + consistency + architect + result
---

# Task 02 — Door pins + closeout

<goal>
Pin the behavior on both doors and close the wave.
</goal>

<scope>
- MCP/HTTP door pins: canonicalization observable through both doors; import round-trip byte-stable for already-canonical bodies.
- Consistency pass; architect light; result with post-release click-check (upsert a body with an inline section, patch it).
</scope>

<acceptance-criteria>
- [x] Door pins green; import round-trip pinned.
- [x] Architect verdict recorded; result drafted with click-check.
</acceptance-criteria>

<refs>
- Spec 110bcde0; plan 7eecf2c9 (task 2).
</refs>

<log>
- 2026-10-05 [agent] — [agent] Taken by orchestrator (task 01 done). Door pins: HTTP door pinned in AgentPlanningApiTests (Patch_InlineSectionAfterUpsert_SucceedsWithoutResend), import round-trip pinned in PlanningRoundTripTests, MCP door pin added by orchestrator (6deadff, PlanningPatchArtifact_InlineSectionHint_SurfacesThroughTheDoor — Mcp 455). Consistency decision: tool descriptions deliberately not extended — the 400 hint IS the just-in-time documentation ("re-send the body once"), firing exactly when an agent hits the case; spec's three-entrance scope honored (feature bodies excluded by design). Dispatching architect light.
- 2026-10-05 [agent] — [agent] Closeout complete: door pins green (HTTP + MCP + import round-trip), architect light APPROVED-WITH-WARNINGS with W1-b (convention pinned in STRUCTURE.md) and W2 (depth-cap boundary test, Core 338) fixed in-tree (5351e1b); W3 (ADR) requires the user's approval per house rule — riding the return-tap stack. Result 0905d689 drafted with click-check. Both AC ticked. Closing task.
</log>
