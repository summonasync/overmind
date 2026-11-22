#!/usr/bin/env bash
# grimoire-kit packer — builds a self-contained client bundle (dir + tar.gz) under dist/.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
command -v python3 >/dev/null 2>&1 || { echo "python3 is required (manifest version)" >&2; exit 1; }
command -v tar >/dev/null 2>&1 || { echo "tar is required" >&2; exit 1; }

VERSION="$(python3 -c "import json; print(json.load(open('$ROOT/manifest.json'))['version'])")"
NAME="grimoire-kit-$VERSION"
DIST="$ROOT/dist"

rm -rf "$DIST/$NAME"
mkdir -p "$DIST/$NAME"

cp "$ROOT/manifest.json" "$ROOT/install.sh" "$ROOT/install.ps1" "$ROOT/pack.sh" \
   "$ROOT/README.md" "$ROOT/CHANGELOG.md" "$DIST/$NAME/"
cp -r "$ROOT/skills" "$ROOT/scripts" "$ROOT/runtime-profiles" "$ROOT/tools" "$DIST/$NAME/"

tar -czf "$DIST/$NAME.tar.gz" -C "$DIST" "$NAME"

echo "Packed $NAME:"
echo "  dir    $DIST/$NAME"
echo "  bundle $DIST/$NAME.tar.gz"
