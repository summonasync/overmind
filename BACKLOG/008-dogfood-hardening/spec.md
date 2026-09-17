---
feature: 008
slug: dogfood-hardening
status: validated
created: 2026-09-16
validated: 2026-09-16
---

# Spec 008 — Dogfood hardening: importer gaps + web UX from 0.1.0

<decisions>
- IMP-1: BacklogImportCommand печатает report.Skipped построчно (INF) после summary-строки. Модель отчёта не меняется.
- IMP-2: парсинг frontmatter-значений срезает инлайн-комментарий (пробел + # и далее) и тримит: «approved        # draft | approved» → «approved»; значение без комментария не меняется.
- IMP-3: MapSpecOrPlanStatus мапит approved → Validated (исторически approved = FROZEN). Только spec/plan. Пункт про легаси-статусы уходит из declared losses в CONTEXT.md.
- IMP-4: импортёр читает result.md как артефакт kind=result (order 0); маппинг статусов по словарю kind'а. Declared losses в CONTEXT.md обновляются.
- IMP-5 (подтверждено пользователем 2026-09-17): TryParseTaskOrder принимает NN + одну букву (01b); базовый order = NN, при коллизии файл уходит в конец (max+1 по возрастанию буквы) с loud-записью в отчёт.
- UI-3: сайдбар визуально отделяется от фона (тень/сепаратор по токенам brand-book).
- UI-4 (решение пользователя 2026-09-17): markdown-превью раскрашивает церемониальные XML-секции: имя тега (открывающий и закрывающий — один цвет) и содержимое секции в цвете секции. Палитра: goal — зелёный, requirements — жёлтый, non-goals — слегка алый, остальные секции — по токенам brand-book в том же духе. HTML-комментарии скрываются целиком.
- PERF-1: EntriesApi.ListByProject — AsSplitQuery на запросе с двумя коллекциями (Tags + SkillFiles).
- Всё едет одним релизом 0.1.1: pack --version 0.1.1 --bundle-version 2 + живой догфуд upgrade-sync на боевом профиле.
</decisions>

<open-questions>
- нет
</open-questions>

<requirements>
- Изменения импортёра покрыты тестами в Overmind.Infrastructure.Tests; CLI-вывод — Host-тестами
- CLI skipped-лист: одна строка = одна запись отчёта, префиксы skipped/ignored/unknown сохраняются
- UI-правки без новых зависимостей, по токенам brand-book; lint+tsc чистые
- Цвета секций в превью — через токены/семантические классы, не хардкод hex в разметке
- PERF-1: ворнинг MultipleCollectionInclude не появляется в логе хоста на GET /entries
- CONTEXT.md обновляется в том же изменении (importer losses, поведение парсера)
</requirements>