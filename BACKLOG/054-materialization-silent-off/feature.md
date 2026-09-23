---
feature: 054
slug: materialization-silent-off
status: inProgress
priority: p0
version: 1
created: 2026-09-23
---

# Feature 054 — Bug: planning materialization is silent-off — default off, errors swallowed, quickstart overpromises (OM-2)

Field report OM-2 layer 1 (FEATURELIST/overmind-features-2026-09-23.md, DMT run 2026-09-23): after `planning_create_feature` + spec/plan + 5 tasks for `mmt-36-2`, no `BACKLOG/` tree appeared on disk (neither `D:\Projects\DMT-AspireWrapper\BACKLOG\` nor `D:\Projects\DMT\BACKLOG\`), git status clean.

Root causes verified in code:

1. `SettingDefaults.PlanningMaterializationDefault = "off"` — fresh projects resolve strategy **Off** unless a per-project override (`Project.PlanningMaterialization`) or the global setting says otherwise. `PlanningServiceGuard` then skips writing, and nothing tells the agent this happened (compounded by the 042 rendering gap).
2. `PlanningServiceGuard.SyncMaterializationAsync` catches ALL exceptions and logs a host-side warning — a materialization failure never reaches lastRun, the API response, or the agent.

The quickstart claims "the BACKLOG/ tree under the project root is rewritten after every planning mutation" — true only when strategy is on.

Target model from the field report: Overmind is the single write point, `BACKLOG/` is a read-only mirror agents read with native Read/Grep. Blockers of that model: OM-1 and OM-2.

<log>
- 2026-09-23 [auto] — artifact created → inProgress
</log>
