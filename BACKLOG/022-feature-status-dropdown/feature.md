---
feature: 022
slug: feature-status-dropdown
status: done
priority: p2
version: 1
created: 2026-09-19
---

# Feature 022 — Feature status dropdown on the dashboard card

<goal>
Смена статуса фичи прямо на дашборде (user request 2026-09-19): выпадающий список статуса (draft/ready/inProgress/done) на карточке фичи — без захода в edit-окно. Минорная UI-фича.
</goal>

<requirements>
- idea stub: обсуждается, спека не начата
- Dropdown статуса на карточке фичи на доске (рядом с пином current), мгновенное применение через существующую human door (PUT features/{slug} с status)
- Оптимистичное обновление + инвалидация доски (React Query); ошибка → откат + тост
- Валидации/ограничения переходов (если есть на бэке) — уважать, показывать ошибку
- Учесть 017 (live sync): чужие смены статуса тоже должны подтягиваться
</requirements>

<non-goals>
- Изменение state-машины статусов на бэкенде
- Bulk-операции над фичами
</non-goals>