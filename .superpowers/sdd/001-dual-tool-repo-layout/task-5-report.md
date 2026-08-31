# Task 001/05 — sync.ps1 + sync.sh — Implementation Report

## Status: DONE_WITH_CONCERNS

## What was done
- Created `sync.ps1` (repo root) — PowerShell deploy via robocopy `/MIR`, `-DryRun` switch.
- Created `sync.sh` (repo root) + `chmod +x` — bash equivalent via `cp`, `--dry-run` flag.
- Updated `README.md:62` — removed stale "planned, may not exist yet" note, added AGENTS.md caveat.

## Deviations from the verbatim script bodies in the task file
All deviations are driven by the task's own `<constraints>` / `<acceptance-criteria>`:

1. **robocopy exit-code handling (required by constraints).** The verbatim body had no exit-code check. Added inside `Mirror()`:
   ```powershell
   if ($LASTEXITCODE -ge 8) { exit $LASTEXITCODE }
   ```
   Plus a trailing `exit 0` so a successful run always returns 0 (robocopy's 1-7 would otherwise leak as the script's exit code via `$LASTEXITCODE` semantics when the caller inspects it).
2. **Dry-run robocopy output no longer piped to `Out-Null`.** The verbatim dry-run branch (`robocopy ... /L ... | Out-Null`) printed nothing, violating acceptance criterion "выводит план копирования". Now dry-run shows the robocopy `/L` summary tables. Real-run branch kept `| Out-Null` removed too — actually both branches print the summary; silent mirroring hid failures. If silent real-run is wanted, re-add `| Out-Null` to the `else` branch only.
3. **sync.sh: added `mkdir -p "$HOME/.kimi-code/TASKS" "$HOME/.claude/TASKS"`** before the `cp -r TASKS/_TEMPLATE` lines. `cp -r src dest/` fails when the parent `TASKS/` dir does not exist in a fresh home; without this the verbatim script breaks on first real run (non-idempotent on cold start). In dry-run mode the mkdir is echoed like everything else.

No other changes to the prescribed logic.

## Final script contents

### sync.ps1
```powershell
param([switch]$DryRun)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$kimiHome = Join-Path $HOME '.kimi-code'
$claudeHome = Join-Path $HOME '.claude'

function Mirror($src, $dst) {
    if ($DryRun) { robocopy $src $dst /MIR /L /NFL /NDL /NJH }
    else { robocopy $src $dst /MIR /NFL /NDL /NJH }
    if ($LASTEXITCODE -ge 8) { exit $LASTEXITCODE }
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
exit 0
```

### sync.sh
```bash
#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DRY=""
[[ "${1:-}" == "--dry-run" ]] && DRY="echo"

$DRY mkdir -p "$HOME/.kimi-code/TASKS" "$HOME/.claude/TASKS"
$DRY cp -r "$ROOT/.kimi-code/agents" "$ROOT/.kimi-code/skills" "$HOME/.kimi-code/"
$DRY cp -r "$ROOT/TASKS/_TEMPLATE" "$HOME/.kimi-code/TASKS/"
$DRY cp "$ROOT/.kimi-code/SOCIAL_DICTIONARY.md" "$ROOT/.kimi-code/SKILLFLOW.md" "$ROOT/.kimi-code/config.toml" "$HOME/.kimi-code/"
$DRY cp -r "$ROOT/.claude/agents" "$ROOT/.claude/skills" "$HOME/.claude/"
$DRY cp -r "$ROOT/TASKS/_TEMPLATE" "$HOME/.claude/TASKS/"
$DRY cp "$ROOT/CLAUDE.md" "$ROOT/.claude/SKILLFLOW.md" "$ROOT/.kimi-code/SOCIAL_DICTIONARY.md" "$HOME/.claude/"
```

## Dry-run outputs (actual)

### `powershell -File sync.ps1 -DryRun` → exit 0
Six robocopy `/L` summary tables (agents: 18 files; skills: 21 dirs / 20 files; _TEMPLATE: 4 files — kimi side; then 18 / 21+20 / 4 — claude side, all "Copied" there since ~/.claude targets were empty) plus:
```
Would copy ...\.kimi-code\SOCIAL_DICTIONARY.md -> C:\Users\User\.kimi-code
Would copy ...\.kimi-code\SKILLFLOW.md -> C:\Users\User\.kimi-code
Would copy ...\.kimi-code\config.toml -> C:\Users\User\.kimi-code
Would copy ...\CLAUDE.md -> C:\Users\User\.claude
Would copy ...\.claude\SKILLFLOW.md -> C:\Users\User\.claude
Would copy ...\.kimi-code\SOCIAL_DICTIONARY.md -> C:\Users\User\.claude
Dry run complete - no changes made.
```
Full tables in conversation above; robocopy `/L` never writes, FS unchanged. All six robocopy invocations exited 0/1 (<8).

### `bash sync.sh --dry-run` → exit 0
```
mkdir -p /c/Users/User/.kimi-code/TASKS /c/Users/User/.claude/TASKS
cp -r .../.kimi-code/agents .../.kimi-code/skills /c/Users/User/.kimi-code/
cp -r .../TASKS/_TEMPLATE /c/Users/User/.kimi-code/TASKS/
cp .../SOCIAL_DICTIONARY.md .../SKILLFLOW.md .../config.toml /c/Users/User/.kimi-code/
cp -r .../.claude/agents .../.claude/skills /c/Users/User/.claude/
cp -r .../TASKS/_TEMPLATE /c/Users/User/.claude/TASKS/
cp .../CLAUDE.md .../.claude/SKILLFLOW.md .../.kimi-code/SOCIAL_DICTIONARY.md /c/Users/User/.claude/
```
Pure echo, no filesystem changes. Real (non-dry-run) sync was NOT executed per anti-scope.

## Acceptance criteria
- [x] `powershell -File sync.ps1 -DryRun` prints copy plan, exit code 0 (<8), FS unchanged.
- [x] `./sync.sh --dry-run` echoes commands only, no changes.
- [x] Coverage: agents+skills both sides ✔, TASKS/_TEMPLATE to both homes ✔, SOCIAL_DICTIONARY.md to both homes ✔, config.toml only to kimi ✔, CLAUDE.md only to claude ✔.
- [x] Changed files reported here; no commit made.

## Concerns
1. **README mapping table vs scripts:** `README.md:57` lists `AGENTS.md → ~/.kimi-code/AGENTS.md`, but the task-05 contract does not sync `AGENTS.md` (and no script line for it). Scripts follow the contract; README now carries a caveat note. Decide whether to add AGENTS.md to sync.ps1/sync.sh or drop the README row.
2. **Real-run robocopy output is visible** (deviation 2). If noise matters, re-add `| Out-Null` to the non-dry-run branch — exit-code check is unaffected.
3. **Real sync untested** (by design, anti-scope). First real run against `~/` should be user-triggered.
4. `sync.sh` is Git-Bash/Linux-oriented; on macOS/Linux `cp -r` semantics for trailing dirs are fine, but the script is not idempotent-safe against stale files the way robocopy `/MIR` is (cp never deletes extras). Acceptable per task context ("*nix-эквивалент на cp"), but parity note: deletions only propagate on Windows.

---

# Fix Round — AGENTS.md added to sync scope (user ruling)

## Status: DONE

## Diff summary
- `sync.ps1`: added `Copy-One (Join-Path $root 'AGENTS.md') $kimiHome` after the config.toml line (kimi home only; claude side untouched — it gets CLAUDE.md).
- `sync.sh`: appended `"$ROOT/AGENTS.md"` to the kimi-home `cp` line (SOCIAL_DICTIONARY/SKILLFLOW/config.toml line); claude line unchanged.
- `README.md`: removed the caveat sentence ("AGENTS.md is listed above but is not deployed by the scripts...") — mapping table row `AGENTS.md → ~/.kimi-code/AGENTS.md` is now accurate as-is.
- `TASKS/001-dual-tool-repo-layout/tasks/05-sync-scripts.md`: one line appended to `<log>` recording the ruling.

## Dry-run outputs (fix round)

### `powershell -File sync.ps1 -DryRun` → exit 0
```
Would copy ...\.kimi-code\SOCIAL_DICTIONARY.md -> C:\Users\User\.kimi-code
Would copy ...\.kimi-code\SKILLFLOW.md -> C:\Users\User\.kimi-code
Would copy ...\.kimi-code\config.toml -> C:\Users\User\.kimi-code
Would copy ...\AGENTS.md -> C:\Users\User\.kimi-code        <-- new, kimi only
Would copy ...\CLAUDE.md -> C:\Users\User\.claude
Would copy ...\.claude\SKILLFLOW.md -> C:\Users\User\.claude
Would copy ...\.kimi-code\SOCIAL_DICTIONARY.md -> C:\Users\User\.claude
Dry run complete - no changes made.
```
AGENTS.md present in the kimi plan, absent from the claude plan. FS unchanged.

### `bash sync.sh --dry-run` → exit 0
```
mkdir -p ~/.kimi-code/TASKS ~/.claude/TASKS
cp -r .kimi-code/agents .kimi-code/skills ~/.kimi-code/
cp -r TASKS/_TEMPLATE ~/.kimi-code/TASKS/
cp SOCIAL_DICTIONARY.md SKILLFLOW.md config.toml AGENTS.md -> ~/.kimi-code/   <-- AGENTS.md added here
cp -r .claude/agents .claude/skills ~/.claude/
cp -r TASKS/_TEMPLATE ~/.claude/TASKS/
cp CLAUDE.md .claude/SKILLFLOW.md SOCIAL_DICTIONARY.md -> ~/.claude/           <-- no AGENTS.md
```
Pure echo, no filesystem changes. Real sync against `~/` still NOT executed (anti-scope). No commits.

---

## Fix round — spec v5 (AGENTS.md / CLAUDE.md moved into tool dirs)

### Commands
- `git mv AGENTS.md .kimi-code/AGENTS.md` → staged as `R AGENTS.md -> .kimi-code/AGENTS.md`
- `git mv CLAUDE.md .claude/CLAUDE.md` (already index-added; `git add -N` not needed) → staged as `A .claude/CLAUDE.md`
- Layout-note edits in `.kimi-code/AGENTS.md` and `.claude/CLAUDE.md` (line 3 each): files now live inside their tool dirs.
- `SYNC-STRUCTURE.md`: intro line → `` `.kimi-code/` incl. `AGENTS.md` `` / `` `.claude/` incl. `CLAUDE.md` ``; mapping row → `` `.kimi-code/AGENTS.md` `` ↔ `` `.claude/CLAUDE.md` ``.
- `README.md`: intro, layout tree (AGENTS.md under `.kimi-code/`, CLAUDE.md under `.claude/`), deploy table (both files folded into their home rows), editing-rule and crew-docs references updated.
- `sync.ps1`: `Copy-One (Join-Path $root '.kimi-code/AGENTS.md') $kimiHome`; `Copy-One (Join-Path $root '.claude/CLAUDE.md') $claudeHome`.
- `sync.sh`: sources now `$ROOT/.kimi-code/AGENTS.md` and `$ROOT/.claude/CLAUDE.md`.

### Dry-runs
- `powershell -File sync.ps1 -DryRun` — exit 0; plan shows `.kimi-code/AGENTS.md -> ~/.kimi-code` and `.claude/CLAUDE.md -> ~/.claude`; robocopy /L listing only, "Dry run complete - no changes made."
- `bash sync.sh --dry-run` — exit 0; echoed plan includes `.kimi-code/AGENTS.md` into `~/.kimi-code/` and `.claude/CLAUDE.md` into `~/.claude/`; echo-only, no FS writes.

### Verification greps
- `ls AGENTS.md CLAUDE.md` → "No such file or directory" for both (root clean).
- `grep -rni 'kimi\|k2\.7\|\bk3\b\|config\.toml' .claude/CLAUDE.md` → exit 1 (no hits).
- `grep -ni 'claude' .kimi-code/AGENTS.md` → 1 hit, the layout note only (line 3).
- No commit performed. TASKS/ untouched by this round.
