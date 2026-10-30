#!/usr/bin/env bash
# Qwen Code PostToolUse hook — crew build/lint gate (see QWEN.md «Deterministic Enforcement»).
#
# Contract: hook event JSON on stdin. Exit 2 + stderr = blocking error, Qwen feeds stderr back
# to the model. Exit 0 = success, nothing injected.
#
# FAILS OPEN on purpose: no jq, no payload, unknown tool, no project anchor, missing toolchain,
# uninstalled dependencies — all exit 0 silently. A verification hook that wedges every edit is
# worse than no hook at all.
set -uo pipefail

command -v jq >/dev/null 2>&1 || exit 0

payload="$(cat)"
[[ -n "$payload" ]] || exit 0

tool="$(printf '%s' "$payload" | jq -r '.tool_name // empty' 2>/dev/null)"
case "$tool" in
    edit|write_file|notebook_edit) ;;
    *) exit 0 ;;
esac

edited="$(printf '%s' "$payload" | jq -r '.tool_input.file_path // empty' 2>/dev/null)"
[[ -n "$edited" && -f "$edited" ]] || exit 0

tail_lines() { printf '%s\n' "$1" | grep -v '^[[:space:]]*$' | tail -n "$2"; }

fail() { printf '%s\n' "$1" >&2; exit 2; }

# nearest ancestor directory containing one of the marker globs
find_anchor() {
    local dir markers m
    dir="$(cd "$(dirname "$1")" 2>/dev/null && pwd)" || return 0
    shift
    markers=("$@")
    while [[ -n "$dir" ]]; do
        for m in "${markers[@]}"; do
            # nullglob-safe: compgen lists matches without expanding
            if compgen -G "$dir/$m" >/dev/null 2>&1; then printf '%s\n' "$dir"; return 0; fi
        done
        [[ "$dir" == "/" ]] && break
        dir="$(dirname "$dir")"
    done
    return 0
}

ext=".${edited##*.}"
ext="$(printf '%s' "$ext" | tr '[:upper:]' '[:lower:]')"

if [[ "$ext" == ".cs" ]]; then
    command -v dotnet >/dev/null 2>&1 || exit 0
    anchor="$(find_anchor "$edited" '*.csproj' '*.sln')"
    [[ -n "$anchor" ]] || exit 0

    out="$(cd "$anchor" && dotnet build --nologo 2>&1)"
    code=$?
    [[ $code -ne 0 ]] && fail "dotnet build FAILED in $anchor
$(tail_lines "$out" 40)"
    exit 0
fi

case "$ext" in
    .ts|.tsx|.js|.jsx|.css) ;;
    *) exit 0 ;;
esac

case "$edited" in */node_modules/*) exit 0 ;; esac
command -v npm >/dev/null 2>&1 || exit 0

anchor="$(find_anchor "$edited" 'package.json')"
[[ -n "$anchor" ]] || exit 0
case "$anchor" in */node_modules|*/node_modules/*) exit 0 ;; esac
[[ -d "$anchor/node_modules" ]] || exit 0

report=""
failed=0

if [[ "$(jq -r '.scripts.lint // empty' "$anchor/package.json" 2>/dev/null)" != "" ]]; then
    out="$(cd "$anchor" && npm run lint 2>&1)"
    if [[ $? -ne 0 ]]; then
        failed=1
        report+="npm run lint FAILED
$(tail_lines "$out" 30)
"
    fi
fi

if [[ -f "$anchor/tsconfig.json" && -d "$anchor/node_modules/typescript" ]]; then
    out="$(cd "$anchor" && npx tsc --noEmit 2>&1)"
    if [[ $? -ne 0 ]]; then
        failed=1
        report+="npx tsc --noEmit FAILED
$(tail_lines "$out" 30)
"
    fi
fi

[[ $failed -eq 1 ]] && fail "frontend verification FAILED in $anchor
$report"
exit 0
