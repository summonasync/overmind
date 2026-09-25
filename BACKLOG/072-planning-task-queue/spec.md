---
feature: 072
slug: planning-task-queue
status: draft
created: 2026-09-25
---

# Spec 072 — Planning task queue: MCP-тул «что брать следующим» — проекция борды (приоритет asc, blocked-aware, readiness), юзер рулит приоритетами с борды

<goal>
The board IS the queue. The human drags feature cards on the dashboard to reorder them (within one priority band; priorities remain the outer sort), and an MCP tool answers "what should be worked on next" from that same order: `planning_task_queue(projectId, limit?)`. After each task the agent re-pulls the queue and takes the next entry. Both sides of the glass can reorder: the human drags in the UI; the agent gets one-call gestures — move up/down, jump to a position — within a priority band; changing a priority band itself through MCP requires explicit user approval. No new UI entity — reorder happens directly on the existing dashboard board.
</goal>

<context>
User request 2026-09-24: "получить task-queue по mcp, а я изменяю". Socratic round 2026-09-25 reshaped the idea across four in-session turns: (1) draft v1 — "no mutation path, priorities only"; (2) the upgrade — "очерёдность на борде, чтобы я мог просто на борде менять очерёдность (внутри одного приоритета) — и наша борда = наша очередь. Первый на борде = первый в очереди. Никаких ui новых сущностей — drag n drop queue = drag n drop в дашборде", plus the flow: create a near-term queue → open UI → the feature that matters is not next → drag it forward → the agent works tasks, re-pulling the queue after each, and may change a priority with user approval when something surfaces mid-task; (3) "надо дать MCP пусть двигать таски внутри приоритета тоже" — the agent gets its own reorder door; (4) "я тебе сказал, двинь по очереди вверх/вниз — а ты не можешь — грусть?" — the agent door must be a one-call gesture, not a recompute-the-whole-band dance; full-band MCP reorder was dropped in favor of move gestures. Manual order is authoritative; readiness/blocked/current are decorations on top, never sort overrides. 035-execution-queue (closed superseded) is partially resurrected by this decision — this spec supersedes the superseding: manual order is back, as queue positions on the board, not as a separate entity.
</context>

<decisions>
- Order lives as `QueuePosition` (int, not null) on `PlanningFeature`. There is no queue table, no queue UI entity — the dashboard board sorted by `priority asc → queuePosition asc` IS the queue.
- Drag boundaries (both doors): reordering operates within ONE priority band. Moving between bands = changing priority (human: existing inline select 038; agent: planning_set_feature_priority with user approval). The composite board sort becomes `priority asc → queuePosition asc` (replaces the current tiebreaker).
- Backfill: migration assigns positions to existing features by `priority asc → createdAt asc`, numbered 1..N per priority group. New features auto-assign tail of their priority group (max+1). Import-created features take the same tail path.
- Done features keep their position value but drop out of the queue view (hide-done is already the board default); gaps in numbering are harmless — order is relative.
- Reorder API surface: ONE shared renumber service, three doors over it.
  - Human door `POST .../planning/queue/reorder`: full ordered slug list of one priority band (the UI always sends the complete post-drop order); server renumbers the band 1..N in one transaction. Validation: slugs must belong to the project and the named priority and cover every non-done feature of the band — partial lists rejected with clear 400s. Last-writer-wins accepted (single-user tool).
  - MCP `planning_move_feature(projectId, slug, move, position?)` — the agent gesture, five moves (user decision 2026-09-25: "move 1 Up, move 1 down, make 1st, make last, toPosition"): `"up"` / `"down"` swap with the adjacent non-done feature in the band (no-op success at band edges); `"first"` / `"last"` jump to the band's head/tail; `"toPosition"` moves to the absolute 1-based spot given in `position`. `position` is required iff move=`"toPosition"` — otherwise 400 naming the contract. The server resolves neighbors and renumbers; the agent needs no band-wide knowledge (works even when planning_task_queue was called with a limit).
  - MCP `planning_set_feature_priority(projectId, slug, priority)` — thin wrapper over the existing human-door priority PUT (034 landed the endpoint, 034 task-01 explicitly deferred the agent door as "non-goal v1"). Moving between bands is the weightier act: the tool description requires explicit user approval in-session. Reposition within the new band = tail auto-assign (same rule as create).
- MCP `planning_task_queue(projectId, limit?)` — ReadOnly, Idempotent: returns non-done features sorted `priority asc → queuePosition asc` (the board order). Entries compact per 037: `{slug, number, title, priority, status, queuePosition, specStatus, planStatus, taskDone, taskTotal, readiness, blockedBy, isBlocked, isCurrent}`. `number` = leading digits of slug (null if none). `blockedBy` = direct blocker slugs, empty array when none (never absent). `limit`: optional positive int, default full queue.
- Readiness = ordinal 0–3 (user decision 2026-09-25): spec validated +1, plan validated +1, taskTotal > 0 +1. Decorative in the response — does NOT affect ordering; human drag and agent moves are the only order authorities.
- Blocked entries stay exactly where the humans/agent put them — flagged with isBlocked + blocker slugs, never auto-bumped. Enforcement remains 030's job (current_task STOP, pin refusal); the queue tool carries the information, not the correction. The current feature is flagged, not force-sorted first — if it should be next, move it.
- Drag implementation: native HTML5 drag and drop (draggable + dragover/drop handlers) — no new npm packages; optimistic reorder with snapshot rollback, mirroring the 022/038 interaction vocabulary.
- queuePosition is DB-only state, like isCurrent: materializer and importer ignore it (not in feature.md frontmatter; re-import must not reset positions).
</decisions>

<scope>
- Core/Infrastructure: QueuePosition on PlanningFeature, additive migration + backfill, tail auto-assign on feature create (both doors + import path), shared band-renumber service (full-list, swap-adjacent, move-to-position), board composite sort priority→position, Infrastructure tests (backfill, tail-assign, renumber primitives, sort).
- Host: queue/reorder endpoints — reorder on the human door (full-band contract), move gesture on the agent door, queue read on the agent door with readiness/blockedBy/current decoration; PlanningContracts DTOs; Api.Tests (reorder validation incl. partial/foreign-band rejections, move edge cases: first-item-up, last-item-down, out-of-range toPosition, done neighbors skipped).
- MCP: planning_task_queue + planning_move_feature + planning_set_feature_priority thin tools, Mcp.Tests.
- Web: native-HTML5 dragndrop reorder within a priority band on the dashboard board (optimistic + rollback), composite sort update, lint/tsc/build gates.
- Docs & ceremony surface (user request 2026-09-25): tool descriptions, onboarding quickstart, bootstrap prompt and web ceremony texts state the mechanic openly — the board order IS the queue, "first on the board = next in the queue" — no hidden mechanics; a dated note lands in the dotNetCrew Overmind project (where the overmind-ceremony skill lives as an entry) so agents on other runtimes pick the ceremony change up.
</scope>

<anti-scope>
- No queue table/sequence entity, no separate queue page — the dashboard board is the surface.
- No MCP full-band reorder tool (move gestures cover the agent; the band-wide door exists for the UI drop only).
- Cross-band drag in the UI; cross-band moves through planning_move_feature (band changes go through priority updates only).
- Readiness/blocked/current affecting order — decorations only.
- Transitive dependency chains in blockedBy; enforcement logic changes (030 stays as-is).
- Materializer/importer frontmatter for queuePosition.
- Cross-project queues; automatic scheduling/assignment; human-door registration of the queue read endpoint (agent door only, web consumes the existing board endpoint).
</anti-scope>

<constraints>
- No new npm packages (native HTML5 DnD); no new NuGet packages.
- Migration additive; backfill deterministic.
- Read-only + idempotent: planning_task_queue; move/reorder/set_priority mutate through the shared service path, each call fully describing the resulting state (replay-safe).
- Compact echo per 037 — no artifact bodies in queue responses.
- Wire camelCase, English-only; PlanningFeatureBoardItem gains queuePosition additively, no renames.
</constraints>

<acceptance-criteria>
- [ ] Migration backfills positions (priority asc → createdAt asc, 1..N per group) on an existing dev database without destructive ops; dotnet build 0 warnings.
- [ ] New feature (API, MCP, import) lands at the tail of its priority group.
- [ ] Human-door reorder accepts the full slug order of one priority band and renumbers it atomically; partial lists, foreign slugs, and cross-band mixes rejected with clear 400s.
- [ ] planning_move_feature: up/down swap with the adjacent non-done feature in the band (clean no-op success at band edges), first/last jump to head/tail, toPosition lands at the 1-based spot; position required iff move=toPosition, otherwise 400 naming the contract; every move pinned in tests.
- [ ] Board endpoint returns queuePosition; MCP planning_task_queue mirrors board order (priority asc → queuePosition asc, done excluded) with readiness 0–3, direct blockedBy slugs, isBlocked/isCurrent flags; limit truncates.
- [ ] planning_set_feature_priority changes priority through the existing PUT path (tail of the new band); description demands in-session user approval.
- [ ] Dashboard: cards drag within a priority band, order persists after reload; failed request rolls back the visual order; no new dependencies in package.json.
- [ ] Re-import of BACKLOG/ does not reset queue positions (DB-only state).
- [ ] The queue mechanic is stated in plain text wherever the ceremony is described: planning_list_features / planning_task_queue descriptions, onboarding quickstart, bootstrap prompt, web ceremony texts; a dated note in the dotNetCrew project records the ceremony update.
- [ ] Full suite green: dotnet test; web lint + tsc + build.
</acceptance-criteria>

<refs>
- Feature body 072 (user request 2026-09-24: "получить task-queue по mcp, а я изменяю"); in-session turns 2026-09-25: "борда = очередь" + drag-within-priority flow, "поменять приоритет (user approved)", "дать MCP двигать таски внутри приоритета", "двинь вверх/вниз — а ты не можешь — грусть".
- 030-feature-dependencies: OpenBlockersAsync / FeatureDependencyEntry.IsClosed / HasOpenBlocker — blocker decoration source; shared-handler precedent.
- 034-feature-priorities (priority data + PUT path; task-01 deferred agent-door priority tool), 038-priority-inline-edit (interaction vocabulary: optimistic + rollback), 022 (status-select rollback precedent), 023-04 (hide-done default).
- 037-mcp-echo-slimming (compact echo), 052-agent-briefing-tools (sibling: queue is mid-session "what next").
- 035-execution-queue — closed superseded by the "priorities are enough" call; this spec deliberately walks that back for ordering WITHIN a priority (documented, not silent).
- Baseline DTO: PlanningFeatureBoardItem (src/Overmind.Host/Api/Contracts/PlanningContracts.cs); MCP surface: src/Overmind.Mcp/OvermindPlanningTools.cs.
</refs>

<log>
- 2026-09-25 — User request in-session 2026-09-25: ceremony texts must state the queue mechanic plainly (board order = queue) and the overmind-ceremony skill update lands as a note in the dotNetCrew project. Scope grows, contract unchanged.
</log>
