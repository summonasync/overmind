#!/usr/bin/env python3
"""Rebuild the matrix score from saved raw logs with the FINAL extractor.

Reads runs/<runtime>/<runtime>/<intent>.jsonl (raw headless stream), re-extracts the
picks chain, and scores two columns per intent:
  first_match — the expected tool is the FIRST MCP pick in the chain
  any_match   — the expected tool appears anywhere in the chain
AC criterion: any_match (selection happened); first_match is the strict delta column
T6 must compare — a Y->N flip there is a selection regression even if any stays Y.

Usage: score_matrix.py --runtime claude|qwen|kimi [--runs-root DIR] [--intents TSV]
                       [--wire JSON] [--out TSV] [--old TSV]
--old diffs the fresh score against a previous results tsv (mixed-method guard).
"""

import argparse
import json
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent


def extract(log, wire, script):
    out = subprocess.run([sys.executable, str(script), "--toolnames", str(wire), str(log)],
                         capture_output=True, text=True)
    picks = []
    for line in out.stdout.splitlines():
        try:
            picks.append(json.loads(line)["pick"])
        except (KeyError, json.JSONDecodeError):
            pass
    return picks


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--runtime", required=True)
    ap.add_argument("--runs-root", default="/mnt/Ubuntu-Dev/scratch/overmind-234")
    ap.add_argument("--intents", default=str(HERE / "intents.tsv"))
    ap.add_argument("--wire", default="/mnt/Ubuntu-Dev/scratch/overmind-234/out/wire.json")
    ap.add_argument("--out")
    ap.add_argument("--old")
    args = ap.parse_args()

    root = pathlib.Path(args.runs_root)
    run_dir = root / "runs" / args.runtime / args.runtime
    intents = {}
    for line in pathlib.Path(args.intents).read_text().splitlines():
        parts = line.split("\t")
        if len(parts) >= 3 and parts[0] != "id":
            intents[parts[0]] = parts[1]

    old = {}
    if args.old and pathlib.Path(args.old).exists():
        for line in pathlib.Path(args.old).read_text().splitlines()[1:]:
            f = line.split("\t")
            if len(f) >= 5:
                old[f[1]] = (f[3], f[4], f[5] if len(f) > 5 else "")

    rows = []
    for intent in sorted(intents):
        log = run_dir / f"{intent}.jsonl"
        if not log.exists():
            rows.append([args.runtime, intent, intents[intent], "", "MISS", "MISS", "no-log", ""])
            continue
        picks = extract(log, args.wire, HERE / "extract_picks.py")
        chain = ",".join(picks)
        expected = intents[intent]
        first = "Y" if picks and picks[0] == expected else "N"
        anym = "Y" if expected in picks else ("NONE" if not picks else "N")
        rc = ""
        exit_file = run_dir / f"{intent}.exit"
        if exit_file.exists():
            rc = exit_file.read_text().strip()
        rows.append([args.runtime, intent, expected, chain, first, anym, rc, ""])
        if intent in old:
            old_picks, old_match, _ = old[intent]
            if old_picks != chain:
                print(f"DRIFT {intent}: old=[{old_picks}] match={old_match} new=[{chain}] any={anym}")

    out_tsv = pathlib.Path(args.out) if args.out else root / f"results-{args.runtime}-scored.tsv"
    lines = ["runtime\tintent\texpected\tpicks\tfirst_match\tany_match\trc"]
    lines += ["\t".join(r[:7]) for r in rows]
    out_tsv.write_text("\n".join(lines) + "\n")
    fm = sum(1 for r in rows if r[4] == "Y")
    am = sum(1 for r in rows if r[5] == "Y")
    print(f"{args.runtime}: first_match {fm}/{len(rows)}  any_match {am}/{len(rows)}  -> {out_tsv}")


if __name__ == "__main__":
    main()
