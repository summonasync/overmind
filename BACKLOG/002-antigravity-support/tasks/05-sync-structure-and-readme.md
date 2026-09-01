---
feature: 002
task: 05
slug: sync-structure-and-readme
status: done
created: 2026-09-04
---

# 002/05 — Three-way SYNC-STRUCTURE.md + README.md

<goal>
`SYNC-STRUCTURE.md` переписан под трёхсторонний контракт; `README.md` включает
Antigravity как третий рантайм.
</goal>

<context>
Зависит от tasks 01-04 (финальные списки файлов и manifest известны). Оба файла на
английском. Текущий SYNC-STRUCTURE.md — двухсторонний (Kimi↔Claude), является эталоном
структуры: core rule, file mapping, особые случаи, translation table, pre-commit checklist.
</context>

<scope>
- `SYNC-STRUCTURE.md`:
  - Core rule → «Edit one side — edit the other two in the same commit». Допустимые
    расхождения: только tool-специфика (имена/роли моделей, home-пути, runtime-фичи,
    hooks vs behavior-правила, plugin-референсы).
  - File mapping → три колонки (Kimi | Claude | Antigravity): AGENTS.md | CLAUDE.md |
    GEMINI.md; SKILLFLOW.md ×3; 20 agent-строк; 22 skill-строки (Antigravity-колонка:
    `.gemini/antigravity/...`).
  - Особые случаи: SOCIAL_DICTIONARY.md живёт в `.kimi-code/`, manifest копирует во ВСЕ
    home (kimi, claude, gemini); config.toml — только Kimi; у Claude — settings.json,
    у Antigravity — permission-настройки в UI (контентом не синкается); BACKLOG/ в корне,
    `_TEMPLATE/` деплоится в kimi+claude (для antigravity — НЕ деплоится, зафиксировать
    явно или добавить запись — решение в log).
  - Translation tables: существующая Kimi→Claude без изменений + новая Kimi→Antigravity
    из plan.md.
  - Pre-commit checklist: пары/тройки синхронизированы + grep-гейты на три стороны
    (в `.gemini/` нет kimi/claude-маркеров; в `.kimi-code/` и `.claude/` нет
    gemini/antigravity).
- `README.md`:
  - What this is: один источник правды для agent crew поверх Kimi Code CLI, Claude Code
    и Google Antigravity.
  - Prerequisites: существующие (Kimi CLI + superpowers, Claude Code) + Google Antigravity.
  - Layout: добавить `.gemini/` в дерево.
  - Install/update: упомянуть homes `~/.gemini`, `~/.gemini/antigravity`; скип-поведение
    при отсутствии `~/.gemini`.
  - Known issues: конфликт `~/.gemini/GEMINI.md` с Gemini CLI (google-gemini/gemini-cli#16058)
    — оба инструмента пишут в один файл.
</scope>

<anti-scope>
- Не дублируем контент rules-файлов в README — только ссылки.
- Не меняем Kimi→Claude translation table.
- Коммиты — только после подтверждения пользователя.
</anti-scope>

<constraints>
- Английский язык, плотный технический стиль, без маркетинговой воды.
</constraints>

<acceptance-criteria>
- [ ] SYNC-STRUCTURE.md: трёхколоночный маппинг (20 agents, 22 skills, rules-файлы), обе translation tables, трёхсторонний checklist
- [ ] README.md: Antigravity в обзоре, prerequisites, layout, install; секция known issues с Gemini CLI конфликтом
- [ ] Все пути из README существуют в репо
- [ ] Изменённые файлы доложены; коммит только после подтверждения
</acceptance-criteria>

<refs>
- @../feature.md — requirements
- @../plan.md — translation table Kimi→Antigravity
- @../../001-dual-tool-repo-layout/tasks/04-sync-structure-and-readme.md — образец
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-04: SYNC-STRUCTURE.md переписан полностью: трёхсторонний core rule, 3-колоночный маппинг (rules + SKILLFLOW + 20 agents + 22 skills), Non-paired + Known intentional differences расширены колонкой Antigravity, добавлена translation table Kimi→Antigravity, checklist с гейтами на три стороны. Решение: BACKLOG/_TEMPLATE деплоится и в antigravity (контент runtime-agnostic, проверено grep — чисто), manifest дополнен записью .kimi-code/BACKLOG → antigravity. Заодно исправлены устаревшие счётчики (18/20 → 20/22). README.md: three runtimes в шапке, Antigravity в prerequisites/layout/install-таблице, секция Known issues с конфликтом GEMINI.md ↔ Gemini CLI (#16058), editing rule на три зеркала.
</log>
