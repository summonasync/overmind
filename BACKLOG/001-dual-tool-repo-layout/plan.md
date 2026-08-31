---
feature: 001
slug: dual-tool-repo-layout
status: approved
version: 1
feature: feature.md
created: 2026-08-31
---

# Plan 001 — Dual-tool repo layout

<approach>
Сначала физический git mv в `.kimi-code/` (история сохраняется), затем верификация
self-references. Дальше порты: CLAUDE.md из AGENTS.md, `.claude/skills` + `.claude/agents`
копированием с механической таблицей замен + ручной проход фронтматтера, mirror BACKLOG/.
Потом доки (SYNC-STRUCTURE.md, README.md), sync-скрипты, финальный grep-гейт на
кросс-контаминацию. Коммиты — только после явного подтверждения пользователя.
</approach>

<tasks>
- [x] 01-move-to-kimi-home — git mv agents/skills/dictionary/config в .kimi-code/ (BACKLOG/ НЕ трогаем — остаётся в корне), проверка целостности → tasks/01-move-to-kimi-home.md
- [x] 02-port-claude-md — `.claude/CLAUDE.md` (полный порт AGENTS.md) + layout note в AGENTS.md; v5: AGENTS.md живёт в `.kimi-code/`, CLAUDE.md — в `.claude/` → tasks/02-port-claude-md.md
- [x] 03-adapt-claude-skills-agents — .claude/skills + .claude/agents + .claude/SKILLFLOW.md: копии + таблица замен + фронтматтер → tasks/03-adapt-claude-skills-agents.md
- [x] 04-sync-structure-and-readme — SYNC-STRUCTURE.md + README.md с prerequisites (superpowers) → tasks/04-sync-structure-and-readme.md
- [x] 05-sync-scripts — sync.ps1 (-DryRun) + sync.sh (--dry-run); деплоят .kimi-code/ (включая AGENTS.md), .claude/ (включая CLAUDE.md) и BACKLOG/_TEMPLATE в оба home → tasks/05-sync-scripts.md
- [x] 06-cross-contamination-gate — финальные grep-гейты обеих сторон → tasks/06-cross-contamination-gate.md
</tasks>

<replacement-table>
Применять строго в этом порядке (специфичное раньше общего):

| # | Find | Replace |
|---|------|---------|
| 1 | `~/.kimi-code/skills` | `~/.claude/skills` |
| 2 | `~/.kimi-code/plugins/managed/superpowers` | `superpowers plugin for Claude Code` |
| 3 | `~/.kimi-code/` | `~/.claude/` |
| 4 | `kimi-code/kimi-for-coding-highspeed` | `haiku` |
| 5 | `kimi-code/kimi-for-coding` | `sonnet` |
| 6 | `kimi-code/kimi-k3` | `opus` |
| 7 | `kimi-code/k3-256k` | `opus` |
| 8 | `kimi-code/k3` | `opus` |
| 9 | `Kimi K2.7` | `Claude Sonnet` |
| 10 | `Kimi K3` | `Claude Opus` |
| 11 | `K2.7` | `Sonnet` |
| 12 | `K3` | `Opus` |
| 13 | `Kimi Code CLI` | `Claude Code` |
| 14 | `kimi-code` | `claude` (только в path/identifier контекстах — каждый хит глазами) |
| 15 | `AgentSwarm` | `multiple Task tool calls in one message` |
| 16 | `Agent tool` / `` `Agent` `` | `Task tool` / `` `Task` `` |
</replacement-table>

<model-mapping>
- opus: audit-orchestrator, coding-challenge-orchestrator, task-orchestrator, ai-code-detector, ai-obfuscator, ai-obfuscation-reviewer, architect-review
- sonnet: все остальные агенты
- haiku: только где скилл явно упоминает cheap/lightweight вспомогательную работу
</model-mapping>

<risks>
- Потеря файлов при mv → mitigation: счётчики (17 agents, 18 skills) + git status renames + git log --follow.
- Утечка kimi-строк в .claude → mitigation: grep-гейты в каждой таске + финальный гейт (task 06).
- Дрейф копий в будущем → mitigation: SYNC-STRUCTURE.md + pre-commit чеклист.
- sed по UTF-8/CRLF на Windows Git Bash → mitigation: sed -i с бэкап-суффиксом на первом прогоне, проверка git diff.
</risks>

<verification>
- `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml' .claude/` — пусто (CLAUDE.md внутри `.claude/`).
- `grep -rni 'claude code\|sonnet\|opus\|haiku' .kimi-code/` — пусто (AGENTS.md внутри `.kimi-code/`).
- Паритет количеств: 18/18 агентов, 20/20 скиллов.
- `powershell -File sync.ps1 -DryRun` — план копирования без изменений ФС.
- `ls .kimi-code .claude` соответствует целевой структуре из feature.md: `.kimi-code/` = agents skills AGENTS.md SKILLFLOW.md SOCIAL_DICTIONARY.md config.toml; `.claude/` = agents skills CLAUDE.md SKILLFLOW.md; `BACKLOG/` — в корне; AGENTS.md/CLAUDE.md в корне отсутствуют.
</verification>
