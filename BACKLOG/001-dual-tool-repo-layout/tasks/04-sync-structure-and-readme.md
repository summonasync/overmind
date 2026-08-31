---
feature: 001
task: 04
slug: sync-structure-and-readme
status: done
created: 2026-08-31
---

# 001/04 — Write SYNC-STRUCTURE.md and README.md

<goal>
В корне появляются `SYNC-STRUCTURE.md` (контракт двойной правки) и `README.md`
(обзор + prerequisites + использование sync).
</goal>

<context>
Зависит от tasks 01-03 (финальные списки файлов известны). Оба файла на английском.
На них ссылаются AGENTS.md (layout note из task 02) и CLAUDE.md.
</context>

<scope>
- `SYNC-STRUCTURE.md`:
  - Правило: «Edit one side — edit the other in the same commit». Различия сторон допустимы ТОЛЬКО для tool-специфики (имена моделей, home-пути, runtime-фичи).
  - Таблица маппинга: `AGENTS.md`↔`CLAUDE.md`, `.kimi-code/SKILLFLOW.md`↔`.claude/SKILLFLOW.md`, `.kimi-code/agents/<n>.md`↔`.claude/agents/<n>.md` (18 строк — сгенерить из `ls .kimi-code/agents/`), `.kimi-code/skills/<n>/`↔`.claude/skills/<n>/` (20 строк — из `ls .kimi-code/skills/`).
  - Отдельно зафиксировать: `BACKLOG/` живёт только в корне репо; sync-скрипт деплоит `BACKLOG/_TEMPLATE/` в оба home. `SOCIAL_DICTIONARY.md` живёт в `.kimi-code/`, sync копирует в оба home. `config.toml` — только Kimi; у Claude аналог — `~/.claude/settings.json`, контентом не синкается.
  - Translation table Kimi→Claude: вставить replacement-table из plan.md.
  - Pre-commit checklist: пары синхронизированы; `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm' .claude/` пусто; `grep -rni 'claude code\|sonnet\|opus\|haiku' .kimi-code/` пусто.
- `README.md`:
  - What this is: единый источник правды для agent crew поверх Kimi Code CLI и Claude Code.
  - Prerequisites: Kimi Code CLI + обязательный `superpowers` plugin (`~/.kimi-code/plugins/managed/superpowers` — workflow-скиллы brainstorming/writing-plans/TDD зависят от него); Claude Code; для паритета workflow — Superpowers plugin для Claude Code.
  - Layout: дерево `.kimi-code/`, `.claude/`, `BACKLOG/`, CLAUDE.md, AGENTS.md, SYNC-STRUCTURE.md, sync-скрипты.
  - Install/update: `sync.ps1 [-DryRun]` / `sync.sh [--dry-run]`, что куда копируется.
  - Editing rule: ссылка на SYNC-STRUCTURE.md.
</scope>

<anti-scope>
- Не дублируем контент AGENTS.md/CLAUDE.md в README — только ссылки.
- Коммиты — только после подтверждения пользователя.
</anti-scope>

<constraints>
- Английский язык, плотный технический стиль, без маркетинговой воды.
</constraints>

<acceptance-criteria>
- [ ] Оба файла существуют в корне
- [ ] Таблица маппинга в SYNC-STRUCTURE.md содержит 18 agent-пар, 20 skill-пар (реальные имена) и строку SKILLFLOW.md
- [ ] README упоминает superpowers plugin как обязательный prerequisite с точным путём
- [ ] Все пути, упомянутые в README, существуют в репо (проверка вручную или `[ -e ]` циклом)
- [ ] Изменённые файлы доложены; коммит только после подтверждения
</acceptance-criteria>

<refs>
- @../feature.md — requirements (README prerequisites, SYNC-STRUCTURE)
- @../plan.md — replacement-table (для вставки в SYNC-STRUCTURE.md)
- tasks/01-move-to-kimi-home.md, tasks/03-adapt-claude-skills-agents.md — источники списков файлов
</refs>

<obstacles>
</obstacles>

<log>
</log>
