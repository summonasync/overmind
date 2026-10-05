---
feature: 109
slug: done-gate-human-tap-and-result-required
status: validated
created: 2026-10-05
validated: 2026-10-05
---

# Result 109 — Done gate: agent-side review→done needs a human tap; done (any door) requires a result artifact

## What shipped

The done transition is now gated on both doors:

- **Agent door** (`planning_set_feature_status` → `/api/agent/.../status`): `done` requires (1) a `result` artifact on the feature — teaching 409 `FeatureResultRequired` naming the kind and the fix path; (2) a FRESH human tap — an approval reply with `RepliedAt >= result.UpdatedAt` (same proof semantics as the 077 validate gate; editing the result voids the tap), teaching 409 `FeatureDoneTapRequired` walking the notify→reply flow. Hidden notifications are NOT filtered — proof stays valid after archiving (invariant #139 pinned by a dedicated test).
- **Human door** (UI): result-required with a visible explanation, no tap mechanics — the click is the action.
- **Provenance:** agent-path done stamps `done via agent review by {alias} ({vendor}), approved by human tap {yyyy-MM-dd}` from the claim system (099); unclaimed degrades to the no-reviewer form; appended before claim release.
- **Non-done transitions untouched** (gate fires only on target=done); auto-transitions and the materializer never write done (pinned); import restores done without the gate — restoration of already-gated disk state, not a close action (recorded in obstacles, architect-endorsed).
- **Implementation:** `FeatureDoneGate` (internal static, Infrastructure) — `RequireResultAsync` (anchor = freshest result's UpdatedAt) + `RequireHumanTapAsync` + `ProvenanceLine`; `SetStatusAsync(requireHumanTap)` defaults false by the ValidateAsync precedent; the agent endpoint is the single production caller with `true`; MCP rides the same route.
- **Docs wave:** ceremony skill Gates §3 + capability matrix, feature-orchestrator closeout step, MCP `planning_set_feature_status` description — all four surfaces state the same rule (architect-verified).

## Review rounds

- T1 light review PASS-WITH-NITS 0 blockers → orchestrator fix round: result-gate hoisted above `feature.Update` in UpdateAsync (tracked-entity mutation impossible before the 409); done-theory restored to all 4 legal start states (draft/ready/inProgress/review).
- Architect light **APPROVED**, 0 blockers, 0 warnings (audits/architect-109.md): layers clean, gate semantics consistent with 077 (`>=`, hidden policy), all doors covered, four doc surfaces consistent, provenance format exact per spec.

## Gates (re-run by orchestrator)

build 0w/0e exit 0 (clean rebuild), Core 365, Infrastructure 672, Api 655 (+2 theory rows), Mcp 486 — all green; format verify green (Infrastructure + Api tests projects).

## Microbatch

#239 (architect S1): shared FreshApprovalReplyAsync helper when the proof policy next moves — both sides pinned meanwhile.

## Post-release click-check

1. Agent asks done on a feature without a result → 409 teaching error names `result` and `planning_upsert_artifact(kind=result)`.
2. Result written, agent flips done without a tap → 409 `FeatureDoneTapRequired` teaching notify→reply.
3. Notify approval → user taps → agent flips done → OK; feature log carries `done via agent review by Van (claude), approved by human tap <date>`; claim released.
4. After a tap, edit the result → tap is void: a new done attempt from a fresh feature 409s until a newer tap.
5. UI: delete a result and flip a feature to done from the board → refused with the result-required explanation (no tap mechanics on the human door).
6. Review promotion unaffected: result landing still auto-promotes to review; non-done transitions never hit the gate.

<log>
- 2026-10-05 [agent] — closeout complete: docs wave (ceremony ×2, orchestrator, tool description), architect light APPROVED 0/0 (audits/architect-109.md), result drafted with click-check. T1 fix round closed earlier (gate hoist + theory restore, gates green). Waiting on the human tap.
</log>