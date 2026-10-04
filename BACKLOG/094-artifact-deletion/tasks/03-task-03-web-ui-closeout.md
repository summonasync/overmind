---
feature: 094
task: 03
slug: artifact-deletion
status: done
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
- [x] Delete affordance with confirm on roster row / artifact detail; confirm lists consequences.
- [x] Deleted artifact disappears from lists after mutation (invalidation pinned in web tests where cheap).
- [x] Consistency pass across doors recorded.
- [x] Architect light dispatched and verdict recorded.
- [x] Result artifact drafted with the post-release click-check item.
</acceptance-criteria>

<refs>
- Spec 1966d6c8 (surfaces); plan 0b80c38а (task 3).
</refs>

<obstacles>
</obstacles>

<log>
- 2026-10-04 [agent] — task cut from validated plan.
- 2026-10-04 [agent] — taken into work
- 2026-10-04 [agent] — web lane: delete affordance on task roster rows (trash action in TaskCard strip) and document detail panel (Delete next to Edit); DeleteArtifactDialog confirm lists consequences (artifact + log, version history stays in DB, materialized BACKLOG file removed next pass) and pre-warns on blocked dependents (mirrors the server 409 "Clean up dependencies first") and on validated artifacts; 409 detail surfaces via ErrorText inside the dialog, dialog stays open on failure; success goes through the existing deleteArtifact mutation (planningKeys.lists() + feature invalidation). No feature-body delete added (anti-scope). Files: web/src/features/planning/lib/artifact-deletion.ts, components/delete-artifact-dialog.tsx, task-board.tsx, feature-workspace.tsx, index.ts barrel, tests/artifact-delete.test.mts (4 tests). Gates: eslint 0 errors (6 pre-existing warnings elsewhere), tsc --noEmit clean, node --test 71/71, npm run build green.
- 2026-10-04 [agent] — web lane complete, closeout follows by orchestrator
- 2026-10-04 [agent] — web lane review returned PASS (audits/review-094-03-web.md): 0 blockers, 0 warnings, 1 suggestion (close-guard pending during Escape — v1.1 polish, mirrors the delete-feature dialog behavior); error path end-to-end verified (409 detail reaches ErrorText inside the dialog), pessimistic mutation justified for irreversible soft delete, BFF/a11y/AI-trace clean, gates green (lint 0, tsc 0, node --test 71/71).
- 2026-10-04 [agent] — closeout complete: consistency verified by the architect pass (guard matrix single-owner, filter covers every read path, doors thin over the same service); architect light APPROVED-WITH-WARNINGS 0 blockers (audits/architect-review-2026-10-04-094.md), W1 CONTEXT.md roster fixed in closeout, N1-N3 to the microbatch ledger; result 0728e1ad drafted with the 5-scenario click-check. Gates: 1937/0 sequential + web 71/0.
</log>