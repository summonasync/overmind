---
feature: 122
task: 01
slug: artifact-validation-buttons
status: todo
review: light
created: 2026-10-04
brief: API: human decline route — [ui] log line, status untouched
---

# Task 01 — Decline route (human door)

<goal>
A human-door decline action records the veto on the artifact log without touching status.
</goal>

<scope>
- POST route on the artifact (human namespace, same family as the validate door): optional reason (trim, ~500 cap) → appends a [ui] log line «declined by human: <reason>» (043 marker discipline; bare line when reason empty).
- Task artifacts → 400 (decline does not apply to the task status flow).
- Deleted artifacts → the standard not-found family behavior.
- Tests: reason appends, empty reason appends bare line, task artifact 400, log line carries the [ui] marker and the reason.
</scope>

<anti-scope>
- No status changes; no agent notifications; no MCP surface.
</anti-scope>

<acceptance-criteria>
- [ ] Decline route live with the three pinned paths (reason / bare / task-400); log line sourcing correct.
</acceptance-criteria>

<refs>
- Spec 467ba128; plan 5a1e9c7b; AgentPlanningApi validate door family; 043 provenance.
</refs>

<log>
- 2026-10-04 [agent] — task cut post-validation (human proof a7103fb2).
</log>