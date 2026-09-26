---
feature: 085
slug: board-search-filters-layout
status: draft
priority: p1
version: 1
created: 2026-09-26
---

# Feature 085 — Board UI: двухрядный тулбар (фильтры / поиск+fast buttons), fast-фильтры current/hide done/blocked + фильтр require attention

<goal>
Board toolbar gets a two-row layout and intent fast-buttons, plus a "require attention" filter that surfaces features where the user's call is needed. Built on top of the existing client-side quick search (064) and board filters (010).

<requirements>
- Layout (user sketch, 2026-09-26): row 1 = filters (existing tag/status/state/priority set), row 2 = search input + fast buttons. The search moves to the second row, under the filters.
- Fast buttons (toggle chips): current (pinned feature), hide done, blocked.
- New filter "require attention": features where the USER's action is needed. Candidate semantics to refine in the spec (Socratic): feature in review (final flip awaits), spec/plan awaiting validation on gated projects, unclosed blockers where this feature is the blocker, unanswered approval notifications targeting the feature (077 integration). Exact predicate decided in spec with the user.
- Filter state persistence consistent with 010 behavior.
</requirements>

<non-goals>
- Server-side filtering (client-side stays); kanban board rework; mobile.
</non-goals>

<refs>
- 064-board-quick-search (done): search row, Enter/Esc/Shift+Esc contract.
- 010-planning-board-filters (done): filters + persistence; 072 (queue/priority band rendering); 077 (approval = attention signal source).
</refs>