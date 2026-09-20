---
feature: 032
slug: status-model-self-documentation
status: validated
created: 2026-09-20
validated: 2026-09-20
---

# Result 032 — Status model self-documentation: lifecycle в onboarding + per-kind статусы в MCP-тулах

<summary>
Feature 032 done: the status model is now self-documenting on every agent-facing surface. overmind_onboard quickstart carries the lifecycle (5 feature statuses, auto-transitions, escape hatch + terminal done, per-kind artifact statuses, pre-read hint); planning_set_status / planning_upsert_artifact descriptions quote the per-kind sets verbatim with the logNote rule; 13 pinning tests hold docs and model in sync — they read the descriptions through a live MCP process (what agents actually see) and fail on any drift, including new enum members. The Mindlash failure mode (agents building on false premises because the board was silent about its own model) is closed at the documentation layer; the process layer (teaching the full workflow) is carved as feature 036.
</summary>

<tasks>
01 — Self-documentation block + drift pins: OnboardQuickstart.cs lifecycle block (5 lines); OvermindPlanningTools.cs description updates (two tools; planning_set_feature_status untouched as the style etalon); StatusModelContractTests.cs (13 pins, set-equality parsing + enum reflection + live ListToolsAsync surface); Mcp.Tests csproj gained ProjectReferences to Core+Host (zero NuGet). Review PASS (audits/review-032-01.md), 969/969.
</tasks>

<evidence>
- Tests 956 → 969 (Mcp 220 → 233). Build 0 warnings, format clean on the delta.
- Reviewer break-the-string: all three mutations (drop "blocked" from task listing; add "done" to result listing; drop "ready" from arrow line) fail the expected pins; sources byte-restored (cmp + git diff verified).
- Pins read tool descriptions through a real MCP ListToolsAsync roundtrip and the quickstart via the OnboardQuickstart.Text constant — the exact strings traveling to agents.
- Live agent pickup: rides the next deploy + MCP adapter restart (pinned in task); pins already prove the wire format.
</evidence>

<deviations>
- None against the task contract. The gate misclick episode (auto-review variant briefly in spec v2) was fully reverted in v3 before any code — no model changes landed (anti-scope held).
</deviations>

<follow-ups>
- 036-agent-workflow-onboarding picks up the process layer (teaching the ceremony, not just the model).
- Review INFO-1 (shared "spec/plan/result:" marker in the pin parser — safe-fail on future rewording) accepted as design; not queued.
</follow-ups>

<log>
</log>