---
feature: 109
task: 01
slug: done-gate-human-tap-and-result-required
status: todo
review: light
created: 2026-10-04
brief: Backend: result-required + human-tap gate on agent done, provenance in log, both doors tested
---

# Task 01 — Backend gate + provenance

<goal>
done requires a result and (agent door) a fresh human tap; provenance lands in the log.
</goal>

<scope>
- Agent door set_feature_status → done: result-required check (teaching error naming the missing kind) + human-proof requirement (fresh approval reply newer than the result's last update — validation-gate mechanics; error teaches the notify→reply flow).
- Agent-path done stamps the feature log line with reviewed-by provenance from the claim-naming system: «done via agent review by {alias} ({vendor}), approved by human tap {date}».
- Human door: result-required check with a visible explanation; no tap mechanics.
- Non-done transitions unchanged; auto-transitions never reach done (already true — pin it).
- Tests: done without result (both doors), result-but-no-proof (agent), fresh-proof done (agent, provenance asserted), review flip unaffected.
</scope>

<acceptance-criteria>
- [ ] Gate matrix pinned across both doors.
- [ ] Provenance line asserted on agent-path done.
- [ ] Non-done transitions and review path unchanged — pinned.
</acceptance-criteria>

<refs>
- Spec 931c87a5 (v2); plan ed1bec3e (task 1); validation-gate mechanics (077); 099 claim naming; 043 provenance.
</refs>