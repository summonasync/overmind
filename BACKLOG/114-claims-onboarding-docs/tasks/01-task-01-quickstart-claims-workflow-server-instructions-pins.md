---
feature: 114
task: 01
slug: claims-onboarding-docs
status: done
review: light
created: 2026-10-04
brief: Repo lane: quickstart five-point claims block + instructions line + pin tests
---

# Task 01 — Quickstart claims workflow + server instructions + pins

<goal>
The MCP surface teaches the claim practice: quickstart carries the five-point workflow, server instructions point at it before onboarding, pins keep the docs from rotting.
</goal>

<scope>
- OnboardQuickstart.Text: the single Claims paragraph (line ~30) becomes a practical workflow block carrying five points: (a) claim before the first feature mutation, (b) orchestrator claims / sub-agents inherit via the shared adapter session and must not claim themselves, (c) alias+vendor from the agentIdentity block, (d) 409 recipe — live holder → ask the user; stale/ended → take-over legitimate, (e) EOF auto-release — nothing to clean up; unclaim at handoff or wave close. Keep it prose, readable, in the quickstart's existing voice.
- Overmind.Mcp/Program.cs server instructions (the instructions block around line 115): one added line — claim the feature you work, see the onboarding quickstart.
- Pin tests in OnboardApiTests / OnboardingToolContractTests: assert the new practice phrases exist (content pins, not full-string equality).
</scope>

<anti-scope>
- No claim mechanics changes; no skill markdown (task 02 owns the crew kit); no CONTEXT.md.
</anti-scope>

<constraints>
- English texts, existing quickstart tone (terse, imperative).
- dotnet build + affected suites green at the end.
</constraints>

<acceptance-criteria>
- [x] Quickstart carries all five practice points in readable prose.
- [x] Server instructions mention claiming and point to the quickstart.
- [x] Pin tests assert the practice phrases; dotnet build + affected suites green.
</acceptance-criteria>

<refs>
- Spec 93282324; plan 5f55b79d; OnboardQuickstart.cs; Overmind.Mcp/Program.cs; 099-agent-claims (mechanics owner).
</refs>

<log>
- 2026-10-04 [agent] — task cut post-validation (human proof 301c2149).
- 2026-10-04 [agent] — [agent] taken by orchestrator, dispatched to dotnet-coder (sonnet).
- 2026-10-04 [agent] — [agent] Light review PASS-WITH-NITS (0 blockers — review A: warn A1 set_current leak in quickstart fixed on the spot in 1fa80d9 with its pin; suggestions A2 identity pin / A3 dup pin-set → microbatch). Gates re-run by reviewer: build 0/0, Api 608/0, Mcp 443/0. Closing task; result stays draft per pipeline mandate.
</log>