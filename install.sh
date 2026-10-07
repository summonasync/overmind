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

home_path() { case "$1" in kimi) echo "$HOME/.kimi-code";; claude) echo "$HOME/.claude";; gemini) echo "$HOME/.gemini";; antigravity) echo "$HOME/.gemini/antigravity";; qwen) echo "$HOME/.qwen";; codex) echo "$HOME/.codex";; esac }

# Every manifest entry carries all four fields in this exact order — the no-python fallback
# below groups grep hits four at a time, so a missing "mode" would desynchronise the whole list.
parse_manifest() {
    if have_python; then
        (cd "$ROOT" && python -c "
import json
m = json.load(open('manifest.json'))
for e in m['entries']:
    print(e['source'], e['home'], e['type'], e.get('mode', 'mirror'))
" | tr -d '\r')
    else
        grep -oE '"source": *"[^"]+"|"home": *"[^"]+"|"type": *"[^"]+"|"mode": *"[^"]+"' "$ROOT/manifest.json" \
          | sed -E 's/"(source|home|type|mode)": *"([^"]+)"/\2/' \
          | paste -d' ' - - - -
    fi
}

mirror_dir() {  # $1 = src dir, $2 = dst dir
    if command -v rsync >/dev/null 2>&1; then
        if [[ $DRY == 1 ]]; then echo "Would rsync -a --delete $1/ -> $2/"
        else mkdir -p "$2" && rsync -a --delete "$1/" "$2/"; fi
    else
        # cp fallback is additive only: stale files in $2 are NOT deleted (unlike robocopy /MIR on Windows)
        if [[ $DRY == 1 ]]; then echo "Would copy -r $1/. -> $2/"
        else mkdir -p "$2" && cp -r "$1/." "$2/"; fi
    fi
}

copy_file() {  # $1 = src file, $2 = dst dir
    if [[ $DRY == 1 ]]; then echo "Would copy $1 -> $2/"
    else mkdir -p "$2" && cp "$1" "$2/"; fi
}

declare -A HOME_OK=()
entries="$(parse_manifest)" || { echo "Failed to parse manifest.json" >&2; exit 1; }
[[ -n "$entries" ]] || { echo "manifest.json parsed to zero entries" >&2; exit 1; }
while read -r src home type mode; do
    hp="$(home_path "$home")"
    if [[ -z "${HOME_OK[$home]:-}" ]]; then
        if [[ -d "$hp" ]]; then HOME_OK[$home]=1
        elif [[ "$home" == "antigravity" && -d "$(dirname "$hp")" ]]; then HOME_OK[$home]=1
        else HOME_OK[$home]=0; echo "Home $hp not found, skipping its entries."; fi
    fi
    [[ "${HOME_OK[$home]}" == "0" ]] && continue

    name="$(basename "$src")"
    if [[ "$type" == "dir" ]]; then
        if [[ "$mode" == "merge" ]]; then
            # Mirror each child separately: third-party content living next to the crew's own
            # entries in the destination (e.g. non-crew skills in ~/.qwen/skills) survives.
            [[ $DRY == 1 ]] || mkdir -p "$hp/$name"
            for child in "$ROOT/$src"/*; do
                [[ -e "$child" ]] || continue
                if [[ -d "$child" ]]; then mirror_dir "$child" "$hp/$name/$(basename "$child")"
                else copy_file "$child" "$hp/$name"; fi
            done
        else
            mirror_dir "$ROOT/$src" "$hp/$name"
        fi
    else
        copy_file "$ROOT/$src" "$hp"
    fi
done <<< "$entries"

[[ $DRY == 1 ]] && echo "Dry run complete - no changes made."
exit 0
