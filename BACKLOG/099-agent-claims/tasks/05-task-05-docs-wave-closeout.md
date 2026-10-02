---
feature: 099
task: 05
slug: agent-claims
status: done
review: light
created: 2026-09-30
depends_on: 04
brief: ceremony skill + CLAUDE.md + server text pin→claim, live dogfood matrix, architect light, result artifact
---

# Task 05 — Docs wave + closeout

<goal>
The same-wave ceremony/doc flip from pin language to claim language, live verification of the whole matrix, review, and the result artifact.
</goal>

<scope>
- /home/johan-boolman/.claude/skills/overmind-ceremony/SKILL.md: pin → claim language (ceremony loop, briefing contract note that subagents inherit via the shared session).
- Global CLAUDE.md «Pin the feature you are working on» rule → claim wording (via the user-visible diff; flag the config-file edit for the user).
- src/Overmind.Mcp/SessionBootstrapPrompt.cs + ServerInstructions + quickstart text: claim wording, naming-rules mention.
- Live dogfood on a scratch project against a locally running host: claim → foreign-session mutation 409 → holder unclaim → stale take-over (short threshold) → EOF auto-release on adapter kill.
- architect-review light over the whole wave; fix blockers.
- Result artifact: outcome, deviations, follow-ups (crew memory tombstone of pin-current-feature-on-start noted as post-deploy agent work).
</scope>

<anti-scope>
No product code changes beyond what review forces. No migrations. No new features.
</anti-scope>

<constraints>
Docs in English; skill/CLAUDE.md edits are config-adjacent — surface diffs to the user rather than silently rewriting enforcement sections (CLAUDE.md enforcement/config sections are human-only; the pin rule line lives outside them — if in doubt, present the diff and ask).
</constraints>

<acceptance-criteria>
- [x] Skill + CLAUDE.md + server text use claim language, no pin leftovers in ceremony flow
- [x] Live dogfood matrix executed and logged: claim, foreign 409, unclaim, stale take-over, EOF auto-release
- [x] Full dotnet suite + web lint/tsc/build green on the final tree
- [x] architect light review done, blockers fixed or waived with rationale
- [x] Result artifact written; feature ready for review
</acceptance-criteria>

<refs>
- Spec: 0e774399 (Docs & ceremony wave + Migration sections).
- Tasks 1–4 — the code this closeout verifies.
</refs>

<obstacles>
- Global CLAUDE.md carries NO pin rule today: grep for pin/current-feature/planning_set_current in /home/johan-boolman/.claude/CLAUDE.md returns zero hits (the only "pin" matches are version-pinned NuGet wording and index_status text — unrelated). Nothing to flip; the ceremony-skill flip covers the standing rules. The crew memory file pin-current-feature-on-start (agent-owned memory, OUTSIDE CLAUDE.md) still carries pin wording — its tombstone/rewrite stays post-deploy agent work per the spec, listed in the result follow-ups.
- ServerInstructions (adapter Program.cs) never contained pin wording — verified, left as-is. The pin text lived in OnboardQuickstart (step 2 + escape hatch) — flipped to claim language with a new Claims paragraph (lock semantics, self-release, take-over, naming-rules pointer in step 2).
- Dogfood import ordering: the foreign-import scenario must run while the importing adapter is still alive — scripted after take-over and before the EOF kill.
- Stale-ageing in dogfood used a raw SQL LastSeenAt update on the scratch database (no HTTP threshold endpoint exists; raw write against the isolated scratch DB is the same isolation trick the unit tests use).
</obstacles>

<log>
- 2026-10-02 [agent] — dispatched to dotnet-coder (same session): docs wave + live dogfood + result draft; architect light will be dispatched by the orchestrator separately after the code/docs land
- 2026-10-02 [agent] — Docs wave + dogfood executed. OnboardQuickstart flipped to claim language (step 2 claim pick with naming-rules pointer, escape hatch de-pinned, new Claims paragraph: lock semantics, self-release on session end/stale, take-over, unclaim on handoff); ServerInstructions + SessionBootstrapPrompt verified pin-free; ceremony skill standing rule flipped to planning_claim_feature/planning_unclaim_feature (subagents inherit via shared session, unclaim on handoff, live 409 etiquette); CLAUDE.md — pin rule absent in the current tree, nothing to flip (obstacles + result follow-ups carry the memory-file tombstone note). Live dogfood: isolated scratch HOME/host 4799, two real adapters, all 6 scenarios green (transcript /tmp/dogfood-099-transcript.txt). Final tree: dotnet build 0/0, suite 1794/0 (Core 252, Infra 553, Api 570, Mcp 419); web lint 0 errors, tsc clean, build clean, tests 48/48. Result artifact 35613fc8 drafted. Architect light is the orchestrator's dispatch — not run by the coder, AC left open.
- 2026-10-02 [agent] — task 05 executed to review by dotnet-coder: docs wave landed (OnboardQuickstart claim language, ceremony skill standing rule flipped, CLAUDE.md pin rule absent — nothing to flip), 6-scenario live dogfood green on the isolated host (transcript saved), result artifact 35613fc8 in draft, gates green on the final tree (dotnet 0/0 + 1794/0, web lint/tsc/build/test clean). AC3 (architect light) intentionally open — the orchestrator dispatches it separately.
- 2026-10-02 [agent] — 2026-10-02 [agent] — architect-review light over the whole wave: APPROVED-WITH-WARNINGS, 0 blockers / 2 warnings (W1: ceremony skill line 48 still references planning_set_current — violates task 05 AC «no pin leftovers»; W2: POST queue/reorder echo returns claimState:null for claimed features — the only claim surface without ClaimLivenessAsync, breaks the null⇒unclaimed invariant) / 5 nits (→ microbatch; N5 human-claim-ages-out belongs in result follow-ups); independent verification 1794/0 + web gates; report audits/architect-review-2026-10-02-099.md. Fix round dispatched.
- 2026-10-02 [agent] — Micro closeout for architect 099/05: W1 — ceremony SKILL.md capability-matrix cell flipped from planning_set_current to planning_claim_feature/planning_unclaim_feature (last pin leftover in the ceremony flow; grep now clean); W2 — queue/reorder echo wired through FeatureClaimService.ClaimLivenessAsync like QueueMove (claimState for claimed features restored, null⇒unclaimed invariant holds), Reorder handler takes FeatureClaimService; pinning test Reorder_Echo_CarriesClaimFieldsForClaimedFeature (claimed feature carries alias+alive, unclaimed carries nulls); N5 — human-claim aging appended to the result follow-ups (35613fc8 v2) and the Status paragraph records APPROVED-WITH-WARNINGS with W1/W2 closed. Gates: dotnet build 0/0; full suite 1795 passed / 0 failed (Core 252, Infra 553, Api 571, Mcp 419). AC3 ticked (architect light done, warnings closed).
- 2026-10-02 [agent] — architect light APPROVED-WITH-WARNINGS (0 blockers) + closeout round closed both warnings (ceremony skill claim cell, reorder echo liveness + pin) and appended N5 to result follow-ups; AC all ticked; gates 1795/1795 + web green. Closing as done — wave complete.
</log>