---
feature: 097
task: 01
slug: clear-conversations
status: done
review: light
created: 2026-09-29
brief: Backend: HiddenAt + migration + bulk-hide + list/unreadCount filter + includeHidden + clear-conversations endpoint + Api pins
---

# 097/01 — Backend: hide conversations (HiddenAt + bulk-hide + list filter + includeHidden)

<goal>
Replied notifications get a HiddenAt stamp instead of deletion: "Clear conversations" bulk-hides replied rows, the default list/unreadCount exclude hidden rows, ?includeHidden=true reveals them for the archive view. Agent replies endpoint and the 077 proof search stay unconditionally unfiltered.
</goal>

<context>
Spec ded04f10 v2 (validated), plan 7a436aba — task 1 of 2, before the web task. Current surfaces: Core AgentNotification (Hide-style method convention like MarkRead/Reply), NotificationsService (GetOverviewAsync has list+unreadCount pair, ListCapacity=50 Take at the end; ClearReadAsync is the ExecuteDelete precedent; MarkAllReadAsync is the ExecuteUpdate precedent), NotificationsApi (human door global, route-constraint guard for read-all/clear-read vs {id:guid}), NotificationContracts (ClearReadNotificationsResponse precedent). Hidden rows are read rows (084 reply⇒read) — the unreadCount interplay is trivial but pinned.
</context>

<scope>
- src/Overmind.Core: AgentNotification.HiddenAt (nullable DateTime) + entity method Hide() (no Restore in v1); EF configuration if the entity has one (follow MarkRead pattern).
- Migration AddNotificationHiddenAt (additive nullable column, no index, no backfill).
- src/Overmind.Infrastructure/Notifications/NotificationsService.cs:
  - ClearConversationsAsync(ct) → int: ExecuteUpdate HiddenAt=UtcNow where ReplyText != null && HiddenAt == null (MarkAllReadAsync is the idiom precedent).
  - GetOverviewAsync: filter HiddenAt == null on BOTH the list query (before OrderBy/Take(50)) and the unreadCount query.
  - New or extended list read for includeHidden=true: same overview shape, no hidden filter. Choose parameter vs separate method per house style; document in obstacles if it reshapes the signature.
  - ListRepliesAsync: UNCHANGED (no hidden filter — pin stays green).
- src/Overmind.Host/Api/NotificationsApi.cs: POST /api/notifications/clear-conversations → 200 { hidden } (route cannot collide with {id:guid}); human GET list gains ?includeHidden flag (default false).
- src/Overmind.Host/Api/Contracts/NotificationContracts.cs: HideConversationsResponse(int Hidden).
- tests/Overmind.Api.Tests/NotificationApiTests.cs — pins: hide only replied-unhidden (unread untouched); idempotent second call → 0; default list drops hidden (raw JSON, count back below 50-window when applicable); includeHidden=true returns them with HiddenAt set; agent replies endpoint still returns hidden rows; clear-read/clear-conversations independence (a replied row is not deletable by clear-read — 096 pin already covers, keep green).
- Verify: dotnet build + dotnet test (full solution — the 077 suite must stay green untouched).
</scope>

<anti-scope>
- Web (task 02), MCP adapter, ListRepliesAsync filter, 077 proof service, entity read-state changes, Restore/unhide, optimistic anything.
</anti-scope>

<constraints>
- House conventions: static Map<>, method-group handlers, HttpErrors.*, records in Contracts/, ExecuteUpdate/ExecuteDelete idioms already in the service. No comments. English-only. No new packages. Migration naming/shape per existing AddAgentNotifications precedent.
</constraints>

<acceptance-criteria>
- [x] HiddenAt + Hide() on the entity; additive migration compiles and applies (test factory green).
- [x] ClearConversationsAsync hides only replied-unhidden; idempotent.
- [x] Default list + unreadCount exclude hidden BEFORE Take(50); includeHidden=true reveals with HiddenAt set.
- [x] Agent replies endpoint returns hidden rows (pin).
- [x] POST clear-conversations → { hidden } wired; full solution tests green (077 suite untouched-green).
- [x] Work logged via planning_append_log; obstacles patched for any signature reshapes.
</acceptance-criteria>

<refs>
- Spec ded04f10 (validated, v2 hide-model); plan 7a436aba.
- 096 bulk endpoints + response records (conventions); 084 reply⇒read; 020 replies contract.
</refs>

<obstacles>
<obstacles>
- GetOverviewAsync signature: CancellationToken-only -> (bool includeHidden, CancellationToken ct). Chose a bool param over a separate archive method because ListRepliesAsync already carries its bool includeRead on the service with the default applied at the endpoint (bool includeHidden = false on NotificationsApi.List) — param-on-service is the house shape and the single caller (NotificationsApi.List) was updated. No separate method needed.
- NotificationResponse contract: added trailing DateTime? HiddenAt (required by the includeHidden pin "reveals with HiddenAt set"); NotificationMapper.ToResponse passes it through. AgentNotificationResponse (agent create door) deliberately left without HiddenAt — a fresh create row is always unhidden. Replies endpoint shares NotificationResponse, so hidden rows there now expose their HiddenAt too — harmless additive JSON, web consumers untouched (task 02 scope).
- Migration file rename: dotnet-ef emitted 20260929110515_; renamed to 20260929000000_ and synced the [Migration] attribute to match the hand-set-timestamp convention of the latest migrations (20260925100000_, 20260927000000_). No db had applied the pre-rename id.
- dotnet format flags IDE0161 (block-scoped namespace) on the generated migration; all existing migration files share that dotnet-ef shape, kept as-is for consistency.
</obstacles>
</obstacles>

<log>
- 2026-09-29 [agent] — task sliced from validated plan.
- 2026-09-29 [agent] — coder picked up task 01
- 2026-09-29 [agent] — coder done: AgentNotification.HiddenAt + Hide(); migration 20260929000000_AddNotificationHiddenAt (additive nullable TEXT column, no index/backfill, generated via dotnet-ef per AGENTS.md, timestamp renamed to house hand-set convention with [Migration] attribute synced); NotificationsService.ClearConversationsAsync (ExecuteUpdate HiddenAt=UtcNow where ReplyText != null && HiddenAt == null, MarkAllReadAsync idiom); GetOverviewAsync filters HiddenAt == null before OrderBy/Take(50) and on unreadCount; includeHidden read path via bool param (obstacles); POST /api/notifications/clear-conversations -> { hidden }; ?includeHidden on human GET list; NotificationResponse + mapper carry HiddenAt; ListRepliesAsync byte-untouched. Evidence: dotnet build 0 errors / 0 warnings; full dotnet test 1649 passed, 0 failed (Core 225, Infrastructure 535 incl. 077 suite, Api 501, Mcp 388); dotnet format --verify-no-changes clean on hand-written files. One test assertion fixed during the run (read-not-replied row stays visible in the default list — only hidden rows drop).
- 2026-09-29 [agent] — coder done, moving to review: 6/6 ACs ticked, build 0/0, full solution 1649/0 green (077 suite untouched-green), migration 20260929000000_AddNotificationHiddenAt, includeHidden shape documented in obstacles.
- 2026-09-29 [agent] — light review PASS-WITH-NITS (audits/review-097-01.md): 3 NIT в микробатч (#137 Hide() без вызовов — вердикт после 02; #138 снапшот-канонизация + типизированный пин); reviewer перепроверил build 0/0 + 1649/1649
</log>