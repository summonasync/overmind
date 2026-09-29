---
feature: 097
slug: clear-conversations
status: validated
created: 2026-09-29
validated: 2026-09-29
---

# Plan 097 — Clear conversations: guarded bulk-delete of replied notifications (077 proof-aware)

## Contract

- Hide-model per validated spec ded04f10 v2: HiddenAt stamp + exclude-from-list + includeHidden archive toggle; replies endpoint and 077 proof search untouched.
- Layers: 01 backend (entity+migration+service+API+tests) → 02 web+closeout (button+toggle+mutation+e2e+result). Sequential: 02's e2e validates the full chain; file sets disjoint.

## Tasks

- [ ] 01-backend-hide-conversations — Core HiddenAt + migration + service bulk-hide + list filter + includeHidden + endpoint + Api pins → tasks/01-backend-hide-conversations.md
- [ ] 02-web-clear-conversations-closeout — button + archive toggle + mutation + live e2e + result → tasks/02-web-clear-conversations-closeout.md

## Risks

- List/unreadCount filter must apply BEFORE Take(50) (hidden rows must not occupy slots) and on BOTH queries in GetOverviewAsync.
- includeHidden=true must keep the response shape identical (items carry HiddenAt already); default raw-JSON pin guards the silent exclusion.
- Migration is additive-nullable — no backfill, no index; SQLite provider happy path.
- The archive toggle is a query-state switch (includeHidden param), not a separate query — keep notificationKeys single-family so invalidation covers both views.

## Verification

- dotnet build + dotnet test green (new pins: hide-only-replied-unhidden, idempotence, unread untouched, default-exclusion, includeHidden, replies-unfiltered; 077 suite stays green).
- Web: lint + tsc + build + tests green.
- Live e2e on isolated host: reply a notification → clear-conversations → { hidden: 1 } → default list excludes it → includeHidden shows it → agent replies endpoint still returns it → second call { hidden: 0 }.

<log>
- 2026-09-29 [agent] — plan drafted after spec validation; 2 tasks, sequential 01→02.
- 2026-09-29 [agent] — validated with human proof: notification 34a239d4-f66b-403b-8fff-395d2fb5dfa4 replied 2026-09-29 10:56:28 UTC
</log>