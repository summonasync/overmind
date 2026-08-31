#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DRY=0
for arg in "$@"; do
    case "$arg" in
        --dry-run) DRY=1 ;;
        *) echo "Unknown argument: $arg (supported: --dry-run)" >&2; exit 2 ;;
    esac
done

have_python() { command -v python >/dev/null 2>&1 && python -c "" >/dev/null 2>&1; }

home_path() { case "$1" in kimi) echo "$HOME/.kimi-code";; claude) echo "$HOME/.claude";; esac }

parse_manifest() {
    if have_python; then
        (cd "$ROOT" && python -c "
import json
m = json.load(open('manifest.json'))
for e in m['entries']:
    print(e['source'], e['home'], e['type'])
" | tr -d '\r')
    else
        grep -oE '"source": *"[^"]+"|"home": *"[^"]+"|"type": *"[^"]+"' "$ROOT/manifest.json" \
          | sed -E 's/"(source|home|type)": *"([^"]+)"/\2/' \
          | paste -d' ' - - -
    fi
}

declare -A HOME_OK=()
entries="$(parse_manifest)" || { echo "Failed to parse manifest.json" >&2; exit 1; }
[[ -n "$entries" ]] || { echo "manifest.json parsed to zero entries" >&2; exit 1; }
while read -r src home type; do
    hp="$(home_path "$home")"
    if [[ -z "${HOME_OK[$home]:-}" ]]; then
        if [[ -d "$hp" ]]; then HOME_OK[$home]=1; else HOME_OK[$home]=0; echo "Home $hp not found, skipping its entries."; fi
    fi
    [[ "${HOME_OK[$home]}" == "0" ]] && continue

    name="$(basename "$src")"
    if [[ "$type" == "dir" ]]; then
        if command -v rsync >/dev/null 2>&1; then
            if [[ $DRY == 1 ]]; then echo "Would rsync -a --delete $ROOT/$src/ -> $hp/$name/"
            else mkdir -p "$hp/$name" && rsync -a --delete "$ROOT/$src/" "$hp/$name/"; fi
        else
            # cp fallback is additive only: stale files in $hp/$name are NOT deleted (unlike robocopy /MIR on Windows)
            if [[ $DRY == 1 ]]; then echo "Would copy -r $ROOT/$src/. -> $hp/$name/"
            else mkdir -p "$hp/$name" && cp -r "$ROOT/$src/." "$hp/$name/"; fi
        fi
    else
        if [[ $DRY == 1 ]]; then echo "Would copy $ROOT/$src -> $hp/"
        else cp "$ROOT/$src" "$hp/"; fi
    fi
done <<< "$entries"

[[ $DRY == 1 ]] && echo "Dry run complete - no changes made."
exit 0
