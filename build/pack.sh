#!/usr/bin/env bash
#
# pack.sh — build portable Overmind distribution archives (win-x64, linux-x64, osx-x64, osx-arm64).
#
# Usage: build/pack.sh --version <x.y.z> --bundle-version <int> [--crew-kit <path>] [--rids <rid,rid,...>]
#
# Produces dist/overmind-<version>-<rid>.zip (win) or .tar.gz (unix) plus a .sha256 sidecar per
# archive. Each archive unpacks to a single folder: host/ (self-contained Host with bundled/ next
# to the binary), mcp/ (self-contained MCP adapter), web/ (static export), run + install scripts.

set -euo pipefail

DEFAULT_CREW_KIT=/home/johan-boolman/.claude
DEFAULT_RIDS=win-x64,linux-x64,osx-x64,osx-arm64

usage() {
    sed -n '3,9p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    exit 2
}

die() {
    echo "pack.sh: $*" >&2
    exit 1
}

version=""
bundle_version=""
crew_kit=$DEFAULT_CREW_KIT
rids=$DEFAULT_RIDS

while [[ $# -gt 0 ]]; do
    case $1 in
        --version) [[ $# -ge 2 ]] || usage; version=$2; shift 2 ;;
        --bundle-version) [[ $# -ge 2 ]] || usage; bundle_version=$2; shift 2 ;;
        --crew-kit) [[ $# -ge 2 ]] || usage; crew_kit=$2; shift 2 ;;
        --rids) [[ $# -ge 2 ]] || usage; rids=$2; shift 2 ;;
        -h|--help) usage ;;
        *) usage ;;
    esac
done

[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "--version must look like x.y.z"
[[ $bundle_version =~ ^[1-9][0-9]*$ ]] || die "--bundle-version must be a positive integer"
[[ -d $crew_kit ]] || die "crew kit source not found: $crew_kit"

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
repo_root=$(cd "$script_dir/.." && pwd)
dist_dir=$repo_root/dist
stage_root=$dist_dir/stage

layout_file=$script_dir/dist-layout.env
[[ -f $layout_file ]] || die "layout contract not found: $layout_file"
# shellcheck source=/dev/null
source "$layout_file"
KIT_NAME=$CREW_KIT_NAME
read -ra CREW_ITEM_LIST <<<"$CREW_ITEMS"
read -ra CREW_EXCLUDE_LIST <<<"${CREW_EXCLUDES:-}"

for item in "${CREW_ITEM_LIST[@]}"; do
    [[ -e $crew_kit/$item ]] || die "crew kit item not found: $crew_kit/$item"
done
skill_count=$(find "$crew_kit/skills" "$crew_kit/agents" -name SKILL.md 2>/dev/null | wc -l)
[[ $skill_count -ge 1 ]] || die "no SKILL.md found under $crew_kit; refusing to ship an empty crew kit"

for tool in dotnet npm sha256sum; do
    command -v "$tool" >/dev/null 2>&1 || die "$tool is required but not on PATH"
done

IFS=, read -ra RID_LIST <<<"$rids"
[[ ${#RID_LIST[@]} -gt 0 ]] || die "--rids produced an empty list"
for rid in "${RID_LIST[@]}"; do
    [[ $rid =~ ^(win|linux|osx)-(x64|arm64)$ ]] || die "unsupported rid: $rid"
    if [[ $rid == win-* ]] && ! command -v zip >/dev/null 2>&1; then
        die "zip is required for $rid but not on PATH"
    fi
done

[[ -d $repo_root/web/node_modules ]] || die "web/node_modules is missing; run 'npm ci' in web/ first"

drop_stage() {
    [[ -d $stage_root ]] || return 0
    chmod -R u+w "$stage_root" 2>/dev/null || true
    rm -rf "$stage_root"
}
trap drop_stage EXIT
drop_stage

echo "==> building web export (always rebuilt: mtime staleness checks are flaky, the build is cheap)"
(
    cd "$repo_root/web"
    npm run build >/dev/null
)
web_out=$repo_root/web/out
[[ -f $web_out/index.html ]] || die "web build did not produce web/out/index.html"

stage_crew_kit() {
    local bundled_dir=$1
    mkdir -p "$bundled_dir/$KIT_NAME"
    local item
    for item in "${CREW_ITEM_LIST[@]}"; do
        cp -R "$crew_kit/$item" "$bundled_dir/$KIT_NAME/"
    done
    local exclude
    for exclude in "${CREW_EXCLUDE_LIST[@]}"; do
        rm -rf "$bundled_dir/$KIT_NAME/$exclude"
    done
    printf '{"bundleVersion":%s,"kits":["%s"]}\n' "$bundle_version" "$KIT_NAME" >"$bundled_dir/manifest.json"
    find "$bundled_dir" -type d -exec chmod 755 {} +
    find "$bundled_dir" -type f -exec chmod 444 {} +
}

mkdir -p "$dist_dir" "$stage_root"

for rid in "${RID_LIST[@]}"; do
    name=overmind-$version-$rid
    stage=$stage_root/$name

    echo "==> publishing host for $rid"
    mkdir -p "$stage/$DIR_HOST" "$stage/$DIR_MCP" "$stage/$DIR_WEB"
    dotnet publish "$repo_root/src/Overmind.Host/Overmind.Host.csproj" \
        -c Release -r "$rid" --self-contained -o "$stage/$DIR_HOST" >/dev/null

    echo "==> publishing mcp adapter for $rid"
    dotnet publish "$repo_root/src/Overmind.Mcp/Overmind.Mcp.csproj" \
        -c Release -r "$rid" --self-contained -o "$stage/$DIR_MCP" >/dev/null

    cp -R "$web_out/." "$stage/$DIR_WEB/"

    stage_crew_kit "$stage/$DIR_HOST/$DIR_BUNDLED"

    if [[ $rid == win-* ]]; then
        cp "$script_dir/install.ps1" "$script_dir/run.cmd" "$stage/"
        rm -f "$dist_dir/$name.zip"
        (
            cd "$stage_root"
            zip -r -q "$dist_dir/$name.zip" "$name"
        )
        archive=$dist_dir/$name.zip
    else
        cp "$script_dir/install.sh" "$script_dir/run.sh" "$stage/"
        chmod +x "$stage/run.sh" "$stage/install.sh"
        tar -czf "$dist_dir/$name.tar.gz" -C "$stage_root" "$name"
        archive=$dist_dir/$name.tar.gz
    fi

    (
        cd "$dist_dir"
        sha256sum "$(basename "$archive")" >"$(basename "$archive").sha256"
    )
    echo "==> packed $(basename "$archive") ($(du -h "$archive" | cut -f1))"
done

artifact_count=$(find "$dist_dir" -maxdepth 1 -name 'overmind-*' | wc -l)
echo "==> done: $artifact_count artifact files in $dist_dir"
