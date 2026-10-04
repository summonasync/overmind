---
feature: 114
slug: claims-onboarding-docs
status: draft
created: 2026-10-04
---

# Spec 114 — Claims onboarding: teach the claim workflow in MCP quickstart + crew skills

<goal>
The 099 claim workflow (claim/unclaim, session lock, EOF auto-release, stale take-over) ships with mechanics but the PRACTICE is not landing in the field. Agents still ask how to use claims. This feature puts the practice where agents actually read: the MCP onboarding quickstart, the server instructions, and the two crew skills that drive orchestration (overmind-ceremony, feature-orchestrator).
</goal>

<scope>
- R1. OnboardQuickstart.Text: the single Claims paragraph becomes a practical workflow block covering five points:
  (a) WHEN — claim the moment a feature enters active work, before the first feature mutation (set_current, artifact upsert, status move);
  (b) WHO — the orchestrator session claims; sub-agents inherit the lock through the shared adapter session and must NOT claim/unclaim themselves;
  (c) IDENTITY — alias+vendor come from the agentIdentity block of the onboard response (project naming rules apply);
  (d) 409 RECIPE — a refused mutation names the holder: holder session live → ask the user before taking over; stale/ended → take-over is legitimate;
  (e) EOF AUTO-RELEASE — a dead adapter process releases its claim on its own; there is nothing to clean up. Unclaim when handing off or closing the wave.
- R2. MCP server instructions (Overmind.Mcp Program.cs): one added line telling agents to claim the feature they work and pointing at the quickstart — claims must be visible before onboarding, not only inside it.
- R3. overmind-ceremony SKILL.md: the existing claim paragraph grows into a "Claims workflow" section carrying the same five practice points, phrased for the ceremony loop (claim before set_current / first mutation; subagents inherit; unclaim at handoff).
- R4. feature-orchestrator SKILL.md: the orchestration flow gains explicit claim and unclaim steps — claim before dispatching sub-agents, unclaim when the wave closes.
- R5. Pin tests: the existing quickstart contract tests (OnboardApiTests / OnboardingToolContractTests) gain pins on the new practice phrases so the docs cannot silently rot.
</scope>

<anti-scope>
- No claim mechanics changes: the 12-mutation lock list, thresholds, take-over rules and lock filter are 099 territory and stay untouched.
- No UI changes (claim chips exist from 099/04).
- No other crew skills (node-coder, dotnet-coder, ...) — claiming is an orchestrator-level concern; executors inherit the lock.
- CONTEXT.md stays as the 099 wave left it.
</anti-scope>

<constraints>
- Docs-only feature: zero behavior changes outside constant strings and skill markdown.
- Repo texts in English (repo convention).
- R3/R4 files live in ~/.claude/skills/ — outside any git repo; those edits are checkpoint-commit-free by nature.
- Pin tests must assert content, not exact full-string equality (quickstart text keeps evolving).
</constraints>

<acceptance-criteria>
- [ ] Quickstart carries all five practice points (when/who/identity/409/EOF+unclaim) in readable prose; pin tests green.
- [ ] Server instructions mention claiming and point to the quickstart.
- [ ] Both crew skills updated: ceremony has the Claims workflow section, orchestrator flow has claim/unclaim steps.
- [ ] dotnet build + affected test suites green; result drafted.
</acceptance-criteria>

<refs>
- 099-agent-claims (mechanics owner); OnboardQuickstart.cs; Overmind.Mcp/Program.cs (instructions block); ~/.claude/skills/overmind-ceremony/SKILL.md; ~/.claude/skills/feature-orchestrator/SKILL.md; OnboardApiTests.cs; OnboardingToolContractTests.cs.
</refs>

<log>
- 2026-10-04 [agent] — drafted; priority raised to p0 by user request in-session («его надо грумить и стартовать следующим»).
</log>