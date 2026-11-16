#!/usr/bin/env bash
# Isolated Host for the tool-desc-diet smoke matrix.
# Copies the live DB into a scratch dir, disables materialization on the COPY,
# boots a private Overmind.Host on a spare port. The live board is never touched.
#
# Usage: iso_host.sh start [PORT]   -> prints HOST_READY <url> once the API answers
#        iso_host.sh stop           -> kills the private host
set -euo pipefail

SCRATCH="${SCRATCH:-/mnt/Ubuntu-Dev/scratch/overmind-234/iso}"
REPO_ROOT="${REPO_ROOT:-$(cd "$(dirname "$0")/../.." && pwd)}"
HOST_BIN="${HOST_BIN:-/mnt/Ubuntu-Dev/scratch/overmind-234/host/Overmind.Host}"
LIVE_DB="${LIVE_DB:-$HOME/.overmind/overmind.db}"
PORT="${2:-4799}"
URL="http://127.0.0.1:${PORT}"

start() {
  mkdir -p "$SCRATCH"
  python3 - "$LIVE_DB" "$SCRATCH/overmind.db" <<'PY'
import sqlite3, sys
src, dst = sys.argv[1], sys.argv[2]
dst_s = sqlite3.connect(dst)
try:
    src_s = sqlite3.connect(f"file:{src}?mode=ro", uri=True)
except sqlite3.Error:
    src_s = sqlite3.connect(src)
src_s.backup(dst_s)
dst_s.execute("CREATE TABLE IF NOT EXISTS AppSettings (Key TEXT NOT NULL PRIMARY KEY, Value TEXT NOT NULL)")
dst_s.execute("INSERT INTO AppSettings (Key, Value) VALUES ('planning.materializationDefault', 'off') "
              "ON CONFLICT(Key) DO UPDATE SET Value='off'")
dst_s.commit()
print("db copied, materialization forced off on the copy", file=sys.stderr)
PY
  env \
    Overmind__DatabasePath="$SCRATCH/overmind.db" \
    Overmind__BindUrl="$URL" \
    Overmind__PortFilePath="$SCRATCH/host.port" \
    setsid "$HOST_BIN" --no-tray >"$SCRATCH/host.log" 2>&1 < /dev/null &
  echo $! > "$SCRATCH/host.pid"
  for _ in $(seq 1 60); do
    if curl -fsS -o /dev/null "${URL}/api/health" 2>/dev/null; then
      echo "HOST_READY ${URL}"
      exit 0
    fi
    if ! kill -0 "$(cat "$SCRATCH/host.pid")" 2>/dev/null; then
      echo "HOST_DIED — see $SCRATCH/host.log" >&2
      tail -20 "$SCRATCH/host.log" >&2 || true
      exit 1
    fi
    sleep 1
  done
  echo "HOST_TIMEOUT — see $SCRATCH/host.log" >&2
  exit 1
}

stop() {
  if [[ -f "$SCRATCH/host.pid" ]]; then
    pid="$(cat "$SCRATCH/host.pid")"
    # Never signal a recycled pid: only kill if it still runs Overmind.Host.
    if [[ -n "$pid" ]] && grep -qa "Overmind.Host" "/proc/$pid/cmdline" 2>/dev/null; then
      kill "$pid"
      sleep 1
      kill -9 "$pid" 2>/dev/null || true
    fi
  fi
  rm -f "$SCRATCH/host.pid"
  echo "HOST_STOPPED"
}

case "${1:-}" in
  start) start ;;
  stop) stop ;;
  *) echo "usage: iso_host.sh start [PORT] | stop" >&2; exit 2 ;;
esac
