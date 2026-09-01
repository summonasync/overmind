---
feature: 002
slug: antigravity-support
status: approved
version: 1
created: 2026-09-04
---

# Feature 002 — Antigravity support (third runtime mirror)

<goal>
Репозиторий — единый источник правды для ТРЁХ рантаймов: Kimi Code CLI (`~/.kimi-code`),
Claude Code (`~/.claude`) и Google Antigravity (`~/.gemini` + `~/.gemini/antigravity`).
Полное зеркало `.gemini/` по образцу `.claude/`: адаптированные копии, translation table,
sync-контракт теперь трёхсторонний.
</goal>

<context>
Quick prototype уже сделан и лежит в рабочем дереве (uncommitted):
- `.gemini/GEMINI.md` — адаптированная копия AGENTS.md (без Kimi-специфики: нет пула
  K2.7/K3, нет config.toml/hooks; safety-правила переписаны как behavior + отсылка к
  permission-настройкам Antigravity).
- `manifest.json` — добавлены homes `gemini` и `antigravity`; skills/agents пока
  деплоятся ПРЯМЫМ КОПИРОВАНИЕМ из `.kimi-code/` — эта фича заменяет это на зеркало.
- `install.sh` / `install.ps1` — новые homes + мягкая проверка `~/.gemini` для antigravity.
Dry-run пройден. Этот прототип — отправная точка, не финальное состояние.
</context>

<requirements>
- `.gemini/` в корне репо содержит ПОЛНЫЕ адаптированные копии:
  - `.gemini/GEMINI.md` (глобальная память; прототип ревьюится и приводится к финальному виду)
  - `.gemini/antigravity/skills/<22 штуки>/` — адаптированные копии `.kimi-code/skills/`
  - `.gemini/antigravity/agents/<20 штук>.md` — адаптированные копии `.kimi-code/agents/`
  - `.gemini/antigravity/SKILLFLOW.md` — адаптированная копия
- `SOCIAL_DICTIONARY.md` НЕ дублируется в репо для gemini: живёт один раз в `.kimi-code/`,
  manifest копирует его в `~/.gemini/` (как уже сделано для claude).
- Translation table Kimi→Antigravity (вставляется в SYNC-STRUCTURE.md):
  - `~/.kimi-code/` → `~/.gemini/antigravity/` (для skills/agents) и `~/.gemini/` (для rules)
  - `Kimi K2.7` / `kimi-code/kimi-for-coding` → «сильная execution-модель, выбирается в Agent Manager»
  - `Kimi K3` / `kimi-code/kimi-k3` → «сильнейшая reasoning-модель, выбирается в Agent Manager»
    (Antigravity не имеет фиксированного пула алиасов — правило формулируется ролью, не именем модели)
  - `AgentSwarm` → параллельные background-агенты Antigravity Agent Manager
  - `config.toml` secondary-model pool → отсутствует; enforcement — permission-настройки Antigravity
  - hooks (PreToolUse/PostToolUse) → отсутствуют; safety-правила — behavior-обязательные
- `manifest.json`: записи `antigravity`-home переключаются с `.kimi-code/skills|.agents`
  на `.gemini/antigravity/skills|.agents|.gemini/antigravity/SKILLFLOW.md`; добавляется
  копия SOCIAL_DICTIONARY.md → home `gemini`.
- `uninstall.sh` / `uninstall.ps1` знают homes `gemini` и `antigravity`; защита
  «never touch home root» расширена на `~/.gemini` и `~/.gemini/antigravity`.
- `SYNC-STRUCTURE.md` переписан под трёхсторонний контракт: правило «правишь одну —
  правишь две другие в том же коммите», трёхколоночная таблица маппинга
  (Kimi | Claude | Antigravity), обе translation tables (Kimi→Claude, Kimi→Antigravity),
  pre-commit checklist с grep-гейтами на все три стороны.
- `README.md`: Antigravity добавлен в обзор, layout, install-секцию; зафиксирован
  известный конфликт `~/.gemini/GEMINI.md` с Gemini CLI (google-gemini/gemini-cli#16058).
- Верификация (grep-гейты):
  - в `.gemini/` ноль вхождений `~/.kimi-code`, `~/.claude`, `K2.7`, `K3`, `kimi`,
    `AgentSwarm`, `config.toml`, `sonnet`, `opus`, `haiku`
  - в `.kimi-code/` и `.claude/` ноль вхождений `gemini`, `antigravity`
    (кроме manifest/install/uninstall/SYNC-STRUCTURE/README — они общие)
- Коммиты — только после явного подтверждения пользователя.
</requirements>

<decisions>
- Полное зеркало `.gemini/` (адаптированные копии) выбрано над деплоем `.kimi-code/skills`
  как есть: подтверждено пользователем 2026-09-04. Причина та же, что в feature 001 —
  cross-contamination недопустим, tool-специфика в скиллах реальная (модели, hooks, пути).
- Antigravity skills — тот же open standard `SKILL.md`, формат сохраняется 1:1,
  адаптация только контентная.
- Модели НЕ пинятся именами: у Antigravity нет стабильных алиасов, набор моделей
  (Gemini / Claude / GPT) меняется. Правило — по роли (execution vs reasoning).
- Формат agents у Antigravity комьюнити кладёт в `~/.gemini/antigravity/agents/` —
  следуем этой конвенции; если нативная поддержка отличается, фиксируется в obstacles.
</decisions>

<non-goals>
- Не меняем контент code-style/workflow правил — только tool-специфичные фрагменты.
- Никаких генераторов/шаблонизаторов поверх markdown.
- Не трогаем реальные `~/.gemini*` до явного запуска install без dry-run.
- Не адаптируем workflows (`.agent/workflows`) — отдельная фича, если понадобится.
- Не решаем конфликт GEMINI.md ↔ Gemini CLI — только документируем.
</non-goals>

<open-questions>
- Живой прогон в установленном Antigravity: подхватывает ли он skills/agents из
  `~/.gemini/antigravity/` — верифицируется пользователем вручную после install.
</open-questions>

<!--
LIFECYCLE: после status: approved файл ЗАМОРОЖЕН.
Изменение = ре-валидация с пользователем + bump version. Молчаливые правки запрещены.
-->
