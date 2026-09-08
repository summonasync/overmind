---
feature: 001
task: 03
slug: adapt-claude-skills-agents
status: done
created: 2026-08-31
---

# 001/03 — Create .claude/skills and .claude/agents adapted copies

<goal>
`.claude/skills/` (18 штук) и `.claude/agents/` (17 файлов) — полные адаптированные копии
Kimi-стороны, с нулевой утечкой kimi-строк.
</goal>

<context>
Зависит от task 01. Replacement-table (rules 1-16) и model-mapping — в `../plan.md`.
sed-проход механический; rule 14 (`kimi-code` → `claude`) и упоминания config.toml pool —
ручной контекстный проход. На Windows Git Bash sed -i: использовать бэкап-суффикс на первом
прогоне (`sed -i.bak`, потом `find . -name '*.bak' -delete`), проверять `git diff` на CRLF-шум.
</context>

<scope>
- `mkdir -p .claude/skills .claude/agents`
- `cp -r .kimi-code/skills/* .claude/skills/` и `cp .kimi-code/agents/*.md .claude/agents/`
- `cp .kimi-code/SKILLFLOW.md .claude/SKILLFLOW.md` + тот же проход замен (внутри SKILLFLOW.md есть K2.7/K3 — заменить по таблице)
- sed-проход по всем трём (rules 1-13, 15-16 из plan.md)
- Ручной проход: `grep -rni 'kimi' .claude/` — каждый хит глазами, контекстная замена; блоки про `config.toml` secondary_model pool → переформулировка про `model:` frontmatter enforcement
- Фронтматтер агентов: слить `whenToUse` в `description` (фраза «Use when ...»), добавить `model:` по model-mapping из plan.md
</scope>

<anti-scope>
- CLAUDE.md / AGENTS.md — task 02, здесь не трогаем.
- Контент code-style/workflow правил в скиллах не меняем по смыслу.
- Коммиты — только после подтверждения пользователя.
</anti-scope>

<constraints>
- Формат Claude agent frontmatter: только `name`, `description`, `model` (+ `tools` если появится необходимость — по умолчанию нет).
- opus: audit-orchestrator, coding-challenge-orchestrator, task-orchestrator, ai-code-detector, ai-obfuscator, ai-obfuscation-reviewer, architect-review; sonnet: остальные.
</constraints>

<acceptance-criteria>
- [ ] `ls -d .claude/skills/*/ | wc -l` = 20, `ls .claude/agents/*.md | wc -l` = 18
- [ ] `.claude/SKILLFLOW.md` существует, `grep -ni 'k2\.7\|\bk3\b\|kimi' .claude/SKILLFLOW.md` — пусто
- [ ] `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml' .claude/` — пусто
- [ ] Каждый `.claude/agents/*.md` имеет frontmatter с `name`, `description` (содержит «Use when»), `model`
- [ ] `grep -rl 'model: opus' .claude/agents/` = ровно 7 файлов из model-mapping
- [ ] `grep -rn '~/.claude/skills/' .claude/agents/ | wc -l` ≥ 17 (каждый агент ссылается на свой скилл)
- [ ] Изменённые файлы доложены; коммит только после подтверждения
</acceptance-criteria>

<refs>
- @../plan.md — replacement-table, model-mapping, риски (sed/CRLF)
- tasks/01-move-to-kimi-home.md — prerequisite
</refs>

<obstacles>
- Frontmatter key `subagents:` (audit-orchestrator, coding-challenge-orchestrator, task-orchestrator)
  не существует в Claude Code — ключ дропнут, список делегаций остался только в теле скиллов.
- Skill SKILL.md frontmatter содержит `whenToUse:` (Kimi-формат); по scope таски реворкался только
  agent frontmatter — скиллы оставлены как есть (Claude Code игнорирует неизвестные ключи).
- `.kimi/` в stop-list coding-challenge-orchestrator/SKILL.md:65 заменено на `.claude/` (rule 14, контекстная).
</obstacles>

<log>
- 2026-08-31: .claude/ создан (20 skills, 18 agents, SKILLFLOW.md); sed-проход rules 1-13,15-16;
  ручной rule-14 фикс в coding-challenge-orchestrator; agent frontmatter переписан
  (whenToUse слит в description, model: opus x7 / sonnet x11, subagents: дропнут).
  Все acceptance-criteria зелёные. Отчёт: .superpowers/sdd/001-dual-tool-repo-layout/task-3-report.md
</log>
