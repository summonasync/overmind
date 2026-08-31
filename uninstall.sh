#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DRY=0
FORCE=0
for arg in "$@"; do
    case "$arg" in
        --dry-run) DRY=1 ;;
        --force) FORCE=1 ;;
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

declare -A SEEN=()
targets=()
entries="$(parse_manifest)" || { echo "Failed to parse manifest.json" >&2; exit 1; }
[[ -n "$entries" ]] || { echo "manifest.json parsed to zero entries" >&2; exit 1; }
while read -r src home type; do
    hp="$(home_path "$home")"
    [[ -d "$hp" ]] || continue
    name="$(basename "$src")"
    dst="$hp/$name"
    [[ -n "${SEEN[$dst]:-}" ]] && continue
    SEEN[$dst]=1
    targets+=("$dst")
done <<< "$entries"

if [[ $DRY == 1 ]]; then
    for t in "${targets[@]:-}"; do [[ -n "$t" ]] && echo "Would remove $t"; done
    echo "Dry run complete - no changes made."
    exit 0
fi

if [[ $FORCE == 0 ]]; then
    echo "The following paths will be removed:"
    for t in "${targets[@]:-}"; do [[ -n "$t" ]] && echo "  $t"; done
    read -r -p "Proceed? [y/N] " answer
    [[ "$answer" =~ ^(y|yes)$ ]] || { echo "Aborted."; exit 0; }
fi

homes=()
for t in "${targets[@]:-}"; do
    [[ -z "$t" ]] && continue
    [[ -e "$t" ]] && { rm -rf "$t"; echo "Removed $t"; }
    parent="$(dirname "$t")"
    case "$parent" in
        "$HOME/.kimi-code"|"$HOME/.claude") ;;  # never touch home root
        *) [[ -d "$parent" ]] && [[ -z "$(ls -A "$parent")" ]] && { rm -rf "$parent"; echo "Removed empty dir $parent"; } ;;
    esac
done
exit 0
