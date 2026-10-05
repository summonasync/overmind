---
feature: 029
slug: entry-tombstones
status: inProgress
priority: p1
version: 1
created: 2026-09-20
---

# Feature 029 — Entry tombstones: тип записи + автозахват при удалении + список для агентов перед работой

<goal>
Tombstone — новый тип записи (рядом с note/decision/gate/guard): видимая память об удалённом/отменённом/умершем. Любой агент перед работой может получить список tombstone'ов проекта («что уже похоронено и почему») — как читает notes/decisions сегодня. Плюс механика: удаление entry больше не бесследно — delete рождает tombstone.
</goal>

<context>
Задумано юзером 2026-09-20: «у нас есть разные типы entries, но нет типа tombstone — было бы охуенно иметь такой, чтобы любой агент мог получить список томбстоунов до работы». Диагноз кода в той же сессии: версии правок у entries ЕСТЬ (EntryVersion + GET /entries/{id}/versions), но Delete жёсткий (EntriesApi.cs:214-229, Remove + cascade уносит версии), конвенция CLAUDE.md-правила 8 («removal = decision entry») — дисциплинарная, продуктом не держится. Entries в git не материализуются — safety-net нет.
</context>

<sketch>
- EntryType.Tombstone: заголовок = что похоронено, контент = почему/кем/когда (+ ссылка на версии, если сохранены), теги как у всех entries.
- Автозахват: DELETE /entries/{id} вместо жёсткого удаления создаёт tombstone (title исходной + тип + причина опционально); версии либо сохраняются, либо сериализуются в контент — решить на спеке.
- Видимость агентам: list_entries type=tombstone + overmind://resources секция + onboard-хинт «перед работой глянь tombstones» (связь с догфуд-фактами 1-3: модель должна самодокументироваться).
- MCP: write_entry type=tombstone доступен агентам и вручную (осознанное погребение решения/подхода без удаления).
- Веб: тип в фильтрах, тон, создание.
</sketch>

<open-questions>
- Soft-delete механика (строка живёт, RemovedAt) vs tombstone-конверсия при delete (оригинал уходит, tombstone остаётся) — юзер склонился ко второму (тип как источник правды), решить на спеке.
- Должны ли tombstone'ы попадать в семантический индекс (да? — тогда «что умерло» находится поиском).
- Пин в onboard-quickstart или отдельный ресурс-листинг.
</open-questions>

<status>
Draft; спека после короткого брейншторма с юзером. Очередь: после 023-хвоста и 026, до/вместе с 027-догфудом (там же UX-проход по entries).
</status>

<log>
- 2026-10-04 [auto] — artifact created → inProgress
- 2026-10-05 [agent] — Van by claude claimed the feature
</log>
