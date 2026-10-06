---
feature: 049
slug: backlog-index-retirement
status: done
priority: p0
version: 1
created: 2026-09-22
---

# Feature 049 — Retire BACKLOG.md generated index: состояние — только борда (UI/MCP), на диске — дерево артефактов

<goal>
BACKLOG.md с managed-блоком — вторая копия борды в файле, протухающая by design: материализация может молча не отработать (полевой кейс DMT), и индекс начинает врать, пока борда права. Индекс не нужен ни одной из аудиторий: MCP-агенту (`planning_list_features` отдаёт то же свежее и дешевле), человеку (UI борды). При этом дерево `BACKLOG/nnn-slug/` остаётся нужным — суб-агенты без MCP-доступа читают материализованные `tasks/NN-*.md`, git несёт диффы церемонии, внешние ревьюеры видят контракты рядом с кодом. Фича убирает дубликат-индекс, не материализацию.
</goal>

<requirements>
- Materializer перестаёт писать managed-блок BACKLOG.md.
- Судьба файла определена явно: удаление с tombstone-строкой либо замена статическим pointer'ом («борда — UI/MCP, артефакты — BACKLOG/nnn-slug/») — выбрать в спеке.
- Миграция существующих материализуемых проектов: materializer пишет BACKLOG.md любому проекту, не только нашему — прекращение не молчаливое (deprecation-прогон или явное сообщение), чужие репо не ломаем.
- Контракт воркспейса обновлён: описание BACKLOG.md в глобальном CLAUDE.md экипажа (файлы пользователя — только через него), STRUCTURE.md, документация материализации.
- Согласовано с 042-onboard-observability: onboard-блок про материализию не описывает индекс, которого нет.
</requirements>

<non-goals>
- Дерево `BACKLOG/nnn-slug/` остаётся: интерфейс суб-агентов без MCP, git-диффы церемонии, контракты для внешних ревьюеров.
- Отключение материализации как таковой.
- Экспорт борды в иные форматы (HTML-снапшоты и т.п.).
</non-goals>

<refs>
- FEATURELIST/06-instruction-gaps.md — материализация молча не отработала, поиск вслепую.
- FEATURELIST/02-overmind-vs-files.md + README FEATURELIST вывод — гибрид «Overmind for state, files for contracts»; индекс — state, ему в файлах не место.
- 033-materializer-clobber-guard — соседняя территория материализатора.
- 042-onboard-observability — видимость материализации, согласование формулировок.
</refs>

<log>
- 2026-10-06 [agent] — Van by claude claimed the feature
- 2026-10-06 [auto] — artifact created → ready
- 2026-10-06 [auto] — task 04 taken → inProgress
- 2026-10-06 [auto] — all tasks done + spec + plan validated → review
- 2026-10-06 [agent] — by user request in-session — armer notification 2df3d60c; result validated in the final batch sitting; task 05 (live migration) executed on deployed 0.8.0 and light-APPROVED. The BACKLOG.md index retirement is fully landed: writer gone, tests on end-state, docs+kit+ADR consistent, pointer live, orphans homed.
- 2026-10-06 [agent] — done via agent review by Van (claude), approved by human tap 2026-10-06
- 2026-10-06 [agent] — claim released on done-close
</log>
