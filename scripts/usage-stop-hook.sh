#!/usr/bin/env bash
#
# usage-stop-hook.sh — Claude Code Stop hook reporting per-turn token usage to Overmind.
#
# Reads the Stop event JSON from stdin, sums the assistant usage appended to the session
# transcript since the previous Stop, and posts one record to POST /api/usage. Any failure
# (no Overmind host, no transcript, malformed JSON, missing jq/curl) exits 0 silently —
# the hook must never block an agent session.
#
# Manual setup, human-only door: add to ~/.claude/settings.json
#   "hooks": { "Stop": [ { "hooks": [ { "type": "command",
#     "command": "bash /absolute/path/to/usage-stop-hook.sh" } ] } ] }
#
# OVERMIND_HOST overrides the base URL (default http://127.0.0.1:4761).
# OVERMIND_ALIAS overrides the reporting alias (default: git user.name, then "unknown").

set -uo pipefail

host="${OVERMIND_HOST:-http://127.0.0.1:4761}"

command -v jq >/dev/null 2>&1 || exit 0
command -v curl >/dev/null 2>&1 || exit 0

event=$(cat) 2>/dev/null || exit 0
[ -n "$event" ] || exit 0

session_id=$(jq -r '.session_id // empty' <<< "$event") || exit 0
transcript=$(jq -r '.transcript_path // empty' <<< "$event") || exit 0
[ -n "$session_id" ] && [ -n "$transcript" ] && [ -f "$transcript" ] || exit 0

state_dir="${XDG_CACHE_HOME:-$HOME/.cache}/overmind/usage-hook"
state_file="$state_dir/$session_id.marker"
mkdir -p "$state_dir" 2>/dev/null || exit 0

last_marker=0
if [ -f "$state_file" ]; then
    read -r last_marker < "$state_file" 2>/dev/null || last_marker=0
fi
[[ "$last_marker" =~ ^[0-9]+$ ]] || last_marker=0

total_lines=$(wc -l < "$transcript" 2>/dev/null) || exit 0
[[ "$total_lines" =~ ^[0-9]+$ ]] || exit 0
[ "$total_lines" -gt "$last_marker" ] || exit 0

window=$(tail -n "+$((last_marker + 1))" "$transcript" 2>/dev/null) || exit 0
[ -n "$window" ] || exit 0

usage_rows=$(jq -c 'select(.type == "assistant" and (.message.usage? != null))' <<< "$window" 2>/dev/null) || usage_rows=""
[ -n "$usage_rows" ] || { printf '%s\n' "$total_lines" > "$state_file" || exit 0; exit 0; }

read -r input_tokens output_tokens cache_read cache_creation model <<< "$(jq -rs '
    (map(.message.model)[0] // "unknown") as $m |
    [
        (map(.message.usage.input_tokens // 0) | add // 0),
        (map(.message.usage.output_tokens // 0) | add // 0),
        (map(.message.usage.cache_read_input_tokens // 0) | add // 0),
        (map(.message.usage.cache_creation_input_tokens // 0) | add // 0),
        $m
    ] | @tsv' <<< "$usage_rows" 2>/dev/null)" || exit 0

[ -n "${input_tokens:-}" ] || exit 0

alias="${OVERMIND_ALIAS:-$(git config user.name 2>/dev/null)}"
alias="${alias:-unknown}"
alias="${alias//[^[:alnum:]_.-]/_}"

status=$(curl -s -m 5 -o /dev/null -w '%{http_code}' -X POST \
    -H 'Content-Type: application/json' \
    -d "$(jq -rn --arg alias "$alias" --arg sessionId "$session_id" --arg model "$model" \
        --arg ts "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
        --argjson input "${input_tokens:-0}" --argjson output "${output_tokens:-0}" \
        --argjson cacheRead "${cache_read:-0}" --argjson cacheWrite "${cache_creation:-0}" \
        '{alias: $alias, sessionId: $sessionId, ts: $ts, model: $model,
          inputTokens: $input, outputTokens: $output,
          cacheReadTokens: $cacheRead, cacheWriteTokens: $cacheWrite}')" \
    "$host/api/usage" 2>/dev/null) || exit 0

[[ "$status" == 2* ]] || exit 0

printf '%s\n' "$total_lines" > "$state_file" 2>/dev/null || exit 0
exit 0
