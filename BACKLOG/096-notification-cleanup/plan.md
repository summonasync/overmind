---
feature: 096
slug: notification-cleanup
status: validated
created: 2026-09-28
validated: 2026-09-28
---

# Plan 096 — Notification cleanup: delete + bulk actions в колокольчике (per-row ×, mark all read, clear read)

<pipeline>
01 backend-cleanup-endpoints → 02 web-cleanup-ux → 03 live-walk-closeout
Строго последовательно: 02 читает контракт 01, 03 гоняет всё вместе. Параллелить нечего — фича маленькая.
</pipeline>

<tasks>
**01 backend-cleanup-endpoints** (review: light)
- src/Overmind.Infrastructure/Notifications/NotificationsService.cs: DeleteAsync(id), MarkAllReadAsync(), ClearReadAsync() — count-возвраты; bulk через EF ExecuteUpdate/ExecuteDelete (без материализации); per-row по id.
- src/Overmind.Host/Api/NotificationsApi.cs, human-группа: DELETE /notifications/{id:guid} → 204/404; POST /notifications/read-all → {marked}; POST /notifications/clear-read → {deleted}.
- tests/Overmind.Api.Tests/NotificationApiTests.cs: delete+404, read-all только по ReadAt==null, clear-read только по ReadAt!=null, reply⇒read инвариант не ломается (replied сносится clear-read, awaiting-approval выживает).
- Верификация: dotnet test (фильтр Notification), dotnet build.

**02 web-cleanup-ux** (review: light)
- web/src/features/notifications/api.ts: deleteNotification/readAllNotifications/clearReadNotifications (относительные пути как существующие).
- web/src/features/notifications/types.ts: BulkResult.
- web/src/features/notifications/lib/use-notification-mutations.ts: три мутации с optimistic-хирургией кэша (удаление item / патч ReadAt / снос прочитанного хвоста), invalidate на settle; unreadCount двигается мгновенно.
- web/src/widgets/app-shell/ui/notification-center.tsx: хедер — «Mark all read» + «Clear read» (ghost, disabled по unreadCount/наличию прочитанных).
- web/src/widgets/app-shell/ui/notification-row.tsx: тихая × (hover/focus, aria-label, не рендерится при pending reply-форме).
- Верификация: npm run lint && npx tsc --noEmit && npm run build (в web/).

**03 live-walk-closeout** (review: hard)
- Live e2e на изолированном dev-хосте (паттерн 084: scratch cwd DB, exact-PID cleanup): agent-door notify → human delete → read-all (badge 0) → clear-read (пустой центр) → replied-инвариант чек.
- audits/architect-review-YYYY-MM-DD-096.md (light-режим структуры).
- Result-артефакт, validate, фича в review через человеческую дверь.
</tasks>

<risks>
- Optimistic-патч кэша: NotificationsResponse несёт unreadCount — патчить оба поля согласованно, иначе бейдж врёт до invalidate.
- ExecuteDelete/ExecuteUpdate не триггерит SaveChanges-перехватчики — их у нотификаций нет, но проверить конфиг entity на каскады/фильтры.
- Кнопки в хедере центра не должны ломать current layout модалки (020-стиль списка).
</risks>

<log>
- 2026-09-28 [agent] — план разложен после spec-validate (proof eabfb2e8); 3 таски, конвейер последовательный, риски оптимистичного кэша зафиксированы.
- 2026-09-28 [agent] — validated with human proof: notification 5c85abfb-3ae9-4f0b-b782-8f46eb92531e replied 2026-09-28 11:01:48 UTC
- 2026-09-28 [agent] — deviation (2026-09-28, architect WARNING-1 resolution): spec v2 amends clear-read semantics — replied proof rows (ReplyText != null) are excluded from bulk delete (077 proof trail), per-row DELETE is the explicit remover; plan's task-03 scope line «reply → clear-read сносит» is superseded by the amended spec. Fix-round: NotificationsService filter, optimistic clearRead patch, family-wide invalidation, test rewritten — all gates green (dotnet 0e, Notification 23/23, web lint/tsc clean, 27/27).
</log>