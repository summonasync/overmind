#!/usr/bin/env python3
"""Ground-truth MCP tool-description measurement over the JSON-RPC stdio wire.

Spawns an adapter process, runs initialize -> notifications/initialized -> tools/list,
then sums, per tool, the len() in unicode code points of the tool `description` plus every
`description` string found anywhere in its inputSchema (parameters and nested schema nodes).

Usage:
  measure_wire.py --adapter-cmd CMD [--adapter-arg ARG ...] [--env KEY=VALUE ...]
                  [--out JSON] [--md MD]

Same invocation must work against any build (baseline or dieted) — pass the adapter
built from that branch, nothing else changes.
"""

import argparse
import json
import os
import subprocess
import sys

PROTOCOL_VERSION = "2025-03-26"
CLIENT_INFO = {"name": "tool-desc-diet-harness", "version": "1"}


def drain_descriptions(node):
    total = 0
    if isinstance(node, dict):
        for key, value in node.items():
            if key == "description" and isinstance(value, str):
                total += len(value)
            else:
                total += drain_descriptions(value)
    elif isinstance(node, list):
        for item in node:
            total += drain_descriptions(item)
    return total


def exchange(proc, request_id, payload):
    proc.stdin.write(json.dumps(payload) + "\n")
    proc.stdin.flush()
    while True:
        line = proc.stdout.readline()
        if not line:
            raise RuntimeError(f"adapter closed stdout while awaiting id={request_id}")
        line = line.strip()
        if not line:
            continue
        try:
            msg = json.loads(line)
        except json.JSONDecodeError:
            continue
        if msg.get("id") == request_id:
            return msg


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--adapter-cmd", required=True)
    ap.add_argument("--adapter-arg", action="append", default=[])
    ap.add_argument("--env", action="append", default=[], help="KEY=VALUE overrides for the adapter process")
    ap.add_argument("--out", help="write per-tool rows as JSON")
    ap.add_argument("--md", help="write per-tool markdown table")
    ap.add_argument("--limit", default=None, help="cursor page size for tools/list (omit for default)")
    args = ap.parse_args()

    env = dict(os.environ)
    for pair in args.env:
        key, _, value = pair.partition("=")
        env[key] = value

    cmd = [args.adapter_cmd, *args.adapter_arg]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                            stderr=subprocess.DEVNULL, text=True, encoding="utf-8", env=env)

    init = {"jsonrpc": "2.0", "id": 1, "method": "initialize",
            "params": {"protocolVersion": PROTOCOL_VERSION, "capabilities": {},
                       "clientInfo": CLIENT_INFO}}
    reply = exchange(proc, 1, init)
    if "error" in reply:
        raise RuntimeError(f"initialize failed: {reply['error']}")
    exchange_note = {"jsonrpc": "2.0", "method": "notifications/initialized"}
    proc.stdin.write(json.dumps(exchange_note) + "\n")
    proc.stdin.flush()

    tools = []
    cursor = None
    page_id = 2
    while True:
        params = {} if cursor is None else {"cursor": cursor}
        if args.limit:
            params["limit"] = args.limit
        reply = exchange(proc, page_id, {"jsonrpc": "2.0", "id": page_id,
                                         "method": "tools/list", "params": params})
        if "error" in reply:
            raise RuntimeError(f"tools/list failed: {reply['error']}")
        batch = reply["result"]
        tools.extend(batch.get("tools", []))
        cursor = batch.get("nextCursor")
        page_id += 1
        if not cursor:
            break

    proc.stdin.close()
    proc.terminate()
    proc.wait(timeout=10)

    rows = []
    for tool in tools:
        tool_desc = tool.get("description") or ""
        schema_chars = drain_descriptions(tool.get("inputSchema") or {})
        rows.append({
            "tool": tool["name"],
            "tool_desc_chars": len(tool_desc),
            "param_desc_chars": schema_chars,
            "total_chars": len(tool_desc) + schema_chars,
        })
    rows.sort(key=lambda r: -r["total_chars"])

    grand_tool = sum(r["tool_desc_chars"] for r in rows)
    grand_param = sum(r["param_desc_chars"] for r in rows)
    grand = grand_tool + grand_param

    print(f"tools={len(rows)} tool_desc_chars={grand_tool} param_desc_chars={grand_param} total={grand}")
    print(f"est_tokens_chars_div_3.5={grand / 3.5:.0f}")
    for r in rows:
        print(f"{r['tool']}\t{r['tool_desc_chars']}\t{r['param_desc_chars']}\t{r['total_chars']}")

    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            json.dump({"method": "wire.tools/list", "tool_count": len(rows),
                       "tool_desc_chars": grand_tool, "param_desc_chars": grand_param,
                       "total_chars": grand, "rows": rows}, f, indent=1)
    if args.md:
        with open(args.md, "w", encoding="utf-8") as f:
            f.write("| # | Tool | Desc chars | Param chars | Total chars |\n")
            f.write("|---|------|-----------:|------------:|------------:|\n")
            for i, r in enumerate(rows, 1):
                f.write(f"| {i} | {r['tool']} | {r['tool_desc_chars']:,} | {r['param_desc_chars']:,} | {r['total_chars']:,} |\n")
            f.write(f"| — | **TOTAL ({len(rows)} tools)** | **{grand_tool:,}** | **{grand_param:,}** | **{grand:,}** |\n")


if __name__ == "__main__":
    sys.exit(main())
