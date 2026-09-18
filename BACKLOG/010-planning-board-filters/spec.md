---
feature: 010
slug: planning-board-filters
status: validated
created: 2026-09-18
validated: 2026-09-18
---

# Spec 010 — Planning board: filters (tags/status/state) + current feature highlight

<decisions>
- CURRENT is a pin flag, not a status: `PlanningFeature.IsCurrent` (bool, default false), orthogonal to Draft/Ready/InProgress/Done. A current feature is usually also InProgress — a separate enum value would destroy pipeline visibility (user decision 2026-09-18).
- One current feature per project: setting a new current atomically clears the previous one. Matches the original ask — "the one we are working on" (user decision 2026-09-18).
- Board filtering is client-side. The board endpoint already returns every feature of the project in one response with specStatus/planStatus; data volume is tiny, zero API change needed for filtering (user decision 2026-09-18).
- Agents can set current: new MCP tool `planning_set_current` + `isCurrent` on the board item DTO (additive-only contract, no renames/removals) (user decision 2026-09-18).
- "Tags" in the original request meant the EXISTING spec/plan status chips on the board — no new tagging system on features (user clarification 2026-09-17).
- Filter state lives in URL search params (shareable/bookmarkable, survives reload), not bare useState.
- Feature-level current and task-level current (`planning_current_task`) are independent levels: the flag does not auto-change feature status, and starting a task does not auto-pin the feature. Deliberate pins only.
</decisions>

<open-questions>
- none
</open-questions>

<requirements>
Backend:
- `PlanningFeature.IsCurrent` (bool, default false) + EF migration.
- Single-current invariant per project enforced in the Core service: `SetCurrentAsync(projectId, slug, isCurrent)` clears any other current feature of the same project in the same transaction. Not just in the API layer — the MCP path must hit the same guard.
- API: endpoint to set/unset current (additive; route shape decided in plan), board item DTO gains `isCurrent`.
- MCP: new tool `planning_set_current { projectId, slug, isCurrent }`; `planning_list_features` response gains `isCurrent` per feature (additive).

Web (planning board):
- Filter bar above the board grid: multi-select by feature status (draft/ready/inProgress/done), filter by artifact chips (spec/plan: validated vs not-validated vs none), "current only" toggle.
- Current feature card gets a visual accent (distinct badge + border tone) and sorts first regardless of filter (when visible).
- Set/unset current action on the card (pin icon), next to Edit/Delete.
- Filter state reflected in URL search params; empty-filter behavior = today's board.
- Filters compose (AND); a filter combination with zero matches shows an empty state with a "clear filters" action.
</requirements>

<acceptance-ideas>
- Pin feature A as current via UI → badge+accent on A, sorts first. Pin feature B via MCP → A unpinned, B current (invariant holds across doors).
- Reload page with filters applied → same filtered view (URL params).
- Filter spec=validated + status=inProgress → only matching cards; zero-match shows empty state with reset.
- `dotnet test` green incl. new invariant tests; `npm run lint && npx tsc --noEmit` green.
</acceptance-ideas>