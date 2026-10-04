---
feature: 114
slug: claims-onboarding-docs
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Result 114 — Claims onboarding: teach the claim workflow in MCP quickstart + crew skills

## What shipped

**Repo lane (task 01, coder, `fa570a3` + review fix `1fa80d9`):**
- OnboardQuickstart.Text: the Claims paragraph is now a five-point practical workflow — claim before the first feature mutation (artifact upsert, status move, priority move); orchestrator claims / sub-agents inherit via the shared adapter session; alias+vendor from the agentIdentity block; 409 recipe (live holder → ask the user, stale/ended → take-over legitimate); EOF auto-release + unclaim at handoff/wave close.
- MCP server instructions (Program.cs): one line — claim the feature you work, quickstart explains the workflow. Visible before onboarding.
- 11 content pins across OnboardApiTests (9), StatusModelContractTests (8 overlapping per reviewer — see nits), HandshakeContractTests (2): all contains-based.

**Kit lane (task 02, orchestrator, crew commits `788430e` + `78737d3`, pushed to origin):**
- overmind-ceremony: «Claims workflow (099)» section with the same five points; capability matrix updated — the DEAD `planning_set_current` retired from the lifecycle row, claim/unclaim (099) + `planning_delete_artifact` (094) listed, matrix date bumped to 2026-10.
- feature-orchestrator: queue-based feature pick + claim at takeover (replacing the pin vocabulary that still taught the REMOVED set_current — the exact «practice not landing» this feature hunted), Phase 2 note (sub-agents never claim themselves), new Wave Closeout section (unclaim).

## Review

Both lanes light PASS-WITH-NITS, 0 blockers (reviewer re-ran gates independently: build 0/0, Api 608/0, Mcp 443/0).
- A1 WARNING fixed on the spot: the retired `planning_set_current` leaked into the quickstart mutation list through spec R1a (coder implemented the spec faithfully); dropped in `1fa80d9` together with its pin.
- B1/B2 fixed on the spot in the kit (`78737d3`): last «pinned current feature» mention in orchestrator Step 1; lock count corrected 12 → 13 (delete_artifact added the 13th locked route), spec anti-scope patched the same way (demoted to draft — re-validation pending).
- Nits → microbatch: #181 (identity point unpinned), #182 (overlapping pin set across two suites), #183 (repo-wide format vs 3 migration files — extends old #23).

## Verification

- dotnet build 0/0; Overmind.Api.Tests 608/0; Overmind.Mcp.Tests 443/0 (coder + reviewer independently).
- OnboardApiTests re-run after the A1 fix: 23/23 (targeted).
- Crew kit: both commits on origin/main; working tree clean of my edits.
- Live dogfood bonus: after the user restarted this session's MCP adapter, the first-ever orchestrator claim landed (114 claimed by Claude/anthropic, state alive) — the workflow this feature documents now works on its own author.

<log>
- 2026-10-04 [agent] — coder landed the repo lane; reviewer passed both lanes (0 blockers); A1/B1/B2 fixed on the spot; result drafted.
- 2026-10-04 [agent] — spec anti-scope patch (12→13) demoted the spec to draft; re-validation pending with the result tap.
</log>