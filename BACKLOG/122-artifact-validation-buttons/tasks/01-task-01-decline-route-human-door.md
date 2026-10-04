---
feature: 122
task: 01
slug: artifact-validation-buttons
status: done
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
- [x] Decline route live with the three pinned paths (reason / bare / task-400); log line sourcing correct.
</acceptance-criteria>

<refs>
- Spec 467ba128; plan 5a1e9c7b; AgentPlanningApi validate door family; 043 provenance.
</refs>

<log>
- 2026-10-04 [agent] — task cut post-validation (human proof a7103fb2).
- 2026-10-04 [agent] — [agent] Claimed by Van (claude), taken by orchestrator — dispatching dotnet-coder (sonnet). Noted: user-controller filed 104/04 (mirror section-renderer corruption on repeated obstacles patches) — joins the 104 wave after this lane.
- 2026-10-04 [agent] — [orchestrator] Light review PASS-WITH-NITS (0 blockers, commit a8d05b9): all 5 spec pins confirmed, source hardcode Ui correct (no spoof surface), structural injection into the log impossible (anchored regexes + no newlines), AppendLogAsync reused cleanly. Reviewer correction to the brief's pin: PlanningFailureCode has NO enum-append-to-end rule (043 is about [source] log markers; STRUCTURE.md:54 = member + factory; numeric values never persisted — mid-insert is safe). Ledger: #193 double LoadArtifactOrThrow, #194 human-door [ui] attribution is unauthenticated by design (loopback binding; pin in CONTEXT if the host ever leaves 127.0.0.1), cap-500 pre-trim documented here.
- 2026-10-04 [agent] — [agent] Review passed (PASS-WITH-NITS, 0 blockers), AC ticked. Code in a8d05b9. Closing task; result drafted at wave close (after 122/02 review).
</log>