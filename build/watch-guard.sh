#!/usr/bin/env bash
#
# watch-guard.sh — keep the Host and MCP adapter cheap on inotify watches (Linux smoke guard).
#
# Usage: build/watch-guard.sh [adapter-bin] [host-bin]
#
# Host cell: boots on a throwaway port with an isolated HOME, dummy web root and a 300-dir
# cwd tree, then must hold < 100 watches. That throwaway host doubles as the adapter cell's
# API stub — an unreachable API kills the adapter at its pre-boot health check, before any
# file watches exist, and would pass vacuously with zero — so the adapter starts from the
# same tree with a live stub and must hold < 25 watches. Non-Linux prints SKIP, exits 0.

set -euo pipefail

die() {
    echo "watch-guard.sh: $*" >&2
    exit 1
}

usage() {
    sed -n '3,9p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    exit 2
}

[[ $(uname -s) == Linux ]] || { echo "watch-guard.sh: SKIP (non-Linux: $(uname -s))"; exit 0; }
command -v curl >/dev/null 2>&1 || die "curl is required but not on PATH"

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
repo_root=$(cd "$script_dir/.." && pwd)

adapter_bin=${1:-$repo_root/src/Overmind.Mcp/bin/Release/net10.0/linux-x64/overmind-mcp}
host_bin=${2:-$repo_root/src/Overmind.Host/bin/Release/net10.0/linux-x64/Overmind.Host}
host_threshold=100
adapter_threshold=25

[[ -x $adapter_bin ]] || die "adapter binary not found or not executable: $adapter_bin"
[[ -x $host_bin ]] || die "host binary not found or not executable: $host_bin"

work=$(mktemp -d /tmp/overmind-watch-guard.XXXXXX)
host_pid=""
adapter_pid=""
web=$work/web
tree=$work/tree
mkdir -p "$work/home" "$web" "$tree"
echo '<!doctype html><title>guard</title>' >"$web/index.html"
p=$tree
for i in $(seq 1 300); do p=$p/d$i; done
mkdir -p "$p"

cleanup() {
    if [[ -n $adapter_pid ]] && kill -0 "$adapter_pid" 2>/dev/null; then
        kill -TERM "$adapter_pid" 2>/dev/null || true
        wait "$adapter_pid" 2>/dev/null || true
    fi
    if [[ -n $host_pid ]] && kill -0 "$host_pid" 2>/dev/null; then
        kill -TERM "$host_pid" 2>/dev/null || true
        wait "$host_pid" 2>/dev/null || true
    fi
    rm -rf "$work"
}
trap cleanup EXIT

inotify_watches() {
    local pid=$1 fd target count total=0
    [[ -d /proc/$pid/fd ]] || return 1
    for fd in /proc/$pid/fd/*; do
        target=$(readlink "$fd" 2>/dev/null || true)
        [[ $target == anon_inode:inotify ]] || continue
        count=$(grep -c '^inotify' "/proc/$pid/fdinfo/${fd##*/}" 2>/dev/null || echo 0)
        total=$((total + count))
    done
    echo "$total"
}

free_port() {
    local candidate
    for _ in $(seq 1 50); do
        candidate=$((RANDOM % 20000 + 20000))
        if ! (exec 3<>"/dev/tcp/127.0.0.1/$candidate") 2>/dev/null; then
            echo "$candidate"
            return 0
        fi
    done
    die "no free throwaway port found"
}

port=$(free_port)

echo "watch-guard: host cell (port=$port, cwd=300-dir tree)"
(
    cd "$tree"
    HOME="$work/home" Overmind__BindUrl="http://127.0.0.1:$port" OVERMIND_WEB_ROOT="$web" \
        exec "$host_bin" --no-tray
) >"$work/host.log" 2>&1 &
host_pid=$!

host_up=""
for _ in $(seq 1 90); do
    if curl -fsS -m 2 "http://127.0.0.1:$port/api/health" >/dev/null 2>&1; then
        host_up=1
        break
    fi
    if ! kill -0 "$host_pid" 2>/dev/null; then
        tail -30 "$work/host.log" >&2
        die "host exited before answering /api/health"
    fi
    sleep 1
done
[[ -n $host_up ]] || { tail -30 "$work/host.log" >&2; die "host did not answer /api/health within 90s"; }

sleep 1
host_watches=$(inotify_watches "$host_pid" || true)
[[ $host_watches =~ ^[0-9]+$ ]] || die "host pid $host_pid vanished before the watch count"
[[ $host_watches -lt $host_threshold ]] \
    || die "host holds $host_watches inotify watches (threshold < $host_threshold)"
echo "watch-guard: host cell ok ($host_watches watches < $host_threshold)"

echo "watch-guard: adapter cell (cwd=300-dir tree, API stub=guard host on $port)"
(
    cd "$tree"
    HOME="$work/home" OVERMIND_API_URL="http://127.0.0.1:$port" exec "$adapter_bin"
) >"$work/adapter.log" 2>&1 < <(sleep 30) &
adapter_pid=$!

adapter_watches=""
for _ in $(seq 1 20); do
    if ! kill -0 "$adapter_pid" 2>/dev/null; then
        tail -30 "$work/adapter.log" >&2
        die "adapter exited before its watches could be counted"
    fi
    sample=$(inotify_watches "$adapter_pid" || true)
    if [[ $sample =~ ^[0-9]+$ ]]; then
        adapter_watches=$sample
        [[ $adapter_watches -gt 0 ]] && break
    fi
    sleep 0.5
done
[[ -n $adapter_watches ]] || die "could not sample the adapter watch count"

kill -TERM "$adapter_pid" 2>/dev/null || true
wait "$adapter_pid" 2>/dev/null || true
adapter_pid=""

[[ $adapter_watches -lt $adapter_threshold ]] \
    || die "adapter holds $adapter_watches inotify watches (threshold < $adapter_threshold)"
echo "watch-guard: adapter cell ok ($adapter_watches watches < $adapter_threshold)"

kill -TERM "$host_pid"
wait "$host_pid" 2>/dev/null || true
host_pid=""

echo "WATCH GUARD OK: host=$host_watches (<$host_threshold) adapter=$adapter_watches (<$adapter_threshold)"
