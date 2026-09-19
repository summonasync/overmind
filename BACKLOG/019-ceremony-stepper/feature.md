---
feature: 019
slug: ceremony-stepper
status: draft
version: 1
created: 2026-09-19
---

# Feature 019 — Ceremony stepper on feature page (feature → spec → plan → tasks x/y → result)

<goal>
Степпер этапа церемонии на странице фичи (user request 2026-09-19): сверху feature workspace видно, на каком мы этапе флоу — feature → spec → plan → tasks (x/y) → result — без скролла и переключения табов. Один взгляд = где мы и что следующее.
</goal>

<requirements>
- idea stub: обсуждается, спека не начата
- Визуальный stepper/progress над контентом воркспейса: этапы feature, spec, plan, tasks, result; текущий этап подсвечен, пройденные отмечены, будущие приглушены
- tasks-этап со счётчиком x/y (done/total)
- Клик по этапу ведёт на соответствующий таб (spec/plan/tasks/result) — не просто индикатор, а навигация
- Статусы берутся из уже загруженных данных воркспейса (artifact statuses), без новых эндпоинтов, если хватает
- Согласовать с 017 (live sync): степпер должен сам обновляться при мутациях через MCP
</requirements>

<non-goals>
- Изменение самой state-машины церемонии (статусы/переходы не трогаем, только визуализация)
</non-goals>