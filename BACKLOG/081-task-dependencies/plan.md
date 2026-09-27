---
feature: 081
slug: task-dependencies
status: validated
created: 2026-09-27
validated: 2026-09-27
---

# Plan 081 — Task-level dependencies: blockedBy on task artifacts, honored by current_task (030-семантика на уровень тасок)

<approach>
Five tasks layered bottom-up, reusing the 030 blueprint at every layer. User decisions locked: edges = `blockedBy` order-array on upsert (no add/remove tools), closure = done|review, full-stack including web.

- 030 lessons applied: the current-task resolver physically lives in `AgentPlanningApi.Current` (Host) — task-blocked skipping is added minimally there plus an `OpenBlockersAsync`-style query in a new Infrastructure service; stop-state shape follows the existing current_task stop/empty formats; human door never blocked.
- Edge resolution: API surface speaks sibling task ORDERS; the service resolves to artifact ids at upsert, rejecting unknown orders, self-reference, and cycles (030 DFS with chain in error) with 400s. Declarative semantics: re-upsert replaces the task's edge set; empty array clears.
- Additive-by-construction: existing upserts without `blockedBy` behave byte-identically; roster/task payload growth is task-only fields exactly like review/acTicked before it; all canned contract pins (070/080 roster shape, current_task shapes) updated in the same tasks that change the payload.
- Materialization follows the 030 depends_on precedent: registry-driven key, blockers-first canonical order, byte-stable round-trip, additive import with report entries.

<tasks>
- [ ] 01-core-edges — Core + persistence: `PlanningTaskDependency` entity (030/EntryTag idiom: BlockedById → BlockingId, private ctor, internal Create) + EF config + migration; `PlanningArtifact` (task) gains edge-collection navigation + domain helpers to replace/clear its edge set. Tests (Core.Tests): entity construction, unique-pair semantics. Gates: build 0W/0E + suite → tasks/01-core-edges.md
- [ ] 02-dependency-service — Infrastructure: `TaskDependencyService` — resolve orders→ids, replace/clear edges, DFS cycle detection (direct + transitive, chain in error), `UnclosedBlockersAsync(taskId)` (closure = blocker done|review), blocked-state queries for the resolver and listings; `PlanningArtifactService.UpsertAsync` (task path) accepts optional `blockedBy` (orders), applies replace-semantics inside the existing transaction; artifact delete cascades edges. Tests (Infrastructure.Tests): cycle direct/transitive with chain, unknown order 400, self-ref 400, replace/clear, closure open/close on done/review, re-block on rollback to in_progress, cascade on delete. Gates: build + suite → tasks/02-dependency-service.md
- [ ] 03-current-task-roster — Host + contracts: `AgentPlanningApi.Current` skips blocked tasks (lowest-order-first resolution unchanged) and renders the all-blocked stop reason naming the chain (task order + slug, stop state shape per existing formats); roster + full feature read task items gain additive `blocked` + `blockedBy` (sorted orders); canned contract pins updated (070 roster shape, current_task canned). Tests (Api.Tests): skip-picks-next-actionable, all-blocked stop names chain, roster fields task-only, no-edges feature behaves identically. Gates: build + suite → tasks/03-current-task-roster.md
- [ ] 04-mcp-payload — Mcp: no new tools (user decision); `planning_upsert_artifact`/`planning_upsert_tasks` forward optional `blockedBy`; `planning_current_task`/`planning_list_artifacts`/`planning_read_feature` pass new fields through; canned stubs + count pins (unchanged tool count) refreshed. Tests (Mcp.Tests): blockedBy forwarded, current_task stop passthrough, roster fields passthrough. Gates: build + suite → tasks/04-mcp-payload.md
- [ ] 05-materialization-import — Infrastructure: `depends_on:` frontmatter key on task files (blockers-first canonical order, byte-stable), importer maps it back to edges (order-based within the feature), additive report entries; round-trip test (import → materialize → re-import byte-stable, 030 bar). Gates: build + suite → tasks/05-materialization-import.md
- [ ] 06-web-chips-panel — Web: blocked chips on task cards (feature workspace) + dependencies panel on the feature page (030 panel pattern: blockers with order+title+closed state, muted when closed); data via existing endpoints. Gates: npm lint + tsc + build; manual panel/chips check → tasks/06-web-chips-panel.md
- [ ] 07-docs-closeout — Docs: CONTEXT task-dependency sentence (030 sibling), STRUCTURE write-surface note (blockedBy on upsert, closure done|review), bootstrap dependency-routing line gains the task-level clause (030 precedent); result artifact + live MCP walk (chain setup → current_task skip → all-blocked stop). Gates: grep pins, build+suite, walk protocol in log → tasks/07-docs-closeout.md
</tasks>

<risks>
- **Order-addressing vs re-ordering**: edges reference sibling ORDERS; re-ordering tasks (order edits) re-points edges silently. Mitigation: order is the canonical address in every surface (frontmatter, roster, chips); a future order-edit UX is out of scope; importer resolves depends_on after all task files of the feature are loaded (order known).
- **current_task stop-shape drift**: the all-blocked stop must match existing stop/empty shapes (072/030 precedents) — reviewer rejects any new shape.
- **Roster contract growth**: adding blocked/blockedBy touches the 080 canned pins — additive-only growth, task-only fields, updated in task 03 (reviewer watches for breaking shape drift).
- **Graph scale**: 030 microbatch flagged graph-scale tradeoff (#68) — tasks-per-feature is small (≤50); DFS per upsert is fine; noted, not solved here.
</risks>

<verification>
- dotnet build 0W/0E + full suite green (baseline 1561).
- Web gates: lint + tsc + build green.
- Live MCP walk (task 07): create 3 tasks with chain T3 blockedBy [1,2] → current_task returns T1 → complete T1 → returns T2 → blocked all → stop names chain → roster shows blocked flags → frontmatter round-trip.
- Grep pins: CONTEXT/STRUCTURE clauses present.
</verification>

<log>
- 2026-09-27 [agent] — plan drafted after 030-pattern recon (squeezr-assisted): five+two layered tasks, resolver stays in AgentPlanningApi (030 lesson), order-based declarative edges, depends_on follows the 030 registry precedent.
- 2026-09-27 [agent] — validated with human proof: notification d974ce4f-e9dd-4d82-929d-984e0735f2f2 replied 2026-09-27 18:51:45 UTC
</log>