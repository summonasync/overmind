---
feature: 003
task: 03
slug: crew-role-boundaries
status: done
review: light
created: 2026-09-27
brief: sprint-planner skill+agent on .kimi-code + manifest/SYNC-STRUCTURE/SKILLFLOW registration
---

<goal>Create the sprint-planner skill + agent on .kimi-code and register it in manifest, SYNC-STRUCTURE and SKILLFLOW.</goal>
<scope>
- New .kimi-code/skills/sprint-planner/SKILL.md. Scope: board-level planning ONLY — planning_task_queue triage; priority bands via planning_move_feature (band crossings ONLY on explicit user approval via planning_set_feature_priority); feature deps via planning_add_feature_dependency / planning_remove_feature_dependency; feature creation ceremony planning_create_feature + optional requirements-intake routing; sprint/phase slicing; phase-end handoff → audit-orchestrator. Anti-scope: never works a feature's inner pipeline (→ feature-orchestrator), never codes, never reviews, never touches task artifacts; ceremony mechanics defer to overmind-ceremony (read it first); overmind_onboard mandatory at start. Include the mandatory output contract block and Russian-communication rules, same as sibling skills.
- New .kimi-code/agents/sprint-planner.md in the existing kimi agent shape (no per-agent model pin — pool lives in config.toml; description: «Board-level planner: task queue, priorities, feature dependencies, sprint slicing. Use when triaging the backlog, creating features or planning a sprint»; body points at ~/.kimi-code/skills/sprint-planner/SKILL.md as single source of truth).
- manifest.json: add skill-dir entry + agent entry mirroring existing modes.
- SYNC-STRUCTURE.md: 2 file-mapping rows + pre-commit checklist #4 count parity 22 agents/25 skills → 23/26.
- SKILLFLOW.md: new alternative route section «Board planning (sprint)» → sprint-planner.
</scope>
<anti-scope>No port sides; no edits to existing skills beyond nothing — this task adds files and registration lines only; no BACKLOG/ or audits/ edits.</anti-scope>
<constraints>One skill one job; MCP tool names verbatim (mcp__overmind__*); English-only; NO git commit.</constraints>
<acceptance-criteria>
- [x] .kimi-code: agents count = 23, skills count = 26
- [x] SKILL.md states scope, anti-scope, exact tool names, output contract
- [x] manifest.json parses; SYNC-STRUCTURE parity line updated
- [x] SKILLFLOW.md names the sprint-planner route
</acceptance-criteria>
<refs>Spec f0fdb479 §2; Plan T3. Port side depends on this task (T4).</refs>
<obstacles></obstacles>
<log></log>

<log>
- 2026-09-27 [agent] — взята в работу исполнителем
- 2026-09-27 [agent] — Done: created .kimi-code/skills/sprint-planner/SKILL.md (board-level planning only: queue triage via planning_task_queue/planning_list_features; in-band ordering via planning_move_feature; cross-band moves via planning_set_feature_priority ONLY on explicit user approval; deps via planning_add_feature_dependency/planning_remove_feature_dependency; feature creation via planning_create_feature + intake routing to feature-orchestrator; sprint/phase slicing; phase-end handoff to audit-orchestrator; overmind_onboard mandatory; STOP lines authoritative; output contract; ceremony mechanics defer to overmind-ceremony) and .kimi-code/agents/sprint-planner.md (sibling kimi shape, no model pin). Registered: SKILLFLOW.md alternative route "Board planning (sprint)"; SYNC-STRUCTURE.md 2 file-mapping rows + checklist #4 parity 22/25 -> 23/26.
- 2026-09-27 [agent] — Obstacle 1 — manifest.json NOT edited by design (obstacles section not patchable on this artifact, recording here): manifest is v2, directory-level (23 per-DIRECTORY rows, no per-skill entries). Verified: json.load OK (23 entries); ./install.sh --dry-run deploys kimi side as whole-dir rsync of .kimi-code/agents/ and .kimi-code/skills/ (sprint-planner picked up automatically); ./uninstall.sh --dry-run clean. The scope line "add skill-dir entry + agent entry" described the v1 shape — v2 makes it a no-op (confirms T1 discovery). Obstacle 2 — <refs> "Spec f0fdb479" does not resolve via planning_read_artifact (shortened id); dispatch brief carried the distilled spec §2, used as content source. Obstacle 3 — port sides still 22/25 until T4; SYNC-STRUCTURE checklist #4 states the 23/26 target and becomes fully truthful after T4.
- 2026-09-27 [agent] — Executor: all 4 ACs verified and ticked, files + registrations in place, manifest no-op recorded in log. Ready for review.
- 2026-09-27 [agent] — Orchestrator review passed: SKILL.md content reviewed in full — scope/anti-scope/routing/red-flags/output contract all present, tool names verbatim, band-crossing approval rule explicit. Agent file matches kimi shape (quoted description, whenToUse, no model pin — pool by convention). SKILLFLOW route first in Alternative Routes; SYNC-STRUCTURE rows alphabetical + parity 23/26. Manifest no-op confirmed (dirs mirrored). Obstacle noted: empty <obstacles> section not patchable — logged for feature result.
</log>
