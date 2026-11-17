#!/usr/bin/env bash
# Materializes the TEMP per-runtime MCP configs pointing at a locally built adapter
# against the isolated host. Never touches ~/.claude.json, ~/.qwen/settings.json or
# ~/.kimi-code/mcp.json — kimi gets a scratch KIMI_CODE_HOME whose mcp.json is ours.
#
# Env: ADAPTER (path to overmind-mcp binary), API_URL, PROJECT_ID, OUT_DIR.
# Same script must run unchanged against the dieted build (T6): only ADAPTER changes.
set -euo pipefail

ADAPTER="${ADAPTER:-/mnt/Ubuntu-Dev/scratch/overmind-234/adapter/overmind-mcp}"
API_URL="${API_URL:-http://127.0.0.1:4799}"
PROJECT_ID="${PROJECT_ID:-ff313f29-8144-488c-94e6-5ea5dde57bfd}"
OUT_DIR="${OUT_DIR:-/mnt/Ubuntu-Dev/scratch/overmind-234/cfg}"

mkdir -p "$OUT_DIR"
python3 - "$ADAPTER" "$API_URL" "$PROJECT_ID" "$OUT_DIR" <<'PY'
import json, pathlib, sys

adapter, url, project, out = sys.argv[1:5]
server = {
    "command": adapter,
    "env": {
        "OVERMIND_API_URL": url,
        "OVERMIND_PROJECT_ID": project,
    },
}
cfg = {"mcpServers": {"overmind": server}}
p = pathlib.Path(out)
(p / "overmind.mcp.json").write_text(json.dumps(cfg, indent=1))
(p / "kimi-home").mkdir(exist_ok=True)
(p / "kimi-home" / "mcp.json").write_text(json.dumps(cfg, indent=1))
print("wrote", str(p / "overmind.mcp.json"), "and", str(p / "kimi-home" / "mcp.json"))
PY

KIMI_SRC="${KIMI_SRC:-$HOME/.kimi-code}"
KIMI_HOME="$OUT_DIR/kimi-home"
for f in config.toml tui.toml device_id region projects.json; do
  [[ -e "$KIMI_SRC/$f" && ! -e "$KIMI_HOME/$f" ]] && cp -a "$KIMI_SRC/$f" "$KIMI_HOME/$f" || true
done
[[ -d "$KIMI_SRC/credentials" && ! -e "$KIMI_HOME/credentials" ]] && cp -a "$KIMI_SRC/credentials" "$KIMI_HOME/credentials"
[[ -d "$KIMI_SRC/oauth" && ! -e "$KIMI_HOME/oauth" ]] && cp -a "$KIMI_SRC/oauth" "$KIMI_HOME/oauth"
echo "KIMI_CFG $KIMI_HOME"
