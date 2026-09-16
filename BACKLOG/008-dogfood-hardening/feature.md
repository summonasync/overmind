---
feature: 008
slug: dogfood-hardening
status: draft
version: 1
created: 2026-09-16
---

# Feature 008 — Dogfood hardening: importer gaps + web UX from 0.1.0

<goal>
Свести хвост находок первого живого догфуда 0.1.0: importer gaps, web-UX нитки и перф-ворнинг, всплывшие при флипе планировки этого репо в БД Overmind. Результат — релиз 0.1.1, где догфуд-контур (pack, install, seed, import, materialize, MCP-церемония) не имеет известных слепых пятен.
</goal>

<requirements>
- CLI --import-backlog печатает построчный список skipped/ignored, а не только счётчик (IMP-1)
- frontmatter-парсер импортёра срезает инлайн-комментарии в значениях (IMP-2)
- легаси status: approved мапится в validated для spec/plan, а не в draft (IMP-3)
- импортёр читает result.md — round-trip с материализатором замкнут (IMP-4)
- таски с буквенным суффиксом (01b) не теряются молча; поведение определено в спеке (IMP-5)
- sidebar веб-UI визуально отделён от фона (UI-3)
- markdown-превью не рендерит церемониальные XML-теги и HTML-комменты как голый текст (UI-4)
- устранён EF MultipleCollectionIncludeWarning на выдаче entries (PERF-1)
- выпущен релиз 0.1.1 с бампом bundle-version и живым догфудом upgrade-sync
</requirements>

<non-goals>
- Редизайн навигации к табам проекта (UI-2) — отдельная фича, сюда не входит
- CLI-регистрация MCP в конфиги агентов — фича 009 (idea stub)
- Изменение модели данных планировки; правки только в importer / CLI / web UI
- Новые зависимости NuGet/npm
</non-goals>