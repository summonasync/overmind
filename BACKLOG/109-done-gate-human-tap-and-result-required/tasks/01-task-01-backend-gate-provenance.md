---
feature: 109
task: 01
slug: done-gate-human-tap-and-result-required
status: done
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
- [x] Gate matrix pinned across both doors.
- [x] Provenance line asserted on agent-path done.
- [x] Non-done transitions and review path unchanged — pinned.
</acceptance-criteria>

<refs>
- Spec 931c87a5 (v2); plan ed1bec3e (task 1); validation-gate mechanics (077); 099 claim naming; 043 provenance.
</refs>

<obstacles>
- `PlanningFeatureService.SetStatusAsync` gained `requireHumanTap` inserted before `ct`: legacy positional `default` args at old call sites now bind to the flag, not the token. Service-level default stays `false` (same shape as `ValidateAsync(requireHumanProof)`), the agent door (AgentPlanningApi) passes `requireHumanTap: true` explicitly; test call sites moved to named args where the door semantics matter.
- Unclaimed-feature agent done has no 099 identity to cite: provenance degrades to `done via agent review, approved by human tap <date>` (no reviewer segment). Claimed features cite `ClaimedByAlias (ClaimedByVendor)`; a held claim adds its `claim released on done-close` line AFTER the provenance line, so the MCP echo `lastLogLine` shows the release line, not provenance — tool description updated accordingly.
- Result anchor is the newest result artifact's `UpdatedAt` (max), tap must satisfy `RepliedAt >= result.UpdatedAt`; hidden notifications are NOT filtered out — invariant #139 (hidden proofs stay valid) pinned by a dedicated test.
- Human door (UpdateAsync with status) got the result check only; the check throws before any entity mutation, so a refused close leaves title/body/priority edits unapplied too (all-or-nothing per request).
- Backlog import path (BacklogImporter.MapFeatureStatus) still restores `done` features from disk ungated — import materializes existing state, it is not a close action; left untouched deliberately.
</obstacles>

<log>
- 2026-10-05 [agent] — taken by orchestrator — dispatching dotnet-coder (sonnet) for backend done gate + provenance
- 2026-10-05 [agent] — dotnet-coder: done gate landed. Core: FeatureResultRequired/FeatureDoneTapRequired (RuleViolation, teaching 409s); Infra: FeatureDoneGate (result anchor = newest result UpdatedAt, tap = approval reply RepliedAt >= anchor, hidden replies count per #139) + SetStatusAsync(requireHumanTap) with provenance stamp before claim release + human-door UpdateAsync result-only check; agent door passes requireHumanTap: true; MCP planning_set_feature_status description teaches the gate. Provenance: claimed features cite alias (vendor), unclaimed degrade to no-reviewer form. Gates: build 0w/0e; tests Infra 672, Api 653, Mcp 486, Core 365 all green; dotnet format clean on 6 touched projects.
- 2026-10-05 [agent] — light review PASS-WITH-NITS 0 blockers (gate matrix both doors, provenance format exact, >= comparison consistent with 077, hidden-proof #139 pinned, import-path restoration accepted as legit, MCP covered via AgentPlanningApi requireHumanTap:true). Orchestrator fix round: nit 2 — result-gate hoisted above feature.Update in UpdateAsync (tracked-entity mutation now impossible before the 409; Api tests pin unchanged behavior), nit 1 — done-theory restored to 4 legal start states (draft/ready/inProgress/review, +2 rows). Gates re-run: build 0/0 exit 0, Infra 672, Api 655 (+2), format green.
</log>