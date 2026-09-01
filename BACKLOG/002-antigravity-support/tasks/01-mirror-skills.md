---
feature: 002
task: 01
slug: mirror-skills
status: done
created: 2026-09-04
---

# 002/01 — Mirror skills into .gemini/antigravity/skills

<goal>
`.gemini/antigravity/skills/` содержит 22 адаптированные копии скиллов из `.kimi-code/skills/`,
плюс `.gemini/antigravity/SKILLFLOW.md` — копию `.kimi-code/SKILLFLOW.md`. Контент семантически
идентичен, отличается только tool-спецификой.
</goal>

<context>
Первая таска фичи. Прототипный `.gemini/GEMINI.md` уже существует — его тут НЕ трогаем.
Формат SKILL.md у Antigravity — тот же open standard, структуру папок и фронтматтер
сохраняем 1:1. Адаптация только контентная.
</context>

<scope>
- Скопировать все 22 папки из `.kimi-code/skills/` в `.gemini/antigravity/skills/`
  (список: `ls .kimi-code/skills/`).
- Скопировать `.kimi-code/SKILLFLOW.md` → `.gemini/antigravity/SKILLFLOW.md`.
- Применить translation table из plan.md (строго в порядке 1→15) ко ВСЕМ файлам копий.
- Ручной проход каждого файла:
  - hooks (PreToolUse/PostToolUse, deny-list через config) → behavior-правила + отсылка к
    permission-настройкам Antigravity.
  - sub-agent model pool (K2.7/K3, secondary_model, `/secondary-model`) → ролевые
    формулировки (strong execution / strongest reasoning model, selected in Agent Manager).
  - Спавн сабагентов: `AgentSwarm` / `Agent` tool упоминания → параллельные background
    агенты Antigravity Agent Manager.
  - Ссылки между скиллами (`~/.kimi-code/skills/<n>/SKILL.md`) → `~/.gemini/antigravity/skills/<n>/SKILL.md`.
- SOCIAL_DICTIONARY.md НЕ копировать (деплоится через manifest из `.kimi-code/`).
</scope>

<anti-scope>
- Не меняем `.kimi-code/` и `.claude/`.
- Не трогаем `.gemini/GEMINI.md` (task 03).
- Не меняем manifest и скрипты (task 04).
- Никаких генераторов; ручная/полуручная правка файлов.
</anti-scope>

<constraints>
- Семантика правил не меняется — только tool-специфика.
- Стиль и плотность исходников сохраняются.
</constraints>

<acceptance-criteria>
- [ ] 22 папки скиллов в `.gemini/antigravity/skills/`, имена совпадают с `.kimi-code/skills/`
- [ ] `.gemini/antigravity/SKILLFLOW.md` существует и адаптирован
- [ ] `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml\|sonnet\|opus\|haiku' .gemini/antigravity/` пусто
- [ ] Внутренние skill-ссылки указывают на `~/.gemini/antigravity/skills/`
- [ ] Изменённые файлы доложены; коммит только после подтверждения
</acceptance-criteria>

<refs>
- @../feature.md — requirements, decisions
- @../plan.md — translation table, ручной проход
- @../../001-dual-tool-repo-layout/tasks/03-adapt-claude-skills-agents.md — образец аналогичной таски
</refs>

<obstacles>
</obstacles>

<log>
2026-09-04 (exec): Скопированы 22 папки `.kimi-code/skills/` → `.gemini/antigravity/skills/` +
`.kimi-code/SKILLFLOW.md` → `.gemini/antigravity/SKILLFLOW.md` (SOCIAL_DICTIONARY.md не копировался).
Translation table применена sed-проходом в порядке 1→14 (правило 15 — sonnet/opus/haiku в исходниках
не встретилось). Ручной проход:
- K2.7/K3 без префикса «Kimi» (таблица их не покрывала) заменены вручную в SKILLFLOW.md,
  ai-code-detector, ai-obfuscator, audit-orchestrator, coding-challenge-orchestrator, pipeline-doctor,
  task-orchestrator на ролевые формулировки (strong execution / strongest reasoning model,
  selected in Agent Manager) — стиль согласован с прототипным GEMINI.md.
- SKILLFLOW.md hard rule: «primary alias forbidden» (концепт рантайма Kimi) →
  «Never inherit the main conversation's model for delegated work» (Agent Manager).
- coding-challenge-orchestrator: в списке запрещённых AI-следов `.kimi/` заменён на обобщённый
  «AI-tool config dirs (`.cursor/`, `.ai/`, `.gemini/`, etc.)» — семантика (запрет AI-артефактов
  в деливерабле) сохранена, grep-гейт `kimi` при этом пустой.
- Hook-секций (PreToolUse/PostToolUse/deny-list) в самих скиллах не оказалось — они живут в AGENTS.md
  (для Antigravity это GEMINI.md, task 03), поэтому behavior-перевод тут не потребовался.
- `superpowers:*` ссылки оставлены как есть — в прототипном GEMINI.md тот же подход
  («if the superpowers plugin is installed, otherwise follow SKILLFLOW.md»).
- AGENTS.md-ссылки сохранены (open standard, GEMINI.md-прототип тоже на них ссылается).
- Line endings: sed-проход сконвертировал 8 CRLF-исходников в LF; восстановлены CRLF 1:1
  (dotnet-architect, dotnet-code-reviewer, dotnet-coder, dotnet-init, frontend-designer,
  next-js-architect, next-js-code-reviewer, next-js-coder).
Верификация: grep-гейт `k2.7|\bk3\b|kimi|agentswarm|config.toml|sonnet|opus|haiku` по
`.gemini/antigravity/skills` + `SKILLFLOW.md` — пусто; `ls skills` идентичен источнику;
14 файлов идентичны источнику побайтово, 8 скиллов + SKILLFLOW.md отличаются только переведёнными
tool-специфичными фрагментами. `.gemini/antigravity/agents/` (чужой прототип, task 02) не трогал.
</log>
