---
feature: 051
slug: entries-materialization
status: inProgress
priority: p1
version: 1
created: 2026-09-22
---

# Feature 051 — Entries materialization: notes/decisions/gates/guards на диск (+ tombstone-файлы, не молчаливый снос)

<goal>
Entries (notes/decisions/gates/guards) живут только в БД: `PlanningMaterializer` не трогает их вовсе — на диск уходит только planning-дерево. А decisions/guards — то, что читается каждый заход и что должно лежать в git рядом с кодом: внешний ревьюер, история «почему так решили» (`git log -p` по гейту — полевой аргумент из заметки 02), суб-агенты без MCP-доступа. Материализация entries закрывает эту дыру и доводит гибрид «борда для состояния, файлы для контрактов» до целостности.
</goal>

<requirements>
- Одна запись — один файл: frontmatter (id, type, title, tags, updatedAt) + markdown-тело.
- Расположение в проекте — спечный вопрос: отдельная папка (например `ENTRIES/`) vs соседство с `BACKLOG/` vs настраиваемое; общий механизм для любого проекта, не только нашего.
- Tombstone на диске при удалении записи: не молчаливый снос — маркер-файл или `<removed>`-форма (правило «tombstone rather than delete»). Координация с 029-entry-tombstones: 029 — хозяин tombstone-сущности в БД, 051 — её дисковое отражение; один источник правды о факте удаления, две проекции.
- Clobber-guard (033) распространяется на entries-файлы: локальные правки не затираются молча.
- Ездит на той же пер-проектной стратегии материализации (off/on/archive — контракт 006).
- Open question в спеку: обратный импорт entries-файлов в БД (аналог BacklogImporter для planning) — в первой итерации non-goal, но формат файла должен его не исключать.
</requirements>

<non-goals>
- Planning-дерево — уже материализуется, не трогаем.
- Обратный импорт с диска в БД — первая итерация one-way.
- UI-редактор материализованных entries — существующие механизмы UI работают с БД, как и сейчас.
</non-goals>

<refs>
- 029-entry-tombstones (p1, draft) — ЖЁСТКАЯ координация: сущность томбстоуна её, дисковая проекция — наша; спеки синхронизировать.
- 033-materializer-clobber-guard — расширение зоны действия.
- 006-planning — пер-проектная стратегия материализации, на которой всё ездит.
- FEATURELIST/02-overmind-vs-files.md — git-история решений как субстанция.
- FEATURELIST/08-migration-field-report.md — внешний ревьюер читает контракты в репо, не в борде.
- Полевой контекст: юзер поднял 2026-09-22 («у нас не материализуются наши entries»).
</refs>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
- 2026-10-05 [agent] — Van by claude claimed the feature
</log>
