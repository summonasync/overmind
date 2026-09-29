---
feature: 096
slug: notification-cleanup
status: done
priority: p1
version: 2
created: 2026-09-28
---

# Feature 096 — Notification cleanup: delete + bulk actions в колокольчике (per-row ×, mark all read, clear read)

## Goal
Колокольчик чистится руками: человек удаляет нотификации по одной и пачками, notification center перестаёт быть свалкой.

## Why
Нотификации копятся вечно — list cap 50 прячет хвост, но ничего не удаляется с 020. Жмёшь колокольчик — там пиздец: стены прочитанных info-строк. Критическая боль юзера, 2026-09-28.

## Requirements
- Per-row delete (×) в центре — hard delete одной нотификации.
- Bulk: «mark all read» (все непрочитанные проекта → read) и «clear read» (удалить все прочитанные проекта).
- Оптимистичные обновления кэша, unreadCount и список перерисовываются сразу.
- Всё на human door (HTTP + web UI). MCP agent door не трогаем.

## Non-goals
- Серверный retention/TTL-автоснос (отдельный разговор, рядом с 095-os-desktop-notifications).
- Agent-door удаление через MCP.
- Архив/корзина вместо delete, восстановление удалённых.
- Фильтры и поиск в центре — territory 085/087.

<log>
- 2026-09-28 [auto] — artifact created → inProgress
- 2026-09-28 [auto] — all tasks done + spec + plan validated → review
- 2026-09-29 [ui] — status moved to 'done' via ui
</log>
