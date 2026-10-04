---
feature: 109
slug: done-gate-human-tap-and-result-required
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 109 — Done gate: agent-side review→done needs a human tap; done (any door) requires a result artifact

> Grooming 2026-10-04. 2 tasks, 01 → 02.

## Task chain

1. **01 — Backend gate.** set_feature_status → done (agent door): result-required check (teaching 400/409 naming the missing kind) + human-proof requirement (validation-gate mechanics: fresh approval reply newer than the result's last update; error teaches the notify→reply flow); done via the agent path stamps the done log line with reviewed-by provenance from the claim-naming system (099 alias/vendor — «done via agent review by Dan (qwen), approved by human tap <date>»); non-done transitions unchanged; human door: result-required check with a visible explanation, no tap mechanics; tests: done without result (both doors), done with result but no proof (agent), done with fresh proof (agent, provenance line asserted), review flip unaffected.
2. **02 — Closeout.** Docs: 023/08 legacy flow now ends in a tap (skill/CLAUDE.md/server text where delegated closure is described); architect light; result with post-release click-check (agent requests done → tap → done lands with provenance; UI done without result refused).

## Verification

- Gate matrix tests (01), gates; architect light (02).

<log>
- 2026-10-04 [agent] — plan groomed: 2 tasks; human-door result check is the challengeable default, recorded in the spec.
- 2026-10-04 [agent] — updated for the user's mid-session extension: reviewed-by provenance lands in the done log line (01), documented in closeout (02).
- 2026-10-04 [agent] — reviewed-by provenance added to task 01 per the user's mid-session extension
- 2026-10-04 [agent] — validated with human proof: notification b769b517-9cb0-4996-b32c-db3d961053a9 replied 2026-10-04 12:03:13 UTC
</log>