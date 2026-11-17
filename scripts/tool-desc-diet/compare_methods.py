#!/usr/bin/env python3
"""Join wire.json and literals.json into a per-tool delta markdown table.

Usage: compare_methods.py WIRE.json LITERALS.json [--md out.md]
Tools present on one side only are reported explicitly — never silently dropped.
"""

import argparse
import json


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("wire")
    ap.add_argument("literals")
    ap.add_argument("--md")
    args = ap.parse_args()

    w = {r["tool"]: r for r in json.load(open(args.wire))["rows"]}
    l = {r["tool"]: r for r in json.load(open(args.literals))["rows"]}
    names = sorted(set(w) | set(l), key=lambda n: -(w.get(n, {}).get("total_chars", 0)))

    lines = ["| # | Tool | Wire desc | Wire params | Wire total | Literal total | Delta (wire−literal) |",
             "|---|------|----------:|------------:|-----------:|--------------:|---------------------:|"]
    wt = lt = 0
    for i, n in enumerate(names, 1):
        wr, lr = w.get(n), l.get(n)
        wtot = wr["total_chars"] if wr else 0
        ltot = (lr["tool_desc_chars"] + lr["param_desc_chars"]) if lr else 0
        wt += wtot
        lt += ltot
        delta = wtot - ltot
        note = "" if wr and lr else (" WIRE_ONLY" if wr else " LITERAL_ONLY")
        lines.append(f"| {i} | {n}{note} | {wr['tool_desc_chars'] if wr else '—'} | "
                     f"{wr['param_desc_chars'] if wr else '—'} | {wtot:,} | {ltot:,} | "
                     f"{delta:+,} |")
    lines.append(f"| — | **TOTAL** |  |  | **{wt:,}** | **{lt:,}** | **{wt - lt:+,}** |")
    doc = "\n".join(lines) + "\n"
    print(f"wire_total={wt} literal_total={lt} delta={wt - lt:+} tools={len(names)}")
    if args.md:
        open(args.md, "w", encoding="utf-8").write(doc)
        print(f"wrote {args.md}")


if __name__ == "__main__":
    main()
