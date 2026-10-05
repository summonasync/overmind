---
feature: 051
slug: entries-materialization
status: validated
created: 2026-10-05
validated: 2026-10-05
---

# Result 051 — Entries materialization: notes/decisions/gates/guards на диск (+ tombstone-файлы, не молчаливый снос)

Волна закрыта: записи проецируются на диск той же трубой, что и planning-дерево — `ENTRIES/{type}/{kebab-title}-{id8}.md` (сиблинг BACKLOG/), один файл — одна запись, strategy-respecting, tombstone-конверсия 029 садится в файл естественно. 3/3 таска; архитектор light APPROVED-WITH-WARNINGS — все три W закрыты до result'а.

## Что село

- **01 — write/remove пути** (919fab4): entry-планы аппендятся в общий plans-список SyncAsync ДО захвата plannedPaths — гварды 033, remove-пасс, атомарная запись, 074-отчёт и async-очередь 104 переиспользованы без единой правки (~55 строк проекционного кода). Файлы трекаются в PlanningMaterializedFile (FeatureId null), hand-edit → refusal, CRLF нормализуется через ContentHash, id8-коллизия падает fail-closed (DuplicateTarget), stale-row сносит только hash-verified файлы, on↔archive мигрирует existing other-root танцем.
- **02 — гварды + наблюдаемость + санитайз** (890ef54 + фикс 05f6cc2): гварды 033 над ENTRIES/ запинены на реальных путях (refusal с OffendingPath, force per-file warnings с байт-восстановлением, foreign-file adopt), skipped/refused линии ездят в 074-поверхности. Санитизация на ПЛАН-уровне — EntryDocument сплющивает переносы в title/tags (проекция цела из любых хранимых данных; сильнее, чем заявлял кодер — KitImporter пишет записи напрямую, валидаторы их не видят). Запятые в тегах — отказ на валидаторах (устоявшийся 400-путь). Архив и rename-in-place запинены.
- **03 — клоун** (f972ead + фикс 53f1a9d): доки (STRUCTURE.md layout + CONTEXT.md параграф семантики), архитектор light **APPROVED-WITH-WARNINGS** (audits/architect-051.md): W1 — мои дока-деревья клали ENTRIES внутрь BACKLOG (код — сиблинг, и это load-bearing: импортер сканирует BACKLOG-папки фич) — исправлено; W2 — null-гвард прилетела только в create-валидатор (update NRE-500 на Tags [null]) — исправлено с пином; W3 — честность: entry CRUD не энквейлит 104-пасс, зеркало догоняет записи после следующей planning-мутации или ручного materialize — теперь сказано прямо в CONTEXT.md.

## Гейты финального дерева

dotnet build 0 warnings; последовательно: **Infra 650 (+11), Api 635 (+4), Mcp 459, format clean**; Core не тронут (345).

## Post-release click-check

1. Создать entry → следующий пасс пишет `ENTRIES/{type}/{kebab}-{id8}.md` с frontmatter и дословным телом.
2. Hand-edit трекнутого ENTRIES-файла → пасс отказывает (409, OffendingPath в отчёте); force → per-file warning и байты борды восстанавливаются.
3. Удалить запись (029-конверсия) → старый файл снесён, `ENTRIES/tombstone/…` появился за один пасс.
4. Удалить могилу → файл снесён на следующем пассе, второй могилы нет.
5. Strategy off → ClearAsync сносит entry-файлы вместе с деревом; чужие файлы под ENTRIES/ выживают.

## Остаточное

- Микробатч: arch-N1 (TargetDir не отражает ENTRIES), arch-N2 (тег-инварианты переехать в Tag.Create при появлении MCP entries-тулов), N1 (ownership-row пин), #219/#220 перенесены из 029.
- **К юзеру**: ADR «рецепт третьей проекции» в TECHNICAL-SOLUTIONS.md (вместе с 105-ADR) — по домашнему правилу только с аппрува.
- Осознанные: skill-дублирование ENTRIES/skill/ + SkillFile (кит = истина по skill-файлам), colon/quote рендер без кавычек (реверс-импорт потребует диалекта), entry CRUD не пушит пасс (документировано в CONTEXT.md).

Коммиты волны: 919fab4, 890ef54, 05f6cc2, f972ead, 53f1a9d.

<log>
- 2026-10-05 [agent] — Result drafted: 3/3 tasks done (01 PASS-WITH-NITS, 02 PASS-WITH-NITS + W1 null-tag fix, 03 closeout with architect APPROVED-WITH-WARNINGS — W1/W2/W3 fixed in 53f1a9d). Gates: Infra 650, Api 635, Mcp 459, build 0w. Post-release click-check inside. Waiting for the human tap.
</log>