---
feature: 010
slug: planning-board-filters
status: draft
version: 1
created: 2026-09-16
---

# Feature 010 — Planning board: filters (tags/status/state) + current feature highlight

<goal>
Planning board: фильтры и фокус. Фильтр по статусу фичи (draft / ready / in-progress / done), фильтр по «тэгам»-чипам артефактов на борде (spec: validated, plan: validated и т.д.) — и новый статус CURRENT: пометка фичи, над которой прямо сейчас идёт работа. Запрос пользователя из догфуда 0.1.0 (2026-09-17, в тот же день уточнено: «тэги» = существующие чипы статусов spec/plan, НЕ новая теговая система).
</goal>

<requirements>
- idea stub: обсуждается, спека не начата
- фильтр борда по статусу фичи (существующий PlanningFeatureStatus)
- фильтр борда по статусам spec/plan (данные уже есть в board-ответе: specStatus/planStatus)
- новый статус CURRENT. Дизайн-вопросы для спеки: отдельное значение статуса vs флаг-пин vs derived (фича с in-progress таской); одна current на проект или несколько; как current виден агенту (planning_current_task уже умеет «current» внутри фичи — связать семантику)
</requirements>

<non-goals>
- Теговая система на фичах (первичная формулировка понята неверно — не нужна)
- Редизайн навигации к табам проекта (UI-2) — отдельная фича
</non-goals>