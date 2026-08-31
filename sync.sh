#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DRY=""
[[ "${1:-}" == "--dry-run" ]] && DRY="echo"

$DRY mkdir -p "$HOME/.kimi-code/TASKS" "$HOME/.claude/TASKS"
$DRY cp -r "$ROOT/.kimi-code/agents" "$ROOT/.kimi-code/skills" "$HOME/.kimi-code/"
$DRY cp -r "$ROOT/TASKS/_TEMPLATE" "$HOME/.kimi-code/TASKS/"
$DRY cp "$ROOT/.kimi-code/SOCIAL_DICTIONARY.md" "$ROOT/.kimi-code/SKILLFLOW.md" "$ROOT/.kimi-code/config.toml" "$ROOT/.kimi-code/AGENTS.md" "$HOME/.kimi-code/"
$DRY cp -r "$ROOT/.claude/agents" "$ROOT/.claude/skills" "$HOME/.claude/"
$DRY cp -r "$ROOT/TASKS/_TEMPLATE" "$HOME/.claude/TASKS/"
$DRY cp "$ROOT/.claude/CLAUDE.md" "$ROOT/.claude/SKILLFLOW.md" "$ROOT/.kimi-code/SOCIAL_DICTIONARY.md" "$HOME/.claude/"
