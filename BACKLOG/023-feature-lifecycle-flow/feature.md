---
feature: 023
slug: feature-lifecycle-flow
status: draft
version: 1
created: 2026-09-19
---

# Feature 023 — Feature lifecycle state machine + board filter persistence

<goal>
Жизненный цикл фичи как честная state-машина (user request 2026-09-19): сейчас все фичи бесконечно висят в draft. Новый флоу: draft (сырая мысль, «надо покумекать») → ready (обдумали, дописали, можно брать в работу — существующий статус, НЕ плодим todo/backlog/sprint) → inProgress (спека/план/таски в работе) → review (все таски done, ждёт валидацию юзера — НОВЫЙ статус) → done (юзер подтвердил). Плюс два фильтр-улучшения доски: hide done (дефолт ON) и персистентность выбранных фильтров на сессию между дашбордами проектов.
</goal>

<requirements>
- idea stub: спека не начата; user flow discussion 2026-09-19 зафиксирован в этом теле
- State machine: draft → ready → inProgress → review → done; review — новый член PlanningFeatureStatus (additive по контракту — проверить, что MCP/API клиенты не ломаются от нового значения)
- Авто-переходы (событийные, в Core-сервисе, обе двери API+MCP): добавление spec/plan/task к draft/ready → inProgress; все таски done → review; done ставит ТОЛЬКО человек (UI) или явная команда — никогда авто
- Ручные переходы через dropdown (связка с 022-feature-status-dropdown): разрешённые переходы + откат назад (review → inProgress при новой работе)
- Коллизия семантики ready: сегодня ready существует и, возможно, нигде не используется — на спеке проверить текущие данные/поведение и зафиксировать новую семантику (tombstone в CONTEXT.md)
- UI доски: степпер/бейдж статуса на карточке (связка с 019-ceremony-stepper — один визуальный язык)
- Фильтр hide done: тоггл как current-only, дефолт ON при открытии дашборда
- Персистентность фильтров: выбор (status/spec/plan/current/hide-done) сохраняется минимум на сессию и применяется при переключении проектов (sessionStorage vs localStorage — на спеку; сейчас фильтры живут в URL + state после 010/06)
</requirements>

<non-goals>
- Workflow-автоматизация сверх перечисленного (дэдлайны, assignee, sla)
- Переименование/удаление существующих статусов из контракта (additive-only)
</non-goals>