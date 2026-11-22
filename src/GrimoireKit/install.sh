#!/usr/bin/env bash
# grimoire-kit installer — merge-safe: kit files land in kit-owned subpaths, foreign
# content in the destination homes (skills, hooks, settings.json entries) survives.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BASE_HOME="${HOME:-}"
RUNTIME="all"
UNINSTALL=0
DRY=0

usage() {
    cat <<'EOF'
grimoire-kit installer

Usage: install.sh [options]
  --home DIR        operate under DIR instead of $HOME (sandbox/testing; creates
                    runtime home dirs instead of requiring them to exist)
  --runtime NAME    claude | qwen | all (default: all)
  --uninstall       remove grimoire-kit files and its settings hook entries only
  --dry-run         print what would happen, change nothing
  -h, --help        this text

Merge discipline: skills and hooks dirs are additive (per-child), settings.json
hook entries are merged by tools/settings_merge.py (existing entries survive).
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --home) BASE_HOME="$2"; shift 2 ;;
        --runtime) RUNTIME="${2:?runtime needs a value}"; shift 2 ;;
        --uninstall) UNINSTALL=1; shift ;;
        --dry-run) DRY=1; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown argument: $1 (see --help)" >&2; exit 2 ;;
    esac
done

case "$RUNTIME" in claude|qwen|all) ;; *) echo "Bad --runtime '$RUNTIME' (claude|qwen|all)" >&2; exit 2 ;; esac
[[ -n "$BASE_HOME" ]] || { echo "No HOME resolved; pass --home DIR" >&2; exit 1; }

command -v python3 >/dev/null 2>&1 || { echo "python3 is required (settings.json hook merge)" >&2; exit 1; }

MERGE_TOOL="$ROOT/tools/settings_merge.py"
MARKER='hooks/grimoire-kit/'
REAL_HOME="${HOME:-}"
SANDBOX=0
[[ "$BASE_HOME" != "$REAL_HOME" ]] && SANDBOX=1

want_runtime() { [[ "$RUNTIME" == "all" || "$RUNTIME" == "$1" ]]; }

home_dir() {  # $1 = runtime name -> base home subpath
    case "$1" in claude) echo "$BASE_HOME/.claude";; qwen) echo "$BASE_HOME/.qwen";; esac
}

home_active() {  # explicit --home means a managed sandbox: create and proceed; real $HOME: skip runtimes absent from the machine
    local hp
    hp="$(home_dir "$1")"
    if [[ $SANDBOX == 1 ]]; then
        [[ $DRY == 1 ]] || mkdir -p "$hp"
        return 0
    fi
    [[ -d "$hp" ]]
}

manifest_field() {  # $1 = python expr, evaluated against the parsed manifest as m
    python3 -c "import json; m = json.load(open('$ROOT/manifest.json')); print($1)"
}

entries_tsv() {
    python3 - "$ROOT" <<'PYEOF'
import json, sys
m = json.load(open(f"{sys.argv[1]}/manifest.json"))
for e in m["entries"]:
    print("\t".join([e["source"], e["home"], e["type"], e["mode"], e["dest"]]))
PYEOF
}

settings_path() {  # $1 = runtime name
    echo "$(home_dir "$1")/$(manifest_field "m['hooks']['$1']['settings']")"
}

mirror_dir() {  # $1 = src dir, $2 = dst dir
    if [[ $DRY == 1 ]]; then echo "Would rsync -a --delete $1/ -> $2/"; return 0; fi
    mkdir -p "$2"
    if command -v rsync >/dev/null 2>&1; then
        rsync -a --delete "$1/" "$2/"
    else
        echo "rsync not found - falling back to additive cp (stale kit files are NOT removed)" >&2
        cp -r "$1/." "$2/"
    fi
}

copy_file() {  # $1 = src file, $2 = dst dir
    if [[ $DRY == 1 ]]; then echo "Would copy $1 -> $2/"; return 0; fi
    mkdir -p "$2" && cp "$1" "$2/"
}

while IFS=$'\t' read -r src home type mode dest; do
    [[ -n "$src" ]] || continue
    want_runtime "$home" || continue
    home_active "$home" || { echo "Home $(home_dir "$home") not found, skipping its entries."; continue; }
    hp="$(home_dir "$home")"
    [[ -e "$ROOT/$src" ]] || { echo "Missing kit source $ROOT/$src" >&2; exit 1; }

    if [[ $UNINSTALL == 1 ]]; then
        if [[ "$mode" == "merge" ]]; then
            for child in "$ROOT/$src"/*; do
                [[ -e "$child" ]] || continue
                t="$hp/$dest/$(basename "$child")"
                if [[ $DRY == 1 ]]; then [[ -e "$t" ]] && echo "Would remove $t"
                elif [[ -e "$t" ]]; then rm -rf "$t"; echo "Removed $t"; fi
            done
        else
            t="$hp/$dest"
            if [[ $DRY == 1 ]]; then [[ -e "$t" ]] && echo "Would remove $t"
            elif [[ -e "$t" ]]; then rm -rf "$t"; echo "Removed $t"; fi
        fi
        continue
    fi

    if [[ "$type" == "dir" ]]; then
        if [[ "$mode" == "merge" ]]; then
            for child in "$ROOT/$src"/*; do
                [[ -e "$child" ]] || continue
                if [[ -d "$child" ]]; then mirror_dir "$child" "$hp/$dest/$(basename "$child")"
                else copy_file "$child" "$hp/$dest"; fi
            done
        else
            mirror_dir "$ROOT/$src" "$hp/$dest"
        fi
    else
        copy_file "$ROOT/$src" "$hp/$(dirname "$dest")"
    fi
done < <(entries_tsv)

for rt in claude qwen; do
    want_runtime "$rt" || continue
    home_active "$rt" || { echo "Home $(home_dir "$rt") not found, skipping its hook wiring."; continue; }
    settings="$(settings_path "$rt")"
    if [[ $DRY == 1 ]]; then
        if [[ $UNINSTALL == 1 ]]; then echo "Would remove grimoire-kit hook entries from $settings"
        else echo "Would merge hook entry into $settings [$(manifest_field "m['hooks']['$rt']['event']")]"; fi
        continue
    fi
    if [[ $UNINSTALL == 1 ]]; then
        python3 "$MERGE_TOOL" unmerge "$settings" "$MARKER"
    else
        python3 "$MERGE_TOOL" apply "$ROOT" "$rt" "$settings" "$MARKER"
    fi
done

if [[ $UNINSTALL == 1 && $DRY == 0 ]]; then
    prune_empty_up() {  # $1 = start dir, $2 = base home to stop at
        local d="$1" stop="$2"
        while [[ "$d" != "$stop" && -d "$d" && -z "$(ls -A "$d")" ]]; do
            rmdir "$d" && echo "Removed empty dir $d"
            d="$(dirname "$d")"
        done
    }
    while IFS=$'\t' read -r src home type mode dest; do
        [[ -n "$src" ]] || continue
        want_runtime "$home" || continue
        home_active "$home" || continue
        t="$(home_dir "$home")/$dest"
        prune_empty_up "$([[ "$type" == "dir" ]] && echo "$t" || dirname "$t")" "$BASE_HOME"
    done < <(entries_tsv)
fi

[[ $DRY == 1 ]] && echo "Dry run complete - no changes made."
[[ $UNINSTALL == 0 ]] && echo "grimoire-kit install complete (runtime: $RUNTIME)."
exit 0
