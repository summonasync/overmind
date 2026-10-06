---
feature: 121
slug: alias-rules-editor
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Spec 121 — Alias rules: settings editor + stable personal alias in ceremony skill

<goal>
The user writes the agent-naming prompt («как выбирать алиас») in project settings; agents read it in the agentIdentity block of the onboard payload and pick a STABLE personal alias per those rules. Vendor = the runtime (claude / qwen / …), alias = the agent's own chosen name — two agents of the same runtime must not look identical on the board.
</goal>

<scope>
Code ground truth (verified 2026-10-06):
- EXISTS since 099: `Project.AgentNamingRules` (string?, trim + null-on-whitespace via `SetClaimSettings`, Project.cs:35/64), `ClaimStaleThresholdMinutes` default 15; the Host serves `OnboardAgentIdentityBlock(alias="agent", vendor="unknown", rules ?? GenericNamingHint)` at OnboardApi.cs:107; the claim flow (`planning_claim_feature(alias, vendor)`) is complete.
- GAPS this feature closes (body demand vs code):
  1. No write surface — no endpoint calls `SetClaimSettings` (the API test seeds via raw SQL UPDATE); `ProjectResponse` does not expose the fields. The user cannot save rules at all.
  2. No web slot — zero hits for namingRules/claimSettings across web/src; there is no settings editor anywhere.
  3. MCP adapter drops the block — `OvermindOnboardingTools.Format` renders materialization/validation/semantic/adapter but never agentIdentity, and the tool description does not mention it. Agents never see the rules.
  4. Ceremony skill (`~/.claude/skills/overmind-ceremony/SKILL.md`, claim section) says claim "with your alias+vendor" but never tells the agent to CHOOSE a stable personal alias per the naming rules and keep it across sessions.

Build:
- Host: `GET/PUT /api/projects/{projectId}/claim-settings` mirroring PlanningWorkflowApi — body `{ claimStaleThresholdMinutes, agentNamingRules }`, echo response; FluentValidation (threshold >= 0, rules <= 2000 chars); `MaxNamingRulesLength` const + guard in `SetClaimSettings` mirroring `SetManifest`.
- Web: claim settings section on the project detail (projects page) — textarea for the naming prompt (maxLength 2000) + save; threshold stays API-only.
- MCP adapter: render the agentIdentity block in the `overmind_onboard` output and mention it in the tool description.
- Ceremony skill: stable-personal-alias instruction referencing the agentIdentity naming rules.
</scope>

<anti-scope>
- No server-side alias enforcement or uniqueness — display collisions remain a UX smell (099 decision).
- No claim-flow changes: planning_claim_feature/unclaim, session liveness, stale thresholds semantics, take-over/adoption — untouched.
- No changes to `AgentIdentityDefaults` texts or the generic hint wording.
- No threshold editing in the web UI (API-only; the entity couples it into the same PUT, the form sends it back untouched).
- Installer managed block (`OnboardManagedBlock.cs`) untouched — it carries no alias wording today.
- No schema migration (columns exist since 099).
</anti-scope>

<constraints>
- Rules text is freeform prompt text: trim; empty/whitespace → cleared (null); max 2000 chars (brief-style cap — validator returns 400 ValidationProblem, entity guard mirrors SetManifest).
- `claimStaleThresholdMinutes` >= 0 (0 = stale detection off, 099 semantics).
- The adapter is a deliberate standalone copy (no Core/Host reference) — render from the raw payload JSON, no contract dependency.
- MCP tool description edits keep the existing key-phrase pins (099/03 relaxation + 119 adapter-block text).
</constraints>

<acceptance-criteria>
- [ ] User can edit and save naming rules in the web project settings; reload shows the persisted text.
- [ ] PUT/GET /api/projects/{id}/claim-settings round-trip: echo; whitespace-only rules → null; threshold 0 allowed; -1 → 400; 2001 chars → 400; unknown project → 404.
- [ ] Onboard agentIdentity carries the saved rules — proven through the API round-trip (not only the existing SQL-seeded test).
- [ ] `overmind_onboard` text output renders the identity block (alias/vendor + naming rules); the tool description mentions it; Mcp.Tests pins green.
- [ ] Ceremony skill instructs: stable personal alias per the agentIdentity naming rules; vendor = runtime; same-runtime agents differ by alias.
- [ ] dotnet build + full serial dotnet test green; web lint + tsc + tests + build green.
</acceptance-criteria>

<refs>
- Feature 099-agent-claims — infra origin (spec decisions; tasks 01 core model, 03 MCP/HTTP surface).
- Feature 119-adapter-version-awareness — sibling onboard-payload work; description-pin relaxation precedent.
- Code: src/Overmind.Core/Entities/Project.cs (:35, :64-69, SetManifest guard :71-79); src/Overmind.Host/Api/OnboardApi.cs (:107-110); src/Overmind.Host/Api/Contracts/OnboardContracts.cs (:25); src/Overmind.Host/Api/AgentIdentityDefaults.cs; src/Overmind.Mcp/OvermindOnboardingTools.cs (:22, :49-89); src/Overmind.Host/Api/PlanningWorkflowApi.cs (endpoint pattern); src/Overmind.Host/Program.cs (:165, :174); src/Overmind.Host/Api/Validation/ProjectValidators.cs; web/src/pages/projects-page/ui/projects-page.tsx (:102-110 mount points); web/src/features/planning/components/workflow-settings.tsx (form pattern); web/src/features/planning/queries.ts (:18, :45 key pattern); tests/Overmind.Api.Tests/AgentSessionLockApiTests.cs (Onboard_NamingRules_ProjectValueAndGenericHint); tests/Overmind.Mcp.Tests/OnboardingToolContractTests.cs; web/tests/feature-number-settings.test.mts.
- Outside the repo: /home/johan-boolman/.claude/skills/overmind-ceremony/SKILL.md (claim section, lines 81-86).
</refs>

<obstacles>
- The ceremony skill lives outside this git repo (crew config) — the executor edits the file directly; the change never appears in repo diffs or commits.
- Raw-SQL seeding in AgentSessionLockApiTests exists precisely because no write API was possible before — keep the SQL-seeded leg as the storage pin and add the API round-trip as separate coverage.
- The onboard tool description is pinned by two test generations (099 key phrases, 119 adapter text) — the description edit must keep both green.
</obstacles>

<log>
- 2026-10-06 [agent] — spec drafted by feature-orchestrator (claim held by Van/claude main session); code ground truth verified: the four gaps (write surface, web slot, adapter rendering, skill instruction) confirmed against src; batch mode — spec+plan drafted together, the human taps both in one sitting later.
- 2026-10-06 [agent] — validated with human proof: notification 252273fe-4716-4a05-aa7b-3e8996384ab2 replied 2026-10-06 13:05:14 UTC
</log>