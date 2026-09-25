---
feature: 040
slug: artifact-partial-read
status: done
priority: p1
version: 2
created: 2026-09-22
---

# Feature 040 — Artifact partial read: read-artifact-by-id + параметр sections + сниппеты в search

<goal>
grep/sed-эквивалент над телами артефактов. Сегодня `planning_read_feature` — единственное чтение и возвращает фичу со всеми артефактами целиком (8–9k токенов, до ~150k символов на живом проекте), а `search` находит артефакт, но не показывает нужный кусок внутри него. На диске тот же вопрос решается `grep -A15 '<log>' tasks/*.md` за ~2k токенов. Гранулярность чтения — вторая по громкости боль полевого фидбека (три автора из четырёх).
</goal>

<requirements>
- Чтение одного артефакта по id — то, что нужно сразу после `planning_current_task`, без вытягивания всей фичи.
- Параметр `sections: [...]` у чтения артефакта — вернуть только названные XML-секции (например, `["log", "obstacles"]`).
- `search` отдаёт совпадение с контекстом в N строк, а не целые тела артефактов/записей.
- Дешёвые метаданные (статусы, версии) читаются без тел.
</requirements>

<non-goals>
- Патч-запись — 040 про чтение, запись в 039.
- Web UI read mode (inline expand/collapse, fullscreen) — отдельная draft-фича 026-entries-read-mode, не пересекается.
- Листинг записей (`list_entries`) — 041.
- Diff версий — вторая волна.
</non-goals>

<refs>
- FEATURELIST/05-no-partial-artifact-read.md — главный документ: grep-эквивалент, 150k символов на MMT-36.
- FEATURELIST/01-token-cost-profile.md — «read one section of a spec»: ~7K vs ~500 токенов.
- FEATURELIST/03-tool-description-gaps.md (сниппеты в search), FEATURELIST/08-migration-field-report.md («sed -n '55,80p' для obstacles, здесь all-or-nothing»).
- Тот же корень, что 039: единица работы — весь документ; 039 лечит запись, 040 — чтение.
</refs>

<log>
- 2026-09-24 [ui] — status moved to 'done' via ui
</log>
