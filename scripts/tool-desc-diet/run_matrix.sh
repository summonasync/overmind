#!/usr/bin/env bash
# Run the intent matrix for ONE runtime against its own isolated Host + DB copy.
#
# Usage: run_matrix.sh <claude|qwen|kimi> [PORT]
# Results: $SCRATCH/results-<runtime>.tsv — runtime, intent, expected, picks, match, rc, elapsed_s
# RESUMABLE: rows already scored Y/N (a real observed pick) are kept; missing, NONE
# and ERROR rows are (re)run. Intents that time out with picks observed are scored
# from the picks (rc recorded), not discarded — the pick is the measurement.
set -u

HERE="$(cd "$(dirname "$0")" && pwd)"
SCRATCH="${SCRATCH:-/mnt/Ubuntu-Dev/scratch/overmind-234}"
RUNTIME="${1:?usage: run_matrix.sh <claude|qwen|kimi> [PORT]}"
PORT="${2:-}"

case "$RUNTIME" in
  claude) PORT="${PORT:-4799}" ;;
  qwen) PORT="${PORT:-4800}" ;;
  kimi) PORT="${PORT:-4801}" ;;
esac
API_URL="http://127.0.0.1:${PORT}"

export SCRATCH API_URL
export SCRATCH_DIR="$SCRATCH/iso-$RUNTIME"
export CFG="$SCRATCH/cfg-$RUNTIME/overmind.mcp.json"
export KIMI_HOME="$SCRATCH/cfg-$RUNTIME/kimi-home"
export QWEN_HOME="$SCRATCH/cfg-$RUNTIME/qwen-home"
export OUT_DIR="$SCRATCH/cfg-$RUNTIME"
export RUNS_DIR="$SCRATCH/runs/$RUNTIME"
mkdir -p "$RUNS_DIR"

trap 'echo "MATRIX_SIGNALED $RUNTIME — killing process group"; kill 0' INT TERM

OUT_DIR="$OUT_DIR" API_URL="$API_URL" bash "$HERE/make_mcp_configs.sh" >>"$SCRATCH/matrix-$RUNTIME-setup.log" 2>&1

if [[ "$RUNTIME" == qwen ]]; then
  # The qwen lane runs under a SCRATCH QWEN_HOME (exported above) — that generated,
  # server-stripped home IS the frozen baseline surface; T6 reuses the surviving
  # directory verbatim and must not re-copy from the real home or wipe it. Provision it
  # only when absent (fresh repro): copy settings.json with everything EXCEPT the
  # overmind entry stripped out of mcpServers, so grimoire/squeezr/mindlash/trello never
  # spawn under a probe (blast-radius fix, see baseline doc disclosure).
  mkdir -p "$QWEN_HOME"
  if [[ ! -f "$QWEN_HOME/settings.json" ]]; then
    python3 - "$HOME/.qwen/settings.json" "$QWEN_HOME/settings.json" <<'PY'
import json, sys
src, dst = sys.argv[1:3]
d = json.load(open(src))
d["mcpServers"] = {k: v for k, v in (d.get("mcpServers") or {}).items() if k == "overmind"}
json.dump(d, open(dst, "w"), indent=1)
print(f"provisioned {dst} with overmind-only mcpServers", file=sys.stderr)
PY
  fi
  # Probe memory hygiene: headless qwen sessions get a project memory slug per cwd and
  # can write matrix-aware notes that contaminate later intents (baseline doc incident
  # (c)). Wipe the probe-cwd slug before and after the lane.
  PROBE_SLUG="$HOME/.qwen/projects/-mnt-Ubuntu-Dev-scratch-overmind-234-probe-cwd"
  rm -rf "$PROBE_SLUG"
fi

# Never adopt a stranger on the lane port (it may be another lane's live host — measured
# 2026-11-16: 4799 held by lane 236's host). Reuse ONLY our own recorded, alive host
# whose port file matches this lane port; otherwise kill our stale host and boot ours.
if [[ -f "$SCRATCH_DIR/host.pid" ]] && kill -0 "$(cat "$SCRATCH_DIR/host.pid")" 2>/dev/null \
   && grep -q "$PORT" "$SCRATCH_DIR/host.port" 2>/dev/null \
   && curl -fsS -m 5 -o /dev/null "${API_URL}/api/health" 2>/dev/null; then
  echo "HOST_REUSED ${API_URL} (own pid $(cat "$SCRATCH_DIR/host.pid"))" >>"$SCRATCH/matrix-$RUNTIME-setup.log"
else
  # Kill OUR stale host only — verify the pid still runs this lane's host binary before
  # signalling (a recycled pid must never be hit).
  if [[ -f "$SCRATCH_DIR/host.pid" ]]; then
    pid="$(cat "$SCRATCH_DIR/host.pid")"
    if [[ -n "$pid" ]] && grep -qa "Overmind.Host" "/proc/$pid/cmdline" 2>/dev/null; then
      kill "$pid" 2>/dev/null
      sleep 1
    fi
  fi
  SCRATCH="$SCRATCH_DIR" API_URL="$API_URL" bash "$HERE/iso_host.sh" start "$PORT" >>"$SCRATCH/matrix-$RUNTIME-setup.log" 2>&1 \
    || { echo "ISO HOST FAILED — another host may own ${PORT}; free the lane port or pass a different one. See $SCRATCH_DIR/host.log" | tee -a "$SCRATCH/matrix-$RUNTIME-setup.log"; exit 1; }
fi

RESULTS="$SCRATCH/results-$RUNTIME.tsv"
if [[ ! -f "$RESULTS" ]]; then
  printf 'runtime\tintent\texpected\tpicks\tmatch\trc\telapsed_s\n' > "$RESULTS"
fi

TOTAL=$(($(wc -l < "$HERE/intents.tsv") - 1))
DONE=0
# fd 3 keeps the tsv off stdin: a headless runtime must never swallow the remaining
# intents (qwen 0.27.4 appends stdin to the prompt and ate the whole file at intent 01).
while IFS=$'\t' read -r -u 3 id expected prompt; do
  [[ "$id" == "id" ]] && continue
  if awk -F '\t' -v id="$id" '$2==id && ($5=="Y" || $5=="N") {found=1} END {exit !found}' "$RESULTS"; then
    DONE=$((DONE + 1))
    echo "--- $RUNTIME $id already scored, kept"
    continue
  fi
  echo "=== $RUNTIME $id expected=$expected"
  t0=$(date +%s)
  out="$(bash "$HERE/run_intent.sh" "$RUNTIME" "$id" 2>/dev/null </dev/null)"
  elapsed=$(( $(date +%s) - t0 ))
  rc="$(cat "$RUNS_DIR/$RUNTIME/$id.exit" 2>/dev/null || echo 990)"
  picks="$(echo "$out" | python3 -c '
import json, sys
picks = []
for line in sys.stdin:
    line = line.strip()
    if not line.startswith("{"):
        continue
    try:
        picks.append(json.loads(line)["pick"])
    except (KeyError, json.JSONDecodeError):
        pass
print(",".join(picks))
')"
  if [[ -z "$picks" && "$rc" != "0" ]]; then
    match="ERROR:$rc"
  elif [[ ",$picks," == *",$expected,"* ]]; then
    match="Y"
  elif [[ -z "$picks" ]]; then
    match="NONE"
  else
    match="N"
  fi
  echo "    picks=[$picks] match=$match rc=$rc elapsed=${elapsed}s"
  grep -v "^$RUNTIME	$id	" "$RESULTS" > "$RESULTS.tmp" || true
  printf '%s\t%s\t%s\t%s\t%s\t%s\t%s\n' "$RUNTIME" "$id" "$expected" "$picks" "$match" "$rc" "$elapsed" >> "$RESULTS.tmp"
  mv "$RESULTS.tmp" "$RESULTS"
  [[ "$match" == Y || "$match" == N ]] && DONE=$((DONE + 1))
done 3< "$HERE/intents.tsv"

SCRATCH="$SCRATCH_DIR" bash "$HERE/iso_host.sh" stop >> "$SCRATCH/matrix-$RUNTIME-setup.log" 2>&1
[[ "${PROBE_SLUG:-}" == "" ]] || rm -rf "$PROBE_SLUG"
SCORED=$(awk -F '\t' 'NR>1 && ($5=="Y" || $5=="N")' "$RESULTS" | wc -l)
if [[ "$SCORED" -ne "$TOTAL" ]]; then
  echo "MATRIX_INCOMPLETE $RUNTIME scored=$SCORED total=$TOTAL — rerun this command to resume"
  exit 1
fi
echo "MATRIX_DONE $RUNTIME"
grep -c $'\tY\t' "$RESULTS" | xargs -I{} echo "$RUNTIME matches {}/$TOTAL"
