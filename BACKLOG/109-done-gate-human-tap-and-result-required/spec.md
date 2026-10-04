---
feature: 109
slug: done-gate-human-tap-and-result-required
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Spec 109 — Done gate: agent-side review→done needs a human tap; done (any door) requires a result artifact

> Grooming 2026-10-04 (user in session, decisions given directly + extended by the user mid-session).

## Goal

`done` stops being a plain agent mutation. Per the user: (1) an AGENT-side review→done flip needs the user's tap; (2) ANY MCP-side done requires a result artifact to exist first. The overall flow the user wants: the agent brings a feature to review — then EITHER the human reviews and closes it by hand, OR the agent's review cycle closes it through a UI approve pop-up with the result written readably.

## Decisions

- **Agent door (MCP `planning_set_feature_status` → done):**
  - No result artifact on the feature → 400/409 «write the result artifact first» (teaching error, names the missing kind).
  - Result present but no fresh human proof → the validation-gate mechanics: notify approval → the user taps the pop-up/reply → the flip goes through (proof newer than the result's last update, same family as planning_validate). The error teaches: «done needs your human's tap — send overmind_notify(kind=approval) and wait for the reply, or let the human flip in the UI».
  - Flips INTO review (and any non-done status) stay tap-free — unchanged.
- **Human door (UI) done:** the result-artifact requirement holds here too (default decision, challengeable — the invariant is cleaner complete; the UI click already means the human is present, so no tap mechanics needed, just the result check with a visible explanation).
- **Reviewed-by provenance (user idea, this session):** when done lands via the agent-review path, the feature's done log line carries the reviewer identity from the claim-naming system (099 aliases/vendors), e.g. `done via agent review by Dan (qwen), approved by human tap 2026-10-04`. NO new tag mechanism — the 043 log-provenance discipline carries it (challengeable: a real feature-tag taxonomy is a separate thought if the user wants filtering later).
- **Feature-level auto-transitions** never reach done on their own (done is never auto) — no changes there; the gate lives on the two explicit doors.
- **023/08 legacy**: the delegated-closure flow keeps working but now ends in a tap — the logNote provenance stays mandatory as today.

## Non-goals

- Gates on other transitions (review stays free); a feature-tag taxonomy; changing result artifact semantics; bulk done.

<log>
- 2026-10-04 [agent] — groomed with the user in session per their direct decision: agent done = tap + result, review stays free, human door = result check only (default, challengeable).
- 2026-10-04 [agent] — extended mid-session by the user: two sanctioned closing paths (human review → hand close; agent review → tap-approved close with a readable result) + reviewed-by provenance in the done log line using 099 claim aliases (no new tag mechanism — challengeable).
- 2026-10-04 [agent] — user's mid-session extension: two closing paths + reviewed-by provenance in the done log line (099 aliases), no tag mechanism
- 2026-10-04 [agent] — validated with human proof: notification b769b517-9cb0-4996-b32c-db3d961053a9 replied 2026-10-04 12:03:13 UTC
</log>