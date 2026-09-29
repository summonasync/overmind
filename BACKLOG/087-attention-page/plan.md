---
feature: 087
slug: attention-page
status: validated
created: 2026-09-29
validated: 2026-09-29
---

# Plan 087 — Attention-страница в левом меню: все вопросы и attention агентов в одном месте (не только колокольчик)

## Shape

3 таски, линейный конвейер: 01 backend → 02 web → 03 closeout. Backend первым: веб-кодер строит контракт по живым эндпоинтам. Параллельность не даём — обе backend-подзадности писали бы Program.cs.

## Decisions

- D1: awaiting и ceremonies — ОДИН backend-таск: общий concern «read-поверхность attention», Program.cs в одних руках.
- D2: ceremonies-причины считаются на сервере из существующих полей (PlanningArtifact.Status + feature.Status + незакрытые PlanningFeatureDependency) — ноль миграций, ноль новых колонок.
- D3: take-параметр списка уведомлений — опциональный, дефолт 50 (все существующие вызовы не меняются); страница шлёт 200. #140 закрывается этим + awaiting-фильтром.
- D4: веб-перенос: notification-center разбирается — история едет на /attention, bell получает компактный unread-поповер. notification-row переезжает с sr-only «Archived»-фиксом (SUGGESTION 097-02).
- D5: веб-запросы ceremonies/awaiting — через существующий family: невалидация notificationKeys.all после reply/действий покрывает и awaiting-зону; ceremonies — свой ключ, refetch вместе с борда-мутациями не нужен (поллинг списка достаточен).

## Risks

- R1: ceremonies-джойн на большой борде (69 done-фич × проекты) — запрос должен фильтровать в SQL (feature.Status != done, artifacts draft), не в памяти; пин на объём не нужен, проверкаexplain-планом в AC кодера.
- R2: bell-бейдж и unreadCount — единый источник (существующий endpoint); не плодить второй счётчик.
- R3: deep-link строк ceremonies — формат 093 (?project=&feature=); реюз NotificationLinks-хелпера, не третья копия форматера.

<log>
- 2026-09-29 [agent] — plan drafted after spec validation (proof 90e018d2): 3-task linear pipeline per 096 shape, backend combined per D1.
- 2026-09-29 [agent] — validated with human proof: notification a5a8ff5e-5aef-444c-8183-283e25940abc replied 2026-09-29 14:21:26 UTC
</log>