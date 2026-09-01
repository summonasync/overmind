---
feature: 002
slug: antigravity-support
status: approved
version: 1
feature: feature.md
created: 2026-09-04
---

# Plan 002 — Antigravity support

<approach>
Сначала контентное зеркало: механическое копирование skills/agents/SKILLFLOW.md в
`.gemini/antigravity/` + translation table + ручной проход фронтматтера и tool-специфики
(по образцу task 03 фичи 001). Параллельно финализируем GEMINI.md из прототипа.
Затем инфраструктура: manifest переключается на зеркало, uninstall-скрипты учат новые
homes. Потом доки (SYNC-STRUCTURE.md трёхсторонний, README.md). Финал — grep-гейты на
кросс-контаминацию всех трёх сторон. Коммиты — только после подтверждения пользователя.
</approach>

<tasks>
- [x] 01-mirror-skills — `.gemini/antigravity/skills/` (22 шт.) + `.gemini/antigravity/SKILLFLOW.md`: копии `.kimi-code/` + translation table + ручной проход → tasks/01-mirror-skills.md
- [x] 02-mirror-agents — `.gemini/antigravity/agents/` (20 шт.): копии + фронтматтер под конвенции Antigravity → tasks/02-mirror-agents.md
- [x] 03-finalize-gemini-md — ревью прототипного `.gemini/GEMINI.md` против AGENTS.md: полнота портирования, translation table применена → tasks/03-finalize-gemini-md.md
- [x] 04-manifest-and-uninstall — manifest на зеркало + SOCIAL_DICTIONARY → gemini; uninstall.sh/ps1: homes gemini/antigravity + защита home root; dry-run верификация → tasks/04-manifest-and-uninstall.md
- [x] 05-sync-structure-and-readme — SYNC-STRUCTURE.md трёхсторонний (маппинг 3 колонки, обе translation tables, checklist), README.md (Antigravity в обзор/layout/install + конфликт Gemini CLI) → tasks/05-sync-structure-and-readme.md
- [x] 06-contamination-gate — grep-гейты всех трёх сторон + финальный dry-run install/uninstall → tasks/06-contamination-gate.md
</tasks>

<translation-table>
Применять строго в этом порядке (специфичное раньше общего):

| # | Find | Replace |
|---|------|---------|
| 1 | `~/.kimi-code/skills` | `~/.gemini/antigravity/skills` |
| 2 | `~/.kimi-code/plugins/managed/superpowers` | `superpowers plugin (if installed); otherwise follow SKILLFLOW.md transitions directly` |
| 3 | `~/.kimi-code/BACKLOG` | `~/.gemini/antigravity/BACKLOG` |
| 4 | `~/.kimi-code/` | `~/.gemini/antigravity/` |
| 5 | `kimi-code/kimi-for-coding-highspeed` | `fast execution model (selected in Agent Manager)` |
| 6 | `kimi-code/kimi-for-coding` | `strong execution model (selected in Agent Manager)` |
| 7 | `kimi-code/kimi-k3` | `strongest reasoning model (selected in Agent Manager)` |
| 8 | `kimi-code/k3-256k` | `strongest reasoning model (selected in Agent Manager)` |
| 9 | `Kimi K2.7` | `strong execution model` |
| 10 | `Kimi K3` | `strongest reasoning model` |
| 11 | `AgentSwarm` | `parallel background agents (Agent Manager)` |
| 12 | `config.toml` | `Antigravity permission settings` |
| 13 | `Kimi Code CLI` | `Google Antigravity` |
| 14 | `Kimi Code` | `Antigravity` |
| 15 | `~/.claude` / `sonnet` / `opus` / `haiku` (если встретятся в исходнике) | ручной проход — заменить ролевой формулировкой |

После таблицы — обязательный ручной проход:
- Секции про hooks (PreToolUse/PostToolUse/deny-list) → behavior-правила + отсылка к
  permission-настройкам Antigravity (как в GEMINI.md прототипа).
- Секции про sub-agent model pool / secondary_model → Delegation Rules по ролям
  (execution vs reasoning), без имён моделей.
- Slash-команды и CLI-флаги Kimi (напр. `/secondary-model`) → удалить или заменить
  эквивалентом Agent Manager.
- Фронтматтер скиллов сохранить 1:1 (формат SKILL.md — open standard).
</translation-table>

<verification>
- `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml\|sonnet\|opus\|haiku' .gemini/` — пусто
  (исключение: служебные пометки вида "selected in Agent Manager" не матчатся)
- `grep -rni 'gemini\|antigravity' .kimi-code/ .claude/` — пусто
- `install.sh --dry-run` + `install.ps1 -DryRun`: записи antigravity идут из `.gemini/`,
  пропуск при отсутствии `~/.gemini`
- `uninstall.sh --dry-run`: цели gemini/antigravity перечислены, home roots защищены
</verification>
