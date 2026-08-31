---
feature: 001
slug: dual-tool-repo-layout
status: approved
version: 5
created: 2026-08-31
---

# Feature 001 — Dual-tool repo layout (.kimi-code + .claude)

<goal>
Репозиторий — единый источник правды для ДВУХ рантаймов: Kimi Code CLI (`~/.kimi-code`)
и Claude Code (`~/.claude`). Одна и та же команда агентов и скиллов, адаптированная под
конвенции каждого инструмента, с явным sync-контрактом против дрейфа.
</goal>

<requirements>
- Текущий контент физически переезжает (git mv) в `.kimi-code/`: agents/ (18 файлов, включая architect-review), skills/ (20 штук, включая dotnet-best-practices и architect-review), SKILLFLOW.md, SOCIAL_DICTIONARY.md, config.toml.
- `TASKS/` НЕ переезжает: он смешивает глобальный шаблон (`_TEMPLATE/`) и локальные фичи этого репо (`001-.../`). Остаётся в корне.
- `.claude/` содержит ПОЛНЫЕ адаптированные копии: `.claude/agents/`, `.claude/skills/`, `.claude/SKILLFLOW.md` (K2.7/K3 → sonnet/opus внутри).
- `.kimi-code/` дополнительно содержит `AGENTS.md` (глобальная память Kimi — НЕ файл проекта); `.claude/` содержит `CLAUDE.md` (глобальная память Claude). В корне репо этих файлов нет.
- В корне: `README.md` с секцией Prerequisites: Kimi Code CLI + superpowers plugin (`~/.kimi-code/plugins/managed/superpowers`) — обязателен; Claude Code + опционально Superpowers для паритета workflow.
- В корне: `SYNC-STRUCTURE.md` — контракт «правишь одну сторону — правишь другую в том же коммите» + таблица соответствий файлов + таблица перевода Kimi→Claude.
- В корне: `sync.ps1` (primary, Windows, robocopy /MIR, флаг -DryRun) и `sync.sh` (--dry-run) — идемпотентная раскатка в `~/.kimi-code` и `~/.claude`; `TASKS/_TEMPLATE/` деплоится в оба home как глобальный шаблон (`~/.kimi-code/TASKS/_TEMPLATE`, `~/.claude/TASKS/_TEMPLATE`).
- Адаптация агентов Claude: frontmatter `name`/`description`/`model`; `whenToUse` сливается в description; skill-ссылки → `~/.claude/skills/<name>/SKILL.md`.
- Маппинг моделей: K2.7 → sonnet (дефолт coder/reviewer); K3 → opus (orchestration, audit, ai-code-detector, ai-obfuscator, ai-obfuscation-reviewer); haiku — только где явно lightweight.
- Верификация: в `.claude/` и CLAUDE.md ноль вхождений `~/.kimi-code`, `K2.7`, `K3`, `kimi`, `AgentSwarm`, `config.toml`; в `.kimi-code/` ноль вхождений `~/.claude`, `sonnet`, `opus`, `haiku`, `Claude Code`.
- `git mv` сохраняет историю (проверка `git log --follow`).
</requirements>

<decisions>
- Подход «зеркало с полными копиями» выбран над: общим ядром + генератором (YAGNI для markdown) и симлинками (боль на Windows + форматы frontmatter всё равно различаются).
- Дублирование контента скиллов — принятый trade-off, гасится SYNC-STRUCTURE.md дисциплиной.
- TASKS/ остаётся в корне: шаблон `_TEMPLATE/` — глобальный, фича-папки — локальный трекинг репо; sync-скрипт деплоит только `_TEMPLATE/` в оба home (v2, ре-валидировано с пользователем).
- SOCIAL_DICTIONARY.md живёт один раз в `.kimi-code/`; sync-скрипт копирует его в оба home (источник один).
</decisions>

<non-goals>
- Никаких генераторов/шаблонизаторов поверх markdown.
- Никаких симлинков.
- Не меняем контент code-style/workflow правил — только tool-специфичные фрагменты.
- Не трогаем `~/.kimi-code` / `~/.claude` реально до явного запуска sync без dry-run.
</non-goals>

<open-questions>
</open-questions>

<!--
LIFECYCLE: после status: approved файл ЗАМОРОЖЕН.
Изменение = ре-валидация с пользователем + bump version. Молчаливые правки запрещены.
-->
