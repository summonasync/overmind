---
feature: 002
task: 06
slug: contamination-gate
status: done
created: 2026-09-04
---

# 002/06 — Cross-contamination gate

<goal>
Финальный гейт: grep-проверки кросс-контаминации всех трёх сторон + dry-run всех
скриптов. Фича не считается done без чистого вывода.
</goal>

<context>
Последняя таска. Аналог task 06 фичи 001, но на три рантайма. Общие файлы
(manifest.json, install/uninstall скрипты, SYNC-STRUCTURE.md, README.md, BACKLOG/)
законно упоминают все стороны — они вне гейта.
</context>

<scope>
- Гейт 1: `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml\|sonnet\|opus\|haiku\|\.claude' .gemini/` → пусто.
- Гейт 2: `grep -rni 'gemini\|antigravity' .kimi-code/ .claude/` → пусто.
- Гейт 3 (существующий, регрессия): `grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm' .claude/` → пусто;
  `grep -rni 'claude code\|sonnet\|opus\|haiku' .kimi-code/` → пусто.
- Структурная сверка: `diff <(ls .kimi-code/skills) <(ls .gemini/antigravity/skills)` пусто;
  `diff <(ls .kimi-code/agents) <(ls .gemini/antigravity/agents)` пусто.
- Dry-run: `install.sh --dry-run`, `uninstall.sh --dry-run` (+ ps1-эквиваленты, если
  доступен pwsh) — вывод консистентен manifest.
- manifest.json валиден: `python -c "import json; json.load(open('manifest.json'))"`.
- Любая находка → фикс в рамках этой таски + повторный гейт. Всё зафиксировать в log.
</scope>

<anti-scope>
- Не рефакторим контент — только фиксы контаминации.
- Коммит только после подтверждения пользователя.
</anti-scope>

<constraints>
- Гейты запускаются из корня репо.
</constraints>

<acceptance-criteria>
- [ ] Все grep-гейты пусты
- [ ] Структурные diff пусты
- [ ] Dry-run всех скриптов чистый, вывод в log
- [ ] BACKLOG.md: фича переведена в Done (после подтверждения пользователя)
</acceptance-criteria>

<refs>
- @../feature.md — верификация
- @../plan.md — verification
- tasks/01-05 — весь произведённый контент
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-04: Все гейты PASS с первого прогона, фиксов не потребовалось:
  gate1 (.gemini чист от kimi/claude маркеров) PASS; gate2 (.kimi-code/.claude чисты от gemini/antigravity) PASS; gate3a/3b (регрессия kimi↔claude) PASS; структурные diff skills (22) и agents (20) пусты; manifest.json валиден.
  Dry-run install.sh / install.ps1 / uninstall.sh / uninstall.ps1: gemini/antigravity корректно скипаются (~/.gemini отсутствует на этой машине), kimi/claude цели без изменений, uninstall не тащит лишнего. Реальный деплой в ~/.gemini не выполнялся (Antigravity не установлен — живой прогон за пользователем, см. open-questions в feature.md).
</log>
