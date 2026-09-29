---
feature: 008
slug: ceremony-matrix-sync
status: inProgress
priority: p2
version: 1
created: 2026-09-29
---

# Feature 008 — Ceremony capability matrix sync: batch ops, dependencies, agent import shipped

## Goal

The `overmind-ceremony` skill's Capability Matrix (all four runtime copies) claims three shipped capabilities are still missing. The Overmind board proves otherwise:

- 044 batch planning ops — **done**: `planning_upsert_tasks` exists, atomic; proven live on 007 (7 task artifacts in one call).
- 030 structured feature dependencies — **done**: `planning_add/remove/list_feature_dependencies` exist; sprint-planner skill is built on them.
- 058 agent-side import — **done**: `planning_import_feature` exists (OM-7).

Stale rows make future agents loop single calls where one atomic batch exists, and hand-edit dependencies as prose. The ceremony itself prescribes: "if it shipped, update this matrix."

## Requirements

- Update `overmind-ceremony/SKILL.md` on all four sides (quadruple-edit): move 044/030/058 from the "Does NOT exist yet" table to "Works — safe to rely on" with board proofs; the missing table keeps only 029 (tombstones), 051 (entries materialization), 052 (briefing tool), 063 (worktree binding).
- Add one batch-quirk line to "Working the Artifacts": batch-created task bodies register only some XML sections for later `planning_patch_artifact(section=...)` (bug note `6ee3be0f` on the Overmind project) — stash in `<log>` as workaround.
- Mirrors stay byte-identical.

## Non-goals

- No changes to any other skill/agent file, no workflow changes, no SKILLFLOW edits.
- No tombstone/materialization/briefing-tool entries — those are genuinely still draft.

<log>
- 2026-09-29 [auto] — artifact created → inProgress
</log>
