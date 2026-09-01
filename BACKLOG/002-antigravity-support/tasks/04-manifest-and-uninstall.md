---
feature: 002
task: 04
slug: manifest-and-uninstall
status: done
created: 2026-09-04
---

# 002/04 — Manifest on mirror + uninstall support

<goal>
`manifest.json` деплоит gemini-сторону из зеркала `.gemini/` (а не прямым копированием
`.kimi-code/`); `uninstall.sh`/`uninstall.ps1` умеют удалять gemini/antigravity цели.
</goal>

<context>
Прототип уже добавил homes `gemini`/`antigravity` в install.sh/install.ps1 (с мягкой
проверкой `~/.gemini` для antigravity) и временные записи в manifest, где skills/agents
копируются из `.kimi-code/`. Теперь зеркало `.gemini/` существует (tasks 01-03) —
переключаем источники. install-скрипты менять НЕ нужно, только manifest + uninstall.
</context>

<scope>
- `manifest.json`:
  - `{ "source": ".gemini/antigravity/skills", "home": "antigravity", "type": "dir" }` (вместо `.kimi-code/skills`)
  - `{ "source": ".gemini/antigravity/agents", "home": "antigravity", "type": "dir" }` (вместо `.kimi-code/agents`)
  - добавить `{ "source": ".gemini/antigravity/SKILLFLOW.md", "home": "antigravity", "type": "file" }`
  - добавить `{ "source": ".kimi-code/SOCIAL_DICTIONARY.md", "home": "gemini", "type": "file" }`
  - запись `.gemini/GEMINI.md` → home gemini остаётся как есть
- `uninstall.sh`: home_path += gemini → `~/.gemini`, antigravity → `~/.gemini/antigravity`;
  защита «never touch home root» расширена на оба новых пути. Учесть, что сейчас
  `[[ -d "$hp" ]] || continue` молча пропускает несуществующий home — поведение сохранить.
- `uninstall.ps1`: аналогично (homePaths += gemini, antigravity; защита корней).
- Верификация: `install.sh --dry-run`, `uninstall.sh --dry-run`, `install.ps1 -DryRun`,
  `uninstall.ps1 -DryRun` — вывод соответствует ожиданиям (gemini-записи скипаются, если
  `~/.gemini` отсутствует; если существует — источники из `.gemini/`).
</scope>

<anti-scope>
- Не меняем логику install-скриптов (там всё уже есть из прототипа).
- Не трогаем контент `.gemini/`.
- Не запускаем install/uninstall без dry-run.
</anti-scope>

<constraints>
- Идемпотентность и dry-run семантика скриптов сохраняются.
- PowerShell-скрипт остаётся primary на Windows (robocopy).
</constraints>

<acceptance-criteria>
- [ ] manifest.json валиден (python json.load), источники antigravity-записей — `.gemini/`
- [ ] Оба uninstall-скрипта знают 4 home; защита корней `~/.gemini` и `~/.gemini/antigravity`
- [ ] Все 4 dry-run прогнаны, вывод зафиксирован в log
- [ ] Изменённые файлы доложены; коммит только после подтверждения
</acceptance-criteria>

<refs>
- @../feature.md — requirements (manifest, uninstall)
- tasks/01-mirror-skills.md, tasks/02-mirror-agents.md — источники зеркала
</refs>

<obstacles>
</obstacles>

<log>
- 2026-09-04: manifest.json — antigravity-записи переключены на зеркало (.gemini/antigravity/skills|agents + SKILLFLOW.md file-записью), добавлена SOCIAL_DICTIONARY.md → home gemini. uninstall.sh: home_path += gemini/antigravity, защита home root расширена на ~/.gemini и ~/.gemini/antigravity. uninstall.ps1: homePaths += gemini/antigravity (защита корней наследуется из $targets.Home автоматически). install-скрипты не тронуты (хвосты из прототипа уже на месте). Верификация: `python json.load` ok; все 4 dry-run чистые — gemini/antigravity корректно скипаются при отсутствии ~/.gemini, kimi/claude цели без изменений, uninstall перечисляет только ожидаемые пути.
</log>
