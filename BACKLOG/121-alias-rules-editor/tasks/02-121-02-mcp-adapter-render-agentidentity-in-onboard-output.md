---
feature: 121
task: 02
slug: alias-rules-editor
status: todo
review: light
created: 2026-10-06
brief: MCP: render the agentIdentity block in overmind_onboard output (AppendIdentity) + tool description mention; OnboardingToolContractTests pins
---

# 121/02 — MCP adapter: render agentIdentity in onboard output

<goal>
Agents see the naming rules: the overmind_onboard text output renders the agentIdentity block (default alias/vendor + rules verbatim) and the tool description names the block.
</goal>

<context>
The Host serves OnboardAgentIdentityBlock (OnboardApi.cs:107-110) but OvermindOnboardingTools.Format renders materialization/validation/semantic/adapter only — the identity block is silently dropped, so agents never see the rules. Spec dc030eba-1f10-4bf2-a68f-f9fea8534b97, plan ee02bacf-1651-4e60-9b16-a5f672647fa8 (slice 02).
</context>

<scope>
- src/Overmind.Mcp/OvermindOnboardingTools.cs: new AppendIdentity(payload, output) called between AppendValidation and AppendSemantic (Format :81-84); one "Agent identity:" line (default alias/vendor + planning_claim_feature pointer) followed by the naming rules verbatim (may be multi-line); tolerant of a missing block (older hosts).
- Description (:22): extend the carried-blocks list with the agentIdentity block (default alias/vendor + the project's naming rules for choosing your own stable alias).
- tests/Overmind.Mcp.Tests/OnboardingToolContractTests.cs: identity line rendered when the block is present (custom rules string asserted); no crash when absent; description key phrases per the existing pin style.
</scope>

<anti-scope>
- No host changes (task 01 owns the endpoint); no claim-tool changes; no ServerInstructions or quickstart text changes; no installer managed block changes.
</anti-scope>

<constraints>
- Standalone adapter (no Core/Host reference): read the block from the raw payload JSON.
- Description edits keep the double pins green: 099 key phrases + 119 adapter-block text.
</constraints>

<acceptance-criteria>
- [ ] Identity line (alias/vendor + rules) rendered in the onboard output; custom rules string asserted in a test.
- [ ] Missing agentIdentity block does not crash Format.
- [ ] Tool description mentions the agentIdentity block; description contract tests green.
- [ ] dotnet build green; Overmind.Mcp.Tests green.
</acceptance-criteria>

<refs>
- Spec: dc030eba-1f10-4bf2-a68f-f9fea8534b97; Plan: ee02bacf-1651-4e60-9b16-a5f672647fa8 (slice 02).
- src/Overmind.Mcp/OvermindOnboardingTools.cs:22/:49-89; tests/Overmind.Mcp.Tests/OnboardingToolContractTests.cs.
- Feature 119-adapter-version-awareness — description-pin precedent.
- Task 01 (order 1) independent; task 03 (order 3) independent of this task.
</refs>

<obstacles>
- (none yet — append during execution)
</obstacles>

<log>
- 2026-10-06 [agent] — task created from validated plan slice 02 (batch breakdown; review: light).
</log>