---
feature: 096
task: 02
slug: notification-cleanup
status: done
review: light
created: 2026-09-28
depends_on: 01
brief: Web: api/mutations + optimistic-кэш, кнопки «Mark all read»/«Clear read», × на строке
---

# Task 02 — Web cleanup UX

<goal>
Колокольчик чистится из UI: × на строке, «Mark all read» и «Clear read» в хедере центра — с оптимистичным кэшем и честным бейджем.
</goal>

<scope>
- web/src/features/notifications/api.ts: deleteNotification(id) / readAllNotifications() / clearReadNotifications() — относительные пути как существующие, DELETE-хелпер при отсутствии в shared/api.
- web/src/features/notifications/types.ts: BulkResult ({ marked } / { deleted } по эндпоинтам).
- web/src/features/notifications/lib/use-notification-mutations.ts: три мутации с optimistic-хирургией NotificationsResponse (удаление item / патч ReadAt+unreadCount / снос прочитанного хвоста), invalidate на settle. Патчить item и unreadCount СОГЛАСОВАННО (риск из плана).
- web/src/widgets/app-shell/ui/notification-center.tsx: хедер — «Mark all read» (disabled при unreadCount=0), «Clear read» (disabled когда в загруженном списке нет прочитанных). Ghost/quiet стили существующей системы.
- web/src/widgets/app-shell/ui/notification-row.tsx: тихая × (hover/focus, aria-label «Delete notification», скрыта пока reply-форма строки pending).
- One-click без confirm-модалок — осознанное решение спеки.
</scope>

<anti-scope>
- Фильтры/поиск центра (085/087), тосты (микробатч #13), бэкенд (таска 01).
</anti-scope>

<constraints>
- FSD boundaries features/notifications + widgets/app-shell; React Query v5, Server-Action-free (этот слайс ходит через api-хелперы как раньше); TypeScript strict; English-only; static-export правила (без next/link в новых местах).
</constraints>

<acceptance-criteria>
- [x] × удаляет строку без перезагрузки, бейдж не врёт.
- [x] «Mark all read» гасит все непрочитанные, бейдж → 0 мгновенно (optimistic).
- [x] «Clear read» сносит прочитанный хвост, disabled-состояния корректны в обоих крайних случаях.
- [x] npm run lint && npx tsc --noEmit && npm run build зелёные (в web/).
</acceptance-criteria>

<refs>
- feature 096 spec (4d1ab184) — decisions (UI placement, optimistic).
- web/src/features/notifications/lib/use-notification-mutations.ts — существующие паттерны reply-оптимистики (084).
- plan a41a4350 — risk-блок.
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-28 [agent] — task cut from validated plan a41a4350; blockedBy task 01 (нужны эндпоинты).
- 2026-09-28 [agent] — coder took task 02
- 2026-09-28 [agent] — coder: verified backend unreadCount = COUNT(ReadAt == null) — delete of an unread item decrements it, mirrored in optimistic patch (delete/read-all/clear-read share one patchList/restoreList/settleList helper in use-notification-mutations.ts). Gates: lint 0 errors (6 pre-existing warnings in unrelated files), tsc clean, static build ok, web tests 27/27.
- 2026-09-28 [agent] — coder done, lint+tsc+build green, AC ticked
- 2026-09-28 [agent] — review PASS-WITH-NITS (audits/review-096-02.md, 0 blockers/warnings, 2 no-action NITs): gates re-run independently (lint 0e/tsc clean/27 tests/build green), optimistic badge math verified against backend contract, 084 reply semantics byte-identical after helper refactor. Closing done.
</log>