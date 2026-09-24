---
feature: 044
slug: batch-planning-ops
status: inProgress
priority: p1
version: 2
created: 2026-09-22
---

# Feature 044 — Batch planning ops: создать N артефактов / двинуть N статусов одним вызовом

<goal>
Массовые операции церемонии одним вызовом. Полевой замер миграции: 35 задач = 35 `planning_upsert_artifact` + 35 `planning_set_status` = 70 round-trips, при том что файловый эквивалент — один Bash-вызов со скриптом по 35 файлам. Round-trips — это не только латентность, но и промпт-оверхед каждого вызова; порядок величины разницы.
</goal>

<requirements>
- Создать N артефактов одной фичи одним вызовом (order проставляется по списку).
- Двинуть N статусов одним вызовом (logNote один на батч или по элементу — решить в спеке).
- Эхо — сводка по батчу в контракте 037 (id/kind/status/version), без тел.
- Поведение при частичной неудаче определено явно: атомарный роллбэк либо отчёт по элементам с причинами — выбрать в спеке, но не молчать.
</requirements>

<non-goals>
- Патч-операции над телами — 039; батч из патчей — отдельная тема после него.
- Батч для `write_entry` — полевой фидбек просил только артефакты+статусы; расширить по спросу.
</non-goals>

<refs>
- FEATURELIST/08-migration-field-report.md — главный документ: 35+35 вызовов, «order-of-magnitude difference in round trips».
- README FEATURELIST п.5.
</refs>

<log>
- 2026-09-23 [auto] — artifact created → inProgress
</log>
