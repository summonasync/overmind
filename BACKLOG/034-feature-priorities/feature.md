---
feature: 034
slug: feature-priorities
status: done
priority: p1
version: 3
created: 2026-09-20
---

# Feature 034 — Feature priorities: прио как данные (поле + сортировка/фильтр борды + MCP + round-trip)

<goal>
У фич появляется приоритет как данные: поле на сущности (enum P0-P3 или ordered int — решить на спеке), борда сортируется и фильтруется по нему, MCP-тулзы принимают/отдают приоритет, materializer пишет в feature.md frontmatter (round-trip через importer). Оркестратор и юзер видят очередь по прио без ручных таблиц в чате.
</goal>

<context>
Юзер 2026-09-20: «давай добавим фичу на будущее — добавить прио на фичи». Живая боль: приоритизация живёт в чате/памяти оркестратора (эта сессия — таблица «по прио» руками); борда знает только статусы, очередь не выражена. Класс задачи тот же, что у 030 (структурные метаданные фичи вместо текста в body).
</context>

<scope-sketch>
- PlanningFeature: Priority (P0-Critical … P3-Low или int с drag-order — open Q; enum проще и церемониальнее, int гибче для ручной сортировки).
- API/MCP: приоритет в create/update feature + человеческая и агентская двери; board API отдаёт отсортированно.
- Веб: чип прио на карточке + сортировка по умолчанию (прио, потом updatedAt), фильтр по прио.
- Materializer/importer: frontmatter `priority:` round-trip.
- Взаимодействие с current-pin: pin = «работаем сейчас», прио = «порядок очереди» — не conflat'ить.
</scope-sketch>

<open-questions>
- Enum P0-P3 vs int drag-order (юзер на спеке).
- Дефолт для существующих фич (P2/без прио в конце?).
- Влияет ли прио на планировку current-task (нет — только сортировка/подсветка).
</open-questions>

<status>
Draft; спека короткая. Родственная фича: 030-feature-dependencies (тот же класс — метаданные фичи как данные).
</status>

<log>
- 2026-09-20 — Delegated close by orchestrator: user said fix results in-session (032/033 already flipped by user in web). All tasks done+reviewed, result validated, live dogfood on prod (bundle 4/5).
</log>
