---
feature: 094
task: 03
slug: artifact-deletion
status: todo
review: light
created: 2026-10-04
depends_on: 02
brief: Web delete affordance + confirm, then closeout: consistency pass, architect light, result with post-release click-check
---

# Task 03 — Web UI + closeout

<goal>
Give the human door a UI affordance for deletion and close the wave out (consistency, architect light, result).
</goal>

<scope>
- Roster row overflow / artifact detail delete affordance with a confirm step listing what gets removed (artifact, its versions, materialized file on next pass).
- Deleted artifacts disappear from the lists (React Query invalidation per house idiom).
- Closeout: consistency pass across doors, architect light (orchestrator dispatches), result artifact with a post-release click-check item (delete a duplicate task via MCP, verify mirror cleanup).
</scope>

<anti-scope>
- Restore UI (v1.1), batch deletion UI, tombstone display.
</anti-scope>

<constraints>
- BFF pattern: the UI goes through the Next.js backend, no direct host calls.
</constraints>

<acceptance-criteria>
- [ ] Delete affordance with confirm on roster row / artifact detail; confirm lists consequences.
- [ ] Deleted artifact disappears from lists after mutation (invalidation pinned in web tests where cheap).
- [ ] Consistency pass across doors recorded.
- [ ] Architect light dispatched and verdict recorded.
- [ ] Result artifact drafted with the post-release click-check item.
</acceptance-criteria>

<refs>
- Spec 1966d6c8 (surfaces); plan 0b80c38а (task 3).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-10-04 [agent] — task cut from validated plan.
</log>