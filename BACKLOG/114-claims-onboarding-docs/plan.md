---
feature: 114
slug: claims-onboarding-docs
status: validated
created: 2026-10-04
validated: 2026-10-04
---

# Plan 114 — Claims onboarding: teach the claim workflow in MCP quickstart + crew skills

<goal>
Land the claims practice in the three surfaces: MCP quickstart + server instructions (repo lane), crew skills (kit lane).
</goal>

<sequence>
- 01 (repo, coder): OnboardQuickstart.Text claims-workflow block (R1 five points), server instructions line (R2), pin tests (R5). dotnet-coder.
- 02 (crew kit, orchestrator): overmind-ceremony "Claims workflow" section (R3), feature-orchestrator claim/unclaim steps (R4), checkpoint commit in the crew-kit repo (R6). Orchestrator-executed — content work, no build surface.
- Reviews: one light review per lane, dispatched as a batch when both land. Then result.
</sequence>

<verification>
- dotnet build + targeted test suites green on the repo lane.
- Crew-kit lane: git -C src/OvermindCrewKit status clean after its checkpoint commit.
- Reviewer re-runs the repo gates; crew skill edits reviewed for consistency with the quickstart phrasing (same five points, no drift).
</verification>

<refs>
- Spec 93282324; OnboardQuickstart.cs; Overmind.Mcp/Program.cs; src/OvermindCrewKit/.claude/skills/{overmind-ceremony,feature-orchestrator}/SKILL.md; OnboardApiTests; OnboardingToolContractTests.
</refs>

<log>
- 2026-10-04 [agent] — plan drafted on the in-repo kit layout (spec v2).
- 2026-10-04 [agent] — validated with human proof: notification 301c2149-8561-4a63-a22b-c7abee0deb4e replied 2026-10-04 17:29:01 UTC
</log>