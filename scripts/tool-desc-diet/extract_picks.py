#!/usr/bin/env python3
"""Extract the MCP tool picks from a headless runtime run log (claude/qwen/kimi stream).

Scans newline-delimited JSON for tool_use blocks; normalizes deferred chains
(qwen tool_call{name:X} -> X; tool_search -> recorded as search step, not a pick)
and strips mcp__<server>__ prefixes. Cross-checks names against the tool list
captured by measure_wire.py (--toolnames JSON) so typos surface.

Usage: extract_picks.py [--runtime R] LOG [LOG ...]
"""

import argparse
import json
import re
import sys

OBSERVED = []


def walk_tool_uses(obj):
    if isinstance(obj, dict):
        t = obj.get("type")
        name = obj.get("name")
        if t == "tool_use" and isinstance(name, str):
            yield obj
        fn = obj.get("function")
        if isinstance(fn, dict) and isinstance(fn.get("name"), str):
            args = fn.get("arguments")
            if isinstance(args, str):
                try:
                    fn = {**fn, "arguments": json.loads(args)}
                except json.JSONDecodeError:
                    pass
            yield {"type": "tool_use", "name": fn["name"], "input": fn.get("arguments") or {}}
        for value in obj.values():
            yield from walk_tool_uses(value)
    elif isinstance(obj, list):
        for item in obj:
            yield from walk_tool_uses(item)


KNOWN = set()


def strip_prefix(name):
    return re.sub(r"^mcp__[^_]+__", "", name)


def resolve(name):
    return strip_prefix(name) if name in KNOWN or strip_prefix(name) in KNOWN else None


def normalize(block):
    # direct shape: tool_use{name: mcp__overmind__X} ; deferred shape (qwen tool_search
    # mode): tool_use{name: tool_call, input:{name: X or mcp__server__X}}.
    outer = block["name"]
    inp = block.get("input") or {}
    if strip_prefix(outer) in ("tool_call", "call") and isinstance(inp, dict):
        inner = inp.get("name")
        if isinstance(inner, str):
            pick = resolve(inner)
            if pick:
                return pick, [outer, inner]
            return inner, [outer, inner]
    pick = resolve(outer)
    if pick:
        return pick, [outer]
    return outer, [outer]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--runtime")
    ap.add_argument("--toolnames", help="wire.json from measure_wire.py; unknown names get flagged")
    ap.add_argument("logs", nargs="+")
    args = ap.parse_args()

    known = set()
    if args.toolnames:
        doc = json.load(open(args.toolnames))
        known = {r["tool"] for r in doc["rows"]}
    globals()["KNOWN"] = known

    picks = []
    searches = []
    for log_path in args.logs:
        with open(log_path, encoding="utf-8", errors="replace") as f:
            for line in f:
                line = line.strip()
                if not line or not line.startswith("{"):
                    continue
                try:
                    obj = json.loads(line)
                except json.JSONDecodeError:
                    continue
                for block in walk_tool_uses(obj):
                    bare, chain = normalize(block)
                    if bare == "tool_search":
                        searches.append(chain)
                        continue
                    in_known = bare in known if known else bare.startswith("mcp__")
                    if in_known:
                        picks.append({"pick": bare, "chain": chain})
                    elif chain[0].startswith("mcp__"):
                        print(f"UNKNOWN_MCP_TOOL\t{chain[0]}", file=sys.stderr)

    collapsed = []
    for p in picks:
        if collapsed and collapsed[-1]["pick"] == p["pick"]:
            continue
        collapsed.append(p)
    for p in collapsed:
        print(json.dumps(p, ensure_ascii=False))
    if searches:
        print(f"tool_search_steps={len(searches)}", file=sys.stderr)
    if not picks:
        print("NO_PICK", file=sys.stderr)


if __name__ == "__main__":
    main()
