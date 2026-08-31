---
feature: 001
task: 02
slug: port-claude-md
status: done
created: 2026-08-31
---

# 001/02 — Port AGENTS.md to CLAUDE.md

<goal>
В корне появляется `CLAUDE.md` — полный порт AGENTS.md с Claude-эквивалентами вместо
Kimi-специфики; AGENTS.md получает layout-note и обновлённые пути.
</goal>

<context>
Зависит от task 01 (`.kimi-code/` существует). CLAUDE.md будет деплоиться sync-скриптом
в `~/.claude/CLAUDE.md` (task 05). Таблица замен и model-mapping — в `../plan.md`.
</context>

<scope>
- Создать `CLAUDE.md`: копия AGENTS.md с применением replacement-table (plan.md) и правками ниже
- Обновить `AGENTS.md`: layout note + пути в таблице Documentation Files
</scope>

<anti-scope>
- Не меняем tone/slang/stack/workflow секции по смыслу — только tool-специфику.
- `.claude/agents`, `.claude/skills` — task 03.
- Коммиты — только после подтверждения пользователя.
</anti-scope>

<constraints>
- Правки CLAUDE.md относительно AGENTS.md:
  - Применить replacement-table 1-16 из plan.md.
  - Секцию `Sub-Agent Rules` переписать: разрешённые модели `sonnet` (дефолт coder/reviewer) / `opus` (orchestration, audit, ai-code-detector, ai-obfuscator, ai-obfuscation-reviewer); enforcement — через `model:` поле во frontmatter `.claude/agents/*.md`, НЕ через config.toml pool.
  - Блок `**Enforcement (config, not just prompt):**` с TOML-сниппетом заменить на: «sub-agent model choice is enforced by the `model` field in each `.claude/agents/*.md` frontmatter (sonnet for coder/reviewer agents, opus for orchestration/audit/obfuscation) — not by prompt text alone.»
  - Таблица Documentation Files: `TASKS/nnn-slug/` остаётся как есть (repo-root конвенция); `SOCIAL_DICTIONARY.md` → `~/.kimi-code/SOCIAL_DICTIONARY.md` на Kimi-стороне; на Claude-стороне путь деплоя словаря — `~/.claude/SOCIAL_DICTIONARY.md` (sync копирует).
  - В CLAUDE.md: «Superpowers Integration» — ссылки на плагин адаптировать к Claude Code (superpowers plugin for Claude Code).
- Правка AGENTS.md (корень): под `# Global Agent Context` добавить:
  > Repo layout: this file's crew lives in `.kimi-code/` (agents, skills, dictionary, config). Claude Code mirror lives in `.claude/` + `CLAUDE.md`. TASKS/ stays in repo root (feature tracking). Dual-edit rule: see `SYNC-STRUCTURE.md`.
  - В таблице Documentation Files обновить упоминание SOCIAL_DICTIONARY.md на `~/.kimi-code/SOCIAL_DICTIONARY.md`.
</constraints>

<acceptance-criteria>
- [ ] `CLAUDE.md` существует, содержит все секции AGENTS.md (tone, .NET stack, frontend, superpowers integration, workflow)
- [ ] `grep -in 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml' CLAUDE.md` — пусто
- [ ] `grep -in 'claude\|sonnet\|opus\|haiku' AGENTS.md` — хиты только в новом layout note (`.claude/`, `CLAUDE.md`) — intentional
- [ ] Diff CLAUDE.md vs AGENTS.md показывает только tool-специфичные замены
- [ ] Изменённые файлы доложены; коммит только после подтверждения
</acceptance-criteria>

<refs>
- @../plan.md — replacement-table, model-mapping
- @../feature.md — requirements (CLAUDE.md порт)
- tasks/01-move-to-kimi-home.md — prerequisite
</refs>

<obstacles>
- Grep-гейт «ноль kimi в CLAUDE.md» конфликтовал с упоминанием `.kimi-code/` в layout note — решено: в CLAUDE.md note только про `.claude/` сторону (asymmetric by design).
</obstacles>

<log>
- 2026-08-31: CLAUDE.md портирован (15/15 секций), AGENTS.md получил layout note + фикс пути словаря; ревью чистое.
</log>
