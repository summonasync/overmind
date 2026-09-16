#!/usr/bin/env bash
#
# run.sh — launch the Overmind host from an unpacked distribution folder.
# The packed layout keeps web assets under ./web, so OVERMIND_WEB_ROOT is pinned here.

set -euo pipefail

app_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
export OVERMIND_WEB_ROOT="$app_dir/web"
exec "$app_dir/host/Overmind.Host" "$@"
