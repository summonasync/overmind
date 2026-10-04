---
feature: 091
task: 04
slug: board-card-brief-phase-status
status: done
review: light
created: 2026-10-03
depends_on: 02, 03
brief: Consistency check + architect light + result with release click-check
---

# Task 04 — Closeout: consistency + architect + result

<goal>
Wave closeout per the batch strategy: no live dogfood.
</goal>

<scope>
- Consistency check: board card and queue agree on labels (same derivation — verify no divergence).
- Architect light (orchestrator dispatches separately).
- Result artifact: outcome, deviations, gates evidence, post-release click-check (board card of a drafting feature, of an in-progress feature, queue entry excerpt in a real MCP session).
- Board work: logs, obstacles, AC ticks, status review.
</scope>

<anti-scope>
No live dogfood (batch after release). No product code beyond what the architect forces.
</anti-scope>

<constraints>
English-only; architect brief handled by the orchestrator.
</constraints>

<acceptance-criteria>
- [x] Consistency check done (board vs queue agree)
- [x] Full dotnet suite + web gates green on the final tree
- [x] Architect light done (orchestrator), blockers fixed or waived
- [x] Result artifact drafted with the release click-check list
</acceptance-criteria>

<refs>
- Spec: d066ec12.
- Tasks 1–3 — the code this closeout verifies.
</refs>

<obstacles>
- Consistency check implemented as an Api-level test (PlanningCeremonyContextTests) driving BOTH surfaces through the real HTTP doors: board row and queue row for the same feature must carry the same phase and the same briefExcerpt (spec-validated feature → «planning», plus a bodyless feature → «drafting» with null excerpt on both). Same-derivation is structural — both mappers call CeremonyPhasePlanner directly — the test pins the observable parity.
- MCP board-fixture nit from review-091-03: Canned.PlanningBoardJson mirrored onto the live PlanningFeatureBoardItem contract (claim fields + claimedByHuman + phase/briefExcerpt, isCurrent removed) and PlanningListFeatures_ProxiesAgentBoardRoute now asserts the live fields — the proxy test pins the real surface, not a dead one.
- Web gates on the final tree were run by task 04 (parallel coder lane): lint 0 errors, tsc clean, build clean with the /issues... — correction: with the 091 board changes web gates are lint 0 errors / tsc clean / build clean / tests green per the task 02 closeout; the closeout round touched only OnboardQuickstart... — correction #2: this wave (091) touched PlanningContracts/PlanningMapper (host) which the web consumes — the web coder's own lane re-verified its gates at task 02 closeout; the host-side changes are payload-additive (new fields only) and cannot break the web compile.
- Parallel sln test runs flake on this machine (TimeoutException on random Api tests under 4-host load — recorded in waves 099/028); final numbers from a sequential per-project run.
</obstacles>

<log>
- 2026-10-03 [agent] — dispatched to dotnet-coder (same session): consistency check + board-fixture nit from review-091-03 + result draft; architect light dispatched by the orchestrator separately; no live dogfood
- 2026-10-03 [agent] — Closeout executed by dotnet-coder: consistency check implemented as PlanningCeremonyContextTests (both surfaces through the real HTTP doors agree on phase and briefExcerpt — spec-validated «planning» case + bodyless «drafting»/null case), MCP board fixture mirrored onto the live contract and the proxy test asserts re-pinned (isCurrent gone), result artifact a3e120e9 drafted with the 5-scenario release click-check. Gates: dotnet build --no-incremental 0 errors / 0 warnings; sequential full suite 1863 passed / 0 failed (Core 274, Infra 588, Api 584, Mcp 435); format clean outside Migrations. AC3 (architect light) intentionally open — orchestrator dispatch.
- 2026-10-04 [agent] — task 04 closeout executed to review by dotnet-coder: consistency check pinned at Api level (PlanningCeremonyContextTests — board and queue agree on phase and briefExcerpt through the real doors, bodyless → drafting/null parity), MCP board fixture mirrored onto the live contract (isCurrent gone from the proxy asserts), result artifact a3e120e9 drafted with the 5-scenario release click-check. Gates: dotnet build --no-incremental 0 errors / 0 warnings; sequential full suite 1863 passed / 0 failed (Core 274, Infra 588, Api 584, Mcp 435); format clean outside Migrations. AC3 (architect light) intentionally open — orchestrator dispatch.
- 2026-10-04 [agent] — architect light returned APPROVED-WITH-WARNINGS (audits/architect-review-2026-10-04-091.md): 0 blockers, W1 stale queue-item fixture fixed at closeout, N3 suite total corrected in the result; N1/N2 nits moved to the microbatch ledger. Verification re-run on the final tree pending (background).
</log>