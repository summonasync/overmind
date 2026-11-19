#!/usr/bin/env bash
#
# smoke.sh — unpack a linux-x64 Overmind archive and verify the first boot end-to-end.
#
# Usage: build/smoke.sh <overmind-<version>-linux-x64.tar.gz>
#
# Boots the Host with an isolated HOME and --no-tray, waits for ~/.overmind/host.port, then
# asserts: health ok, every bundled kit from build/dist-layout.env seeded as bundled with its
# mandatory flag, no legacy kit resurrected, web UI served at /, seed log line present, plus a
# watch-guard cell bounding Host/adapter inotify usage. SIGTERM shutdown.

set -euo pipefail

die() {
    echo "smoke.sh: $*" >&2
    exit 1
}

usage() {
    sed -n '3,9p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    exit 2
}

[[ $# -eq 1 ]] || usage
archive=$1
[[ -f $archive ]] || die "archive not found: $archive"
[[ $(basename "$archive") =~ ^overmind-[0-9.]+-linux-x64\.tar\.gz$ ]] || die "expected a overmind-<version>-linux-x64.tar.gz archive"

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

for tool in tar curl jq; do
    command -v "$tool" >/dev/null 2>&1 || die "$tool is required but not on PATH"
done

work=$(mktemp -d /tmp/overmind-smoke.XXXXXX)
home=$(mktemp -d /tmp/overmind-smoke-home.XXXXXX)
host_pid=""

cleanup() {
    if [[ -n $host_pid ]] && kill -0 "$host_pid" 2>/dev/null; then
        kill -TERM "$host_pid" 2>/dev/null || true
        wait "$host_pid" 2>/dev/null || true
    fi
    chmod -R u+w "$work" 2>/dev/null || true
    rm -rf "$work" "$home"
}
trap cleanup EXIT

tar -xzf "$archive" -C "$work"
root=$(find "$work" -mindepth 1 -maxdepth 1 -type d)
[[ -n $root ]] || die "archive did not unpack to a single top-level folder"
[[ -x $root/run.sh ]] || die "run.sh missing or not executable in the archive"

HOME=$home "$root/run.sh" --no-tray >"$home/host.log" 2>&1 &
host_pid=$!

port=""
for _ in $(seq 1 120); do
    if [[ -f $home/.overmind/host.port ]]; then
        port=$(tr -d '[:space:]' <"$home/.overmind/host.port")
        [[ -n $port ]] && break
    fi
    if ! kill -0 "$host_pid" 2>/dev/null; then
        tail -30 "$home/host.log" >&2
        die "host exited before writing the port file"
    fi
    sleep 1
done
[[ -n $port ]] || { tail -30 "$home/host.log" >&2; die "no port file at $home/.overmind/host.port after 120s"; }

health=$(curl -fsS "http://127.0.0.1:$port/api/health")
grep -q '"status":"ok"' <<<"$health" || die "health endpoint did not answer ok: $health"

kits=$(curl -fsS "http://127.0.0.1:$port/api/kits")
# shellcheck source=/dev/null
source "$script_dir/dist-layout.env"
seeded_count=0
for kit in $BUNDLED_KITS; do
    row=$(jq -c --arg kit "$kit" '.[] | select(.name == $kit)' <<<"$kits")
    [[ -n $row ]] || die "bundled kit '$kit' missing from /api/kits: $kits"
    [[ $(jq -r '.sourceKind' <<<"$row") == bundled ]] || die "kit '$kit' is not seeded as bundled: $row"
    expected_mandatory=false
    for mandatory in ${MANDATORY_KITS:-}; do
        [[ $mandatory == "$kit" ]] && expected_mandatory=true
    done
    [[ $(jq -r '.mandatory' <<<"$row") == "$expected_mandatory" ]] ||
        die "kit '$kit' mandatory flag was $(jq -r '.mandatory' <<<"$row"), expected $expected_mandatory"
    seeded_count=$((seeded_count + 1))
done
for kit in ${LEGACY_KITS:-}; do
    if jq -e --arg kit "$kit" 'any(.[]; .name == $kit)' <<<"$kits" >/dev/null; then
        die "legacy kit '$kit' came back in a freshly seeded library: $kits"
    fi
done

curl -fsS "http://127.0.0.1:$port/" | grep -qi '<!doctype html' || die "web UI not served at /"

grep -q "Seeded bundled kits" "$home/host.log" || die "seed log line missing from host.log"

kill -TERM "$host_pid"
wait "$host_pid" || true
host_pid=""

"$script_dir/watch-guard.sh" "$root/mcp/overmind-mcp" "$root/host/Overmind.Host"

echo "SMOKE OK: port=$port health=ok bundled kits seeded=$seeded_count mandatory flags honoured web UI served watch guard ok clean shutdown"
