---
feature: 047
slug: api-read-surface-polish
status: done
priority: p0
version: 1
created: 2026-09-22
---

# Feature 047 — API read-surface polish: per-status breakdown в list_features + три докгэпа описаний тулов

<goal>
Дешёвая полировка читаемости MCP-ответов и описаний — четыре мелочи, каждая стоила лишний вызов или лишнюю догадку в поле. Главная: `planning_list_features` отдаёт только `taskTotal`/`taskDone` — фича «4/7» неотличима от «4 done, 3 нетронутых» при «4 done, 2 в работе, 1 blocked», хотя заблокированная и незапланированная задачи требуют противоположных действий. Остальные три — пробелы в формулировках тулов, из-за которых вызывающий угадывает.
</goal>

<requirements>
- Per-status breakdown в `planning_list_features`: `{done, inProgress, todo, blocked}` в той же строке фичи.
- Фраза «Markdown body with its XML sections» у `planning_upsert_artifact`: либо задокументировать с примером, либо убрать как устаревшую — сейчас plain markdown принимается молча.
- Указатель на `overmind://resources` в инструкциях сервера: сказать, чем заменить, если ресурс-тулы не загружены (`list_entries` сработал), либо экспонировать ресурсы тулом.
- Семантика поля `order` у task-артефактов: сортировка или идентификатор — «дырки» в нумерации при импорте прошли наугад.
</requirements>

<non-goals>
- Пагинация/лимиты `list_entries` — уже в требованиях 041-entries-metadata-listing.
- Изменение поведения статусов/переходов — 043.
</non-goals>

<refs>
- FEATURELIST/09-api-nits.md — главный документ (breakdown + order-семантика).
- FEATURELIST/03-tool-description-gaps.md — XML-фраза и resources-pointer.
- README FEATURELIST пп. 12–13.
</refs>

<log>
- 2026-10-06 [agent] — Van by claude claimed the feature
- 2026-10-06 [auto] — artifact created → ready
- 2026-10-06 [auto] — task 00 taken → inProgress
- 2026-10-06 [agent] — by user request in-session — armer notification 8d914133; spec v2 re-validated + result validated in the final batch sitting. taskCounts feature closed (caa6f31 + 3aca50c + 8e7667d).
- 2026-10-06 [agent] — done via agent review by Van (claude), approved by human tap 2026-10-06
- 2026-10-06 [agent] — claim released on done-close
</log>
