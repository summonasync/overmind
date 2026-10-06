---
feature: 090
task: 02
slug: field-nits-batch
status: done
review: light
created: 2026-09-29
depends_on: 01
brief: HTTP query flag includeArtifacts on GetFeature + compact roster DTO + MCP read_feature flag + contract tests (default byte-compatible)
---

# Task 2 — includeArtifacts on feature read (R2)

<goal>
Cheap feature read: planning_read_feature(projectId, slug, includeArtifacts=false) returns the feature body plus an artifact roster summary without dragging every artifact body and log.
</goal>

<scope>
- src/Overmind.Host/Api/PlanningFeaturesApi.cs — GetFeature query flag includeArtifacts (default true).
- src/Overmind.Host/Api/Contracts/PlanningContracts.cs — compact roster item (kind, order, status, brief for tasks, acTicked/acTotal) used when false.
- src/Overmind.Mcp/OvermindPlanningTools.cs — planning_read_feature gains the flag, passes it through, description updated (body + roster summary without artifact bodies — the cheap read).
- tests/Overmind.Api.Tests/PlanningApiTests.cs — contract tests.
</scope>

<anti-scope>
- Web UI changes (web keeps the default).
- sections= style partial reads on the feature body.
</anti-scope>

<constraints>
- Default (true/absent) response must remain byte-compatible with today's contract — the web UI consumes it.
</constraints>

<acceptance-criteria>
- [x] includeArtifacts=false returns feature body + roster summary, zero artifact bodies and log lines.
- [x] Default response identical to the pre-change contract (contract test pins it).
- [x] planning_read_feature forwards the flag; description names the cheap read.
- [x] dotnet build + new tests green.
</acceptance-criteria>

<refs>
- Spec 62aac67e (R2); plan b78738e8 (Task 2).
</refs>

<obstacles>
- Consequential edits beyond the four listed files: compact response needed a new contract (PlanningFeatureRosterResponse) and mapper method (PlanningMapper.ToFeatureRosterResponse) — both in scope by necessity, flagged here. MCP planning_read_feature description is pinned byte-exact by tests/Overmind.Mcp.Tests/PlanningToolContractTests.cs (not StatusModelContractTests as the briefing guessed) — the pin was extended after the preserved prefix and the expected string updated in place.
- Behavior facts learned while writing tests: LogNote on first artifact create records no <log> entry (only body marker); task kind is born in "todo"; feature auto-log ("<log> ... artifact created → inProgress </log>") is appended to the feature body on create.
- Environment: one solution-wide concurrent `dotnet test` (all four test projects in parallel) showed 4 Api + 1 Mcp failures while both project-scoped runs were fully green in repeated runs (518/518, 390/390, twice each, before and after resume); the follow-up diagnostic run was OOM-killed (exit 137). Treated as host resource contention, not a code issue — verification numbers come from the project-scoped runs.
</obstacles>

<log>
- 2026-09-29 [agent] — task cut from validated plan.
- 2026-09-29 [agent] — [agent] taken into work; blocker (task 1) closed done; dispatching dotnet-coder (sonnet).
- 2026-09-29 [agent] — [agent] coder started: reading current state of PlanningFeaturesApi / PlanningContracts / OvermindPlanningTools / PlanningApiTests.
- 2026-09-30 [agent] — [agent] resumed after host restart: all edits verified intact in the working tree (impl + MCP + both test files); re-running build and test verification, then AC ticks.
- 2026-09-30 [agent] — coder completion notes: consequential-file list, contract-pin location correction, test-behavior facts, concurrent-run flake observation.
- 2026-09-30 [agent] — [agent] implementation complete, moved to review: includeArtifacts flag on GetFeature (both doors), PlanningFeatureRosterResponse compact DTO, ToFeatureRosterResponse mapper, planning_read_feature forwards the flag with extended description. dotnet build 0 warnings/0 errors; Overmind.Api.Tests 518/518; Overmind.Mcp.Tests 390/390. All 4 ACs ticked. Resumed once after host restart — edits verified intact, no rework needed.
- 2026-09-30 [agent] — [agent] done: light review PASS-WITH-NITS (audits/review-090-02.md), 4 NIT → microbatch, 0 blockers; default contract byte-pinned, both doors + MCP forwarding confirmed; reviewer rerun: build 0w, Api 518/518, Mcp 390/390. Resume-interrupt survived without rework.
</log>