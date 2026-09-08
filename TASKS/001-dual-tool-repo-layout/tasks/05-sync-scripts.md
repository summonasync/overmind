---
feature: 001
task: 05
slug: sync-scripts
status: done
created: 2026-08-31
---

# 001/05 — Write sync.ps1 and sync.sh

<goal>
Идемпотентные sync-скрипты раскатывают `.kimi-code/`, `.claude/`, `CLAUDE.md` и
`TASKS/_TEMPLATE/` в `~/.kimi-code` и `~/.claude`. Оба с dry-run режимом.
</goal>

<context>
Зависит от tasks 01-04 (структура финальная). Primary — Windows (`sync.ps1`, robocopy).
`sync.sh` — *nix-эквивалент на cp. Ничего за пределами целевых mirror-директорий не удаляется.
</context>

<scope>
- Создать `sync.ps1` в корне:

```powershell
param([switch]$DryRun)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$kimiHome = Join-Path $HOME '.kimi-code'
$claudeHome = Join-Path $HOME '.claude'

function Mirror($src, $dst) {
    if ($DryRun) { robocopy $src $dst /MIR /L /NFL /NDL /NJH | Out-Null }
    else { robocopy $src $dst /MIR /NFL /NDL /NJH | Out-Null }
}

function Copy-One($src, $dstDir) {
    if ($DryRun) { Write-Host "Would copy $src -> $dstDir" }
    else { Copy-Item $src $dstDir -Force }
}

Mirror (Join-Path $root '.kimi-code/agents') (Join-Path $kimiHome 'agents')
Mirror (Join-Path $root '.kimi-code/skills') (Join-Path $kimiHome 'skills')
Mirror (Join-Path $root 'TASKS/_TEMPLATE')  (Join-Path $kimiHome 'TASKS/_TEMPLATE')
Copy-One (Join-Path $root '.kimi-code/SOCIAL_DICTIONARY.md') $kimiHome
Copy-One (Join-Path $root '.kimi-code/SKILLFLOW.md') $kimiHome
Copy-One (Join-Path $root '.kimi-code/config.toml') $kimiHome

Mirror (Join-Path $root '.claude/agents') (Join-Path $claudeHome 'agents')
Mirror (Join-Path $root '.claude/skills') (Join-Path $claudeHome 'skills')
Mirror (Join-Path $root 'TASKS/_TEMPLATE') (Join-Path $claudeHome 'TASKS/_TEMPLATE')
Copy-One (Join-Path $root 'CLAUDE.md') $claudeHome
Copy-One (Join-Path $root '.claude/SKILLFLOW.md') $claudeHome
Copy-One (Join-Path $root '.kimi-code/SOCIAL_DICTIONARY.md') $claudeHome

if ($DryRun) { Write-Host 'Dry run complete - no changes made.' }
```

- Создать `sync.sh` в корне + `chmod +x`:

```bash
#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DRY=""
[[ "${1:-}" == "--dry-run" ]] && DRY="echo"

$DRY cp -r "$ROOT/.kimi-code/agents" "$ROOT/.kimi-code/skills" "$HOME/.kimi-code/"
$DRY cp -r "$ROOT/TASKS/_TEMPLATE" "$HOME/.kimi-code/TASKS/"
$DRY cp "$ROOT/.kimi-code/SOCIAL_DICTIONARY.md" "$ROOT/.kimi-code/SKILLFLOW.md" "$ROOT/.kimi-code/config.toml" "$HOME/.kimi-code/"
$DRY cp -r "$ROOT/.claude/agents" "$ROOT/.claude/skills" "$HOME/.claude/"
$DRY cp -r "$ROOT/TASKS/_TEMPLATE" "$HOME/.claude/TASKS/"
$DRY cp "$ROOT/CLAUDE.md" "$ROOT/.claude/SKILLFLOW.md" "$ROOT/.kimi-code/SOCIAL_DICTIONARY.md" "$HOME/.claude/"
```

</scope>

<anti-scope>
- Реальный (не dry-run) запуск против `~/` — НЕ выполнять в этой таске; только по отдельной команде пользователя.
- Не синкаем `TASKS/001-...` (локальные фичи) — только `_TEMPLATE`.
- Коммиты — только после подтверждения пользователя.
</anti-scope>

<constraints>
- robocopy exit codes < 8 = успех — скрипт не должен падать на кодах 1-7 (при необходимости `if ($LASTEXITCODE -ge 8) { exit $LASTEXITCODE }`).
- Идемпотентность: повторный запуск не меняет состояние.
</constraints>

<acceptance-criteria>
- [ ] `powershell -File sync.ps1 -DryRun` — выводит план копирования, exit code < 8, ФС не изменена
- [ ] `./sync.sh --dry-run` — echo команд, никаких изменений
- [ ] Оба скрипта покрывают: agents/skills обеих сторон, TASKS/_TEMPLATE в оба home, SOCIAL_DICTIONARY.md в оба home, config.toml только в kimi, CLAUDE.md только в claude
- [ ] Изменённые файлы доложены; коммит только после подтверждения
</acceptance-criteria>

<refs>
- @../feature.md — requirements (sync), decisions (SOCIAL_DICTIONARY единый источник)
- @../plan.md — task 05
</refs>

<obstacles>
- Verbatim ps1 dry-run piped robocopy /L to Out-Null -> план не выводился (конфликт с acceptance-criteria). Убрал Out-Null в обеих ветках Mirror().
- В verbatim телах не было обработки robocopy exit codes (требуется constraints). Добавлено `if ($LASTEXITCODE -ge 8) { exit $LASTEXITCODE }` + финальный `exit 0`.
- sync.sh падал бы на холодном старте: `cp -r TASKS/_TEMPLATE` требует существующий родительский `TASKS/` в home. Добавлен `mkdir -p` (echo в dry-run).
- README.md таблица обещает деплой AGENTS.md в ~/.kimi-code — скрипты его не синкают (вне scope тела таски). README обновлён с caveat; решить: добавить AGENTS.md в скрипты или убрать строку из таблицы.
</obstacles>

<log>
2026-08-31: Созданы sync.ps1 (robocopy /MIR, -DryRun, exit-code guard >=8) и sync.sh (cp, --dry-run, mkdir -p). Dry-run обоих: exit 0, ФС не изменена, покрытие полное. README.md:62 обновлён (нота "planned" -> implemented + caveat про AGENTS.md). Реальный sync против ~/ НЕ выполнялся. Отчёт: .superpowers/sdd/001-dual-tool-repo-layout/task-5-report.md. Коммитов нет.
2026-08-31 (fix round): AGENTS.md добавлен в scope синка по решению пользователя — только в kimi home (claude получает CLAUDE.md). sync.ps1 +sync.sh обновлены, caveat из README.md убран, dry-run обоих: exit 0, AGENTS.md в kimi-плане, отсутствует в claude-плане.
2026-08-31 (rework): sync.ps1/sync.sh переименованы в install.ps1/install.sh + добавлены uninstall.ps1/uninstall.sh; deploy-set вынесен в manifest.json (единый источник, 13 entries: 7 kimi + 6 claude). install: skip home при отсутствии ~/.kimi-code|~/.claude, dirs через robocopy /MIR / cp -r, files через Copy-Item / cp, -DryRun/--dry-run без изменений ФС. uninstall: удаляет ровно deployed paths, чистит пустые родительские dirs (home root никогда не трогается), Read-Host-confirmation без -Force/--force. sync.ps1/sync.sh удалены; README.md и SYNC-STRUCTURE.md обновлены. Dry-run всех четырёх: exit 0. Отчёт: .superpowers/sdd/001-dual-tool-repo-layout/task-8-install-scripts-report.md. Коммитов нет.
</log>
