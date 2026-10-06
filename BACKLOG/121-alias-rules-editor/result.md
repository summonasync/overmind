---
feature: 121
slug: alias-rules-editor
status: validated
created: 2026-10-06
validated: 2026-10-06
---

# Result 121 — Alias rules: settings editor + stable personal alias in ceremony skill

<goal>
The naming-rules pipeline is complete end-to-end: the user writes the naming prompt in the project settings UI (claim settings section, textarea capped at 2000 mirroring the Host cap; the coupled claim-stale threshold travels the same PUT and stays API-only by design), the Host persists it via GET/PUT /api/projects/{id}/claim-settings, the MCP adapter now RENDERS the agentIdentity block (default alias/vendor + naming rules verbatim) in the onboard output — closing the silent-drop gap — with the tool description pointing at it, and the stable-alias contract text landed in all 4 kit ceremony-skill ports (md5-identical).
</goal>

<shipped>
- Host (392568f): ClaimSettingsApi (GET/PUT /api/projects/{projectId:guid}/claim-settings, PlanningWorkflowApi shape) + ClaimSettingsContracts + UpdateClaimSettingsRequestValidator (threshold >= 0, rules <= 2000) + MaxNamingRulesLength = 2000 guard in SetClaimSettings (mirrors SetManifest); registered in Program.cs + DependencyInjection.cs. Tests: ClaimSettingsApiTests (echo, whitespace → null, threshold 0 stored, -1 → 400, 2001 chars → 400, unknown 404), API-driven onboard round-trip leg in AgentSessionLockApiTests, entity throw pin in EntityFactoryTests.
- MCP adapter (50fcedb): AppendIdentity renders the agentIdentity block between the validation and semantic blocks of the onboard text; tolerant of a missing block (older hosts); the tool description names the block. OnboardingToolContractTests pins + Canned payload extended.
- Web (1c2aeed): claim-settings-section.tsx on the project settings (projects page, after the planning workflow settings) — textarea maxLength 2000; types/api/queries + use-claim-settings hook wiring per features/projects, barrel export; claim-settings-section.test.mts (node:test).
- Kit contract text (3c67637, kit repo): the stable-alias instruction — "Your alias is your own STABLE personal name — pick it per the project's naming rules from the agentIdentity block of the overmind_onboard payload, reuse the SAME alias every session; vendor = runtime; same-runtime agents differentiate by alias; defaults (agent / unknown) are placeholders" — landed in all 4 ceremony-skill ports, md5-identical.
</shipped>

<verification>
- Reviews: all four slices light — APPROVED (spec-compliance first pass + code quality), no blockers carried into the queue.
- Full suites green: dotnet build + serial dotnet test across Api/Core/Infrastructure/Mcp; web lint + tsc + node tests + build.
- Round-trip proven at the test level: PUT rules → onboard agentIdentity carries them; clearing → generic hint path.
</verification>

<handoff-to-human>
1. The deployed skill sync (repo kit ports → ~/.claude/** etc.) is YOUR act at result validation — until synced, live agents keep reading the old ceremony skill and never see the stable-alias instruction.
2. Browser eyeball of the new claim settings section (projects page → project detail, after planning workflow settings): edit → save → reload shows the persisted text.
3. Gemini port lag noted (ledger #265, quad-sync candidate) — that port stays behind the other three until the microbatch lands.
4. Threshold is API-only by design (defaults accepted at the flag round): the UI edits the rules text; claimStaleThresholdMinutes rides the same PUT and has no dedicated input.
</handoff-to-human>

<refs>
- Spec: dc030eba-1f10-4bf2-a68f-f9fea8534b97; Plan: ee02bacf-1651-4e60-9b16-a5f672647fa8.
- Tasks: 01 1632905c-d8a3-4c64-b37c-c93e052a664d, 02 0ace522e-6285-44e3-b3f2-48a7b891e4b1, 03 5abf8ebc-d8f9-4066-b596-b78107bc1c4c, 04 092c47d1-58bc-42fd-bfc9-5ab955fe93fb — all done, light approved.
- Commits: 392568f (121/01 Host), 50fcedb (121/02 adapter), 1c2aeed (121/03 web) — this repo; 3c67637 (121/04 kit contract text) — kit repo.
- Feature 099-agent-claims (infra origin); 119-adapter-version-awareness (description pins); ledger #265 (gemini port lag, quad-sync candidate).
</refs>

<obstacles>
- The ceremony-skill sync into the live ~/.claude/** locations is deliberately NOT done by agents (out-of-repo, human act at result validation) — task 04's deliverable stayed in-repo as pinned contract text per the coordinator's reframing.
</obstacles>

<log>
- 2026-10-06 [agent] — result drafted by feature-orchestrator from the coordinator's closeout report (commits 392568f/50fcedb/1c2aeed + kit 3c67637; all slices light-approved, full suites green); result notifies are batched by the main session.
</log>