---
feature: 094
task: 02
slug: artifact-deletion
status: todo
review: light
created: 2026-10-04
depends_on: 01
brief: Both doors: MCP planning_delete_artifact (draft-only, alias, logNote) + HTTP DELETE (human, unrestricted) + door tests
---

# Task 02 — Doors: MCP tool + HTTP DELETE

<goal>
Expose guarded deletion through both doors: the agent MCP tool (draft-only) and the human HTTP DELETE (unrestricted).
</goal>

<scope>
- planning_delete_artifact(projectId, artifactId | alias, source, logNote): draft-only guards on the agent door, description documents the guard matrix (agents cannot delete validated/done; humans can via the UI), alias per 090.
- HTTP DELETE on the human door: unrestricted per matrix; 409s for edge-blocked tasks.
- Tests both doors: guard 409s, alias resolution, audit line in the feature log, human-deletes-validated path, unknown artifact 404 with the alias hint.
</scope>

<anti-scope>
- Web UI (task 03), restore, force flag.
</anti-scope>

<constraints>
- Description = documentation for agents: state the matrix verbatim, do not make agents discover it by 409s.
</constraints>

<acceptance-criteria>
- [ ] MCP tool with contract tests: draft delete OK; validated/done 409 (agent); alias resolution; audit line lands in the feature log.
- [ ] HTTP DELETE: human deletes a validated artifact; auto-transition fallout pinned; blockedBy task 409.
- [ ] Tool description documents the guard matrix verbatim (pinned).
- [ ] 404 carries the 090 alias hint.
</acceptance-criteria>

<refs>
- Spec 1966d6c8 (surfaces); plan 0b80c38а (task 2); alias machinery 090; description-as-doc convention 037.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-10-04 [agent] — task cut from validated plan.
</log>