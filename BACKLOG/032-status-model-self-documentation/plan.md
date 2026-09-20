---
feature: 032
slug: status-model-self-documentation
status: draft
created: 2026-09-20
---

# Plan 032 — Status model self-documentation: lifecycle в onboarding + per-kind статусы в MCP-тулах

<approach>
Single small task — two text surfaces in code plus drift-pinning tests. No backend model changes, no web. (The auto-review variant considered at the gate was a misclick and is out; see spec v3 provenance.)
</approach>

<tasks>
01 — Self-documentation block + drift pins: OnboardQuickstart.cs lifecycle block (≤8 lines: arrow line, auto-transitions, escape hatch + terminal done, per-kind artifact statuses, pre-read hint); OvermindPlanningTools.cs description updates (planning_set_status per-kind sets + logNote-mandatory; planning_upsert_artifact created-draft + validate-via-planning_validate); Mcp tests pinning quickstart + descriptions against PlanningFeatureStatus / PlanningArtifact.AllowedStatuses for all kinds (name-level, including the "result has no done" negative). Closeout: build + suite, live adapter check after the next deploy restart.
</tasks>

<verification>
- dotnet build + dotnet test green.
- The pinning tests fail if anyone changes the model or the texts without the other — that is the feature's whole point.
- Live: after next deploy + MCP adapter restart, overmind_onboard output contains the lifecycle block.
</verification>

<risks>
- Description strings are load-bearing for agent behavior (Mindlash built on a false premise from silence) — texts must quote the model verbatim, no paraphrasing that drops a status.
</risks>