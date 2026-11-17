#!/usr/bin/env bash
# Replay ONE fixed intent against ONE runtime and print the MCP tool it actually invoked.
#
# Usage: run_intent.sh <claude|qwen|kimi> <intent-id>
# Env: SCRATCH (default /mnt/Ubuntu-Dev/scratch/overmind-234), INTENTS (tsv path),
#      RUNS_DIR, CFG, KIMI_HOME, API_URL, PROJECT_ID (matrix exports lane values;
#      the defaults here are the shared-baseline ones). Prompts come verbatim from
#      intents.tsv so the dieted-build rerun (T6) replays byte-identical prompts.
set -euo pipefail

SCRATCH="${SCRATCH:-/mnt/Ubuntu-Dev/scratch/overmind-234}"
HERE="$(cd "$(dirname "$0")" && pwd)"
INTENTS="${INTENTS:-$HERE/intents.tsv}"
RUNS_DIR="${RUNS_DIR:-$SCRATCH/runs}"
CFG="${CFG:-$SCRATCH/cfg/overmind.mcp.json}"
KIMI_HOME="${KIMI_HOME:-$SCRATCH/cfg/kimi-home}"
API_URL="${API_URL:-http://127.0.0.1:4799}"
PROJECT_ID="${PROJECT_ID:-ff313f29-8144-488c-94e6-5ea5dde57bfd}"
TIMEOUT="${TIMEOUT:-240}"

runtime="${1:?usage: run_intent.sh <runtime> <intent-id>}"
intent_id="${2:?usage: run_intent.sh <runtime> <intent-id>}"

row="$(awk -F '\t' -v id="$intent_id" '$1==id {print}' "$INTENTS")"
[[ -n "$row" ]] || { echo "unknown intent $intent_id" >&2; exit 2; }
expected="$(echo "$row" | cut -f2)"
prompt="$(echo "$row" | cut -f3-)"

WORK="$SCRATCH/probe-cwd"
mkdir -p "$WORK" "$RUNS_DIR/$runtime"
LOG="$RUNS_DIR/$runtime/$intent_id.jsonl"
ERR="$RUNS_DIR/$runtime/$intent_id.err"

echo "INTENT $intent_id EXPECTED $expected RUNTIME $runtime CFG $CFG"

# Warm the lane host's health path before spawn: the adapter's readiness gate is a
# fixed 20s budget in code (src untouchable by anti-scope) and a cold first health
# probe can blow it, failing MCP server startup for the whole run.
curl -fsS -m 20 -o /dev/null "${API_URL}/api/health" || true

capture_rc() {
  local rc=0
  "$@" || rc=$?
  echo "$rc" > "$RUNS_DIR/$runtime/$intent_id.exit"
}

if [[ "$runtime" == qwen && "${TIMEOUT_SET:-}" == "" ]]; then
  TIMEOUT=420
fi

case "$runtime" in
  claude)
    capture_rc env -C "$WORK" timeout "$TIMEOUT" claude -p "$prompt" \
        --output-format stream-json --verbose \
        --strict-mcp-config --mcp-config "$CFG" \
        --allowedTools "mcp__overmind" \
        --max-turns 6 \
        >"$LOG" 2>"$ERR" </dev/null
    ;;
  qwen)
    # qwen surface notes (2026-11-16, each verified the hard way):
    # - NO --allowed-mcp-server-names: the gate blocked the cfg-provided overmind
    #   server (surfaced as "failed to start"); the model then improvised its own
    #   stdio harness and reported results as if it had used the board.
    # - NO --exclude-tools: it silently drops every declared mcp__* tool.
    # - --mcp-config top-tier merge replaces the live settings "overmind" server
    #   by name; the lane runs under the SCRATCH QWEN_HOME exported by run_matrix.sh —
    #   that generated home IS the frozen qwen baseline surface (T6 reuses it verbatim,
    #   never the real home: surface change + real-project-memory contamination).
    # - headless qwen defers the call: tool_search -> tool_call{name:mcp__overmind__X};
    #   extract_picks.py unwraps it and counts the search step on stderr.
    capture_rc env -C "$WORK" timeout "$TIMEOUT" qwen "$prompt" \
        --output-format stream-json \
        --mcp-config "$CFG" \
        --approval-mode yolo \
        >"$LOG" 2>"$ERR" </dev/null
    ;;
  kimi)
    capture_rc env -C "$WORK" KIMI_CODE_HOME="$KIMI_HOME" timeout "$TIMEOUT" \
        kimi -p "$prompt" --output-format stream-json \
        >"$LOG" 2>"$ERR" </dev/null
    ;;
  *) echo "unknown runtime $runtime" >&2; exit 2 ;;
esac

python3 "$HERE/extract_picks.py" --toolnames "$SCRATCH/out/wire.json" "$LOG"
