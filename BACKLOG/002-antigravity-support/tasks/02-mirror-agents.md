---
feature: 002
task: 02
slug: mirror-agents
status: done
created: 2026-09-04
---

# 002/02 — Mirror agents into .gemini/antigravity/agents

<goal>
`.gemini/antigravity/agents/` содержит 20 адаптированных копий агентов из `.kimi-code/agents/`.
</goal>

<context>
Зависит от task 01 (translation table уже применена на скиллах — держим консистентность
формулировок). Комьюнити-конвенция Antigravity: глобальные агенты живут в
`~/.gemini/antigravity/agents/`. Если фронтматтер Kimi-агентов несовместим — зафиксировать
в obstacles, не изобретать молча.
</context>

<scope>
- Скопировать все 20 `.md` из `.kimi-code/agents/` в `.gemini/antigravity/agents/`
  (список: `ls .kimi-code/agents/`).
- Применить translation table из plan.md (порядок 1→15).
- Ручной проход: model-пулы → роли; hooks → behavior-правила; ссылки на скиллы →
  `~/.gemini/antigravity/skills/<n>/SKILL.md`.
- Фронтматтер: сохранить `name`/`description`; Kimi-специфичные поля (если есть,
  напр. model-алиасы) заменить ролевыми формулировками или удалить — решение зафиксировать
  в log.
</scope>

<anti-scope>
- Не меняем `.kimi-code/` и `.claude/`.
- Не трогаем скиллы (task 01), GEMINI.md (task 03), manifest (task 04).
</anti-scope>

<constraints>
- Семантика ролей агентов не меняется.
</constraints>

<acceptance-criteria>
- [ ] 20 файлов в `.gemini/antigravity/agents/`, имена совпадают с `.kimi-code/agents/`
- [ ] `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml\|sonnet\|opus\|haiku' .gemini/antigravity/agents/` пусто
- [ ] Решения по фронтматтеру зафиксированы в log
- [ ] Изменённые файлы доложены; коммит только после подтверждения
</acceptance-criteria>

<refs>
- @../feature.md
- @../plan.md — translation table
- tasks/01-mirror-skills.md — консистентность формулировок
</refs>

<obstacles>
</obstacles>

<log>
2026-09-04 — executed by sub-agent (K2.7 pool).
- Скопированы все 20 `.md` из `.kimi-code/agents/` в `.gemini/antigravity/agents/`.
- Translation table: сработала только замена #1 (`~/.kimi-code/skills` →
  `~/.gemini/antigravity/skills`) — по 20 вхождениям, по одной на файл. Правила 2–15
  не имели матчей: в агентах нет model-пулов, hooks, AgentSwarm, config.toml,
  slash-команд — вся эта специфика живёт в скиллах/AGENTS.md, а не в agent-файлах.
  Ручной проход по всем 20 файлам это подтвердил (файлы однотипные, ~16 строк каждый).
- Frontmatter decisions:
  - `name`/`description` — сохранены 1:1 во всех 20 файлах.
  - `whenToUse` — сохранено: runtime-агностичное метаполе, без Kimi-специфики;
    нераспознанные поля фронтматтера Antigravity игнорирует безвредно.
  - `tools` (в 8 файлах: ai-code-detector, ai-obfuscation-reviewer, architect-review,
    dotnet-code-reviewer, naming-helper, next-js-code-reviewer, pipeline-doctor) —
    сохранено: имена тулзов (Read/Grep/Glob/Bash/WebSearch/FetchURL) совпадают с
    набором Antigravity, Kimi-специфики нет.
  - `subagents` (в 3 файлах: audit-orchestrator, coding-challenge-orchestrator,
    task-orchestrator) — сохранено: список ролей сабагентов, ролевая семантика
    переносится на Agent Manager как есть.
  - Model-алиасов во фронтматтере не было — ничего не заменялось/удалялось.
- Verification: `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml\|sonnet\|opus\|haiku'
  .gemini/antigravity/agents/` — пусто (exit 1). `diff` списков файлов
  `.kimi-code/agents` vs `.gemini/antigravity/agents` — совпадают (20/20).
- Статус остаётся in_progress — закрытие после review.
</log>
