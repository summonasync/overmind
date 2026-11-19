#!/usr/bin/env bash
#
# pack.sh — build portable Overmind distribution archives (win-x64, linux-x64, osx-x64, osx-arm64).
#
# Usage: build/pack.sh --version <x.y.z> --bundle-version <int> [--crew-kit <path>] [--rids <list>]
#        build/pack.sh --bundle-version <int> --kits-only [--crew-kit <path>]
#
# Produces dist/overmind-<version>-<rid>.zip (win) or .tar.gz (unix) plus a .sha256 sidecar per
# archive. Each archive unpacks to a single folder: host/ (self-contained Host with bundled/ next
# to the binary), mcp/ (self-contained MCP adapter), web/ (static export), run + install scripts.
#
# --kits-only cuts the bundled kit folders plus bundled/manifest.json into dist/kits and stops:
# no dotnet publish, no web build, no archives — the verification leg for the kit split (250).

set -euo pipefail

DEFAULT_RIDS=win-x64,linux-x64,osx-x64,osx-arm64

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
repo_root=$(cd "$script_dir/.." && pwd)

usage() {
    sed -n '3,13p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    exit 2
}

die() {
    echo "pack.sh: $*" >&2
    exit 1
}

version=""
bundle_version=""
crew_kit=""
rids=$DEFAULT_RIDS
kits_only=0

while [[ $# -gt 0 ]]; do
    case $1 in
        --version) [[ $# -ge 2 ]] || usage; version=$2; shift 2 ;;
        --bundle-version) [[ $# -ge 2 ]] || usage; bundle_version=$2; shift 2 ;;
        --crew-kit) [[ $# -ge 2 ]] || usage; crew_kit=$2; shift 2 ;;
        --rids) [[ $# -ge 2 ]] || usage; rids=$2; shift 2 ;;
        --kits-only) kits_only=1; shift ;;
        -h|--help) usage ;;
        *) usage ;;
    esac
done

[[ $bundle_version =~ ^[1-9][0-9]*$ ]] || die "--bundle-version must be a positive integer"
if [[ $kits_only -eq 0 ]]; then
    [[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || die "--version must look like x.y.z"
fi
crew_kit=${crew_kit:-$repo_root/src/OvermindCrewKit}
[[ -d $crew_kit ]] || die "crew kit source not found: $crew_kit"
if kit_dirty=$(git -C "$crew_kit" status --porcelain 2>/dev/null) && [[ -n $kit_dirty ]]; then
    echo "pack.sh: WARNING: kit working tree has uncommitted changes; bundling as-is: $crew_kit" >&2
fi

command -v jq >/dev/null 2>&1 || die "jq is required to cut the kit folders but not on PATH"
if [[ $kits_only -eq 0 ]]; then
    for tool in dotnet npm sha256sum; do
        command -v "$tool" >/dev/null 2>&1 || die "$tool is required but not on PATH"
    done
    [[ -d $repo_root/web/node_modules ]] || die "web/node_modules is missing; run 'npm ci' in web/ first"
fi

layout_file=$script_dir/dist-layout.env
[[ -f $layout_file ]] || die "layout contract not found: $layout_file"
# shellcheck source=/dev/null
source "$layout_file"
read -ra BUNDLED_KIT_LIST <<<"$BUNDLED_KITS"
read -ra MANDATORY_KIT_LIST <<<"${MANDATORY_KITS:-}"
read -ra LEGACY_KIT_LIST <<<"${LEGACY_KITS:-}"
read -ra KIT_SIDE_LIST <<<"$KIT_SIDES"
read -ra KIT_CROSS_SIDE_LIST <<<"${KIT_CROSS_SIDE_ITEMS:-}"
read -ra KIT_EXCLUDE_LIST <<<"${KIT_EXCLUDES:-}"
[[ ${#BUNDLED_KIT_LIST[@]} -gt 0 ]] || die "BUNDLED_KITS is empty in $layout_file"

membership_file=$crew_kit/$KIT_MEMBERSHIP_FILE
[[ -f $membership_file ]] || die "kit membership manifest not found: $membership_file"
jq -e 'type == "object" and (to_entries | all(.value | type == "array" and length > 0))' \
    "$membership_file" >/dev/null \
    || die "kit membership manifest is not an object of non-empty arrays: $membership_file"

kit_in_list() {
    local needle=$1
    shift
    local item
    for item in "$@"; do
        [[ $item == "$needle" ]] && return 0
    done
    return 1
}

declared_kits=$(LC_ALL=C jq -r 'keys_unsorted[]' "$membership_file" | LC_ALL=C sort)
wanted_kits=$(printf '%s\n' "${BUNDLED_KIT_LIST[@]}" | LC_ALL=C sort)
if [[ $declared_kits != "$wanted_kits" ]]; then
    die "BUNDLED_KITS and $KIT_MEMBERSHIP_FILE disagree: $(comm -3 <(printf '%s\n' "$declared_kits") <(printf '%s\n' "$wanted_kits") | tr '\n' ' ')"
fi
for kit in "${MANDATORY_KIT_LIST[@]}"; do
    kit_in_list "$kit" "${BUNDLED_KIT_LIST[@]}" || die "MANDATORY_KITS names '$kit', which is not a bundled kit"
done
for kit in "${LEGACY_KIT_LIST[@]}"; do
    if kit_in_list "$kit" "${BUNDLED_KIT_LIST[@]}"; then
        die "LEGACY_KITS names '$kit', which is also shipped — drop it from one of the two lists"
    fi
done

# Each bundled kit folder carries the canonical side of its membership items: side-prefixed paths
# lose the prefix, side-unique prose lands at the kit root under its own name, and every other
# item is a mirror copy that materialization regenerates per platform. Excludes drop machine state.
kit_excluded() {
    local rel=$1 exclude
    for exclude in "${KIT_EXCLUDE_LIST[@]}"; do
        [[ -n $exclude ]] || continue
        [[ $rel == "$exclude" || $rel == "$exclude"/* ]] && return 0
    done
    return 1
}

kit_item_dest() {
    local item=$1 rel side cross
    for side in "${KIT_SIDE_LIST[@]}"; do
        if [[ $item == "$side/"* ]]; then
            rel=${item#"$side/"}
            ! kit_excluded "$rel" || return 1
            printf '%s\n' "$rel"
            return 0
        fi
    done
    for cross in "${KIT_CROSS_SIDE_LIST[@]}"; do
        [[ -n $cross ]] || continue
        if [[ $item == "$cross" ]]; then
            rel=${cross#*/}
            ! kit_excluded "$rel" || return 1
            printf '%s\n' "$rel"
            return 0
        fi
    done
    return 1
}

bundled_kit_items() {
    jq -r --arg kit "$1" '.[$kit][]' "$membership_file"
}

stage_bundled_kit() {
    local kit=$1 bundled_dir=$2
    local dest=$bundled_dir/$kit
    local item rel staged=0 mirrored=0 skills=0 agents=0 prose=0

    mkdir -p "$dest"
    while IFS= read -r item; do
        [[ -e $crew_kit/$item ]] || die "kit '$kit' item not found: $crew_kit/$item"
        if rel=$(kit_item_dest "$item"); then
            [[ -e $dest/$rel ]] && die "kit '$kit': two membership items land on '$rel'"
            mkdir -p "$dest/$(dirname "$rel")"
            cp -R "$crew_kit/$item" "$dest/$rel"
            staged=$((staged + 1))
            case $rel in
                skills/*) skills=$((skills + 1)) ;;
                agents/*) agents=$((agents + 1)) ;;
                */*) ;;
                *) prose=$((prose + 1)) ;;
            esac
        else
            mirrored=$((mirrored + 1))
        fi
    done < <(bundled_kit_items "$kit")

    local disk_skills=0 disk_agents=0 disk_prose
    [[ -d $dest/skills ]] && disk_skills=$(find "$dest/skills" -name SKILL.md | wc -l)
    [[ -d $dest/agents ]] && disk_agents=$(find "$dest/agents" -maxdepth 1 -type f | wc -l)
    disk_prose=$(find "$dest" -maxdepth 1 -type f | wc -l)
    [[ $disk_skills -eq $skills ]] || die "kit '$kit' staged $skills skills, disk holds $disk_skills"
    [[ $disk_agents -eq $agents ]] || die "kit '$kit' staged $agents agents, disk holds $disk_agents"
    [[ $disk_prose -eq $prose ]] || die "kit '$kit' staged $prose root files, disk holds $disk_prose"

    printf '==> kit %-18s %3d items staged: %2d skills, %2d agents, %2d prose (%d mirror copies skipped)\n' \
        "$kit" "$staged" "$skills" "$agents" "$prose" "$mirrored"
    STAGED_SKILLS=$((STAGED_SKILLS + skills))
    STAGED_AGENTS=$((STAGED_AGENTS + agents))
}

write_bundled_manifest() {
    local bundled_dir=$1
    local mandatory_json kits_json legacy_json
    mandatory_json=$(printf '%s\n' "${MANDATORY_KIT_LIST[@]}" | jq -R . | jq -sc 'map(select(. != ""))')
    kits_json=$(printf '%s\n' "${BUNDLED_KIT_LIST[@]}" | jq -R . \
        | jq -sc --argjson mandatory "$mandatory_json" \
            'map(. as $name | {name: $name, mandatory: (($mandatory | index($name)) != null)})')
    legacy_json=$(printf '%s\n' "${LEGACY_KIT_LIST[@]}" | jq -R . | jq -sc 'map(select(. != ""))')
    jq -cn --argjson bundle_version "$bundle_version" --argjson kits "$kits_json" \
        --argjson legacy "$legacy_json" \
        '{bundleVersion: $bundle_version, kits: $kits, legacy: $legacy}' \
        >"$bundled_dir/manifest.json"
}

stage_bundled_kits() {
    local bundled_dir=$1 kit
    STAGED_SKILLS=0
    STAGED_AGENTS=0
    for kit in "${BUNDLED_KIT_LIST[@]}"; do
        stage_bundled_kit "$kit" "$bundled_dir"
    done
    write_bundled_manifest "$bundled_dir"

    local source_skills=0 source_agents=0
    [[ -d $crew_kit/.claude/skills ]] && source_skills=$(find "$crew_kit/.claude/skills" -name SKILL.md | wc -l)
    [[ -d $crew_kit/.claude/agents ]] && source_agents=$(find "$crew_kit/.claude/agents" -maxdepth 1 -type f | wc -l)
    [[ $source_skills -gt 0 ]] || die "no SKILL.md found under $crew_kit; refusing to ship an empty bundle"
    [[ $STAGED_SKILLS -eq $source_skills ]] ||
        die "kit split lost skills: the six kits stage $STAGED_SKILLS of $source_skills"
    [[ $STAGED_AGENTS -eq $source_agents ]] ||
        die "kit split lost agent definitions: the six kits stage $STAGED_AGENTS of $source_agents"

    find "$bundled_dir" -type d -exec chmod 755 {} +
    find "$bundled_dir" -type f -exec chmod 444 {} +
    printf '==> bundled kits: %d folders, %d skills, %d agents, legacy %s\n' \
        "${#BUNDLED_KIT_LIST[@]}" "$STAGED_SKILLS" "$STAGED_AGENTS" "${LEGACY_KITS:-none}"
}

dist_dir=$repo_root/dist
kits_dir=$dist_dir/kits
stage_root=$dist_dir/stage

if [[ $kits_only -eq 1 ]]; then
    drop_kits() {
        [[ -d $kits_dir ]] || return 0
        chmod -R u+w "$kits_dir" 2>/dev/null || true
        rm -rf "$kits_dir"
    }
    drop_kits
    mkdir -p "$kits_dir"
    echo "==> cutting bundled kits into $kits_dir (no publish, no web build)"
    stage_bundled_kits "$kits_dir"
    echo "==> done: kit staging only, nothing published"
    exit 0
fi

IFS=, read -ra RID_LIST <<<"$rids"
[[ ${#RID_LIST[@]} -gt 0 ]] || die "--rids produced an empty list"
for rid in "${RID_LIST[@]}"; do
    [[ $rid =~ ^(win|linux|osx)-(x64|arm64)$ ]] || die "unsupported rid: $rid"
    if [[ $rid == win-* ]] && ! command -v zip >/dev/null 2>&1; then
        die "zip is required for $rid but not on PATH"
    fi
done

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

    echo "==> cutting bundled kits for $rid"
    stage_bundled_kits "$stage/$DIR_HOST/$DIR_BUNDLED"

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
