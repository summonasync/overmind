#!/usr/bin/env python3
"""memory-ops audit: index budget, invisible entries, drift, frontmatter types, near-duplicates, merge clusters.

Usage:
  audit.py --runtime claude|qwen [--json] [dir ...]
  audit.py --profile FILE [--json] [dir ...]

Without dirs the profile's default tree is audited. Anomalies are findings,
not errors: exit 0 on healthy and on findings, non-zero only on real failures
(bad invocation, missing dir, unreadable index).
"""
import argparse
import difflib
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common

FINDING_KEYS = ("over_budget", "invisible", "ghost_links", "bad_type", "near_dup", "unreadable")


def audit_dir(base, prof):
    rep = {
        "dir": base,
        "units": 0,
        "lines": 0,
        "entries": 0,
        "over_line_chars": 0,
        "over": False,
        "hot": False,
        "findings": {k: [] for k in FINDING_KEYS},
    }
    idx_path = os.path.join(base, "MEMORY.md")
    raw = ""
    if os.path.exists(idx_path):
        try:
            raw = open(idx_path, encoding="utf-8").read()
        except OSError as exc:
            print(f"unreadable index {idx_path}: {exc}", file=sys.stderr)
            raise
    rep["units"] = units = common.units_of(raw)
    links = []
    for line in raw.splitlines():
        if not line.startswith("- ["):
            continue
        rep["entries"] += 1
        cap_lc = prof["caps"].get("line_chars")
        if cap_lc and len(line) > cap_lc:
            rep["over_line_chars"] += 1
        m = re.search(r"\]\(([^)]+)\)", line)
        if m:
            links.append(m.group(1))
    rep["lines"] = len(raw.splitlines()) if prof["lines_count_all"] else rep["entries"]

    caps, warn = prof["caps"], prof["warn"]
    rep["over"] = units >= caps["units"] or rep["lines"] >= caps["lines"]
    rep["hot"] = units >= warn["units"] or rep["lines"] >= warn["lines"]
    if rep["over"]:
        why = []
        if units >= caps["units"]:
            why.append(f"units {units}/{caps['units']}")
        if rep["lines"] >= caps["lines"]:
            why.append(f"lines {rep['lines']}/{caps['lines']}")
        rep["findings"]["over_budget"].append(" + ".join(why))

    files = {}
    for root, _, names in os.walk(base):
        for n in names:
            if n.endswith(".md") and n != "MEMORY.md":
                rel = os.path.relpath(os.path.join(root, n), base)
                files[rel] = os.path.join(root, n)

    trunc = prof.get("link_truncate")
    def visible(rel):
        if rel in raw or (trunc and rel[:30] in raw):
            return True
        if trunc:
            try:
                head = open(files[rel], encoding="utf-8").read(400)
            except OSError:
                return False
            m = re.match(r"^---\nname:\s*\"?(.*?)\"?\n", head)
            return bool(m) and m.group(1) in raw
        return False
    rep["findings"]["invisible"] = sorted(p for p in files if not visible(p))

    fileset = set(files)
    def link_resolves(target):
        if target in fileset:
            return True
        return bool(trunc and any(p.startswith(target[:30]) for p in fileset))
    rep["findings"]["ghost_links"] = sorted({t for t in links if not link_resolves(t)})

    bodies = {}
    for rel in sorted(files):
        entry = common.read_entry(files[rel])
        if entry is None:
            rep["findings"]["unreadable"].append(rel)
            continue
        t = entry["frontmatter"].get("type")
        if t not in common.LEGAL_TYPES:
            rep["findings"]["bad_type"].append([rel, t])
        bodies[rel] = re.sub(r"\s+", " ", entry["body"])[:4000]
    keys = sorted(bodies)
    for i, a in enumerate(keys):
        for b in keys[i + 1:]:
            if difflib.SequenceMatcher(None, bodies[a], bodies[b]).ratio() > 0.85:
                rep["findings"]["near_dup"].append([a, b])
    rep["files"] = keys
    return rep


def print_report(rep, prof):
    f = rep["findings"]
    print(f"\n=== {rep['dir']} ===")
    flag = "OVER" if rep["over"] else ("HOT" if rep["hot"] else "ok")
    headroom = prof["caps"]["units"] - rep["units"]
    lc = prof["caps"].get("line_chars")
    tail = f" · over-{lc} lines {rep['over_line_chars']}" if lc else \
           f" · over-{prof['warn']['line_chars']}-char lines {rep['over_line_chars']} (advisory)"
    print(f"budget: {rep['units']}/{prof['caps']['units']} units [{flag}] · "
          f"lines {rep['lines']}/{prof['caps']['lines']} · entries {rep['entries']}{tail} · "
          f"headroom {headroom} units")
    if f["invisible"]:
        print(f"NOT INDEXED (silent knowledge): {len(f['invisible'])}")
        for p in f["invisible"]:
            print(f"  - {p}")
    if f["ghost_links"]:
        print(f"index links without files: {len(f['ghost_links'])}")
        for p in f["ghost_links"]:
            print(f"  - {p}")
    if f["bad_type"]:
        print(f"illegal frontmatter type (legal: {'|'.join(common.LEGAL_TYPES)}): {len(f['bad_type'])}")
        for p, t in f["bad_type"]:
            print(f"  - {p}: type={t!r}")
    if f["near_dup"]:
        print("near-duplicate pairs (>0.85):")
        for a, b in f["near_dup"]:
            print(f"  - {a}  ~  {b}")
    if f["unreadable"]:
        print(f"unreadable files (skipped): {f['unreadable']}")

    clusters = {}
    for p in rep["files"]:
        clusters.setdefault(os.path.dirname(p), []).append(p)
    print("\nmerge candidates (group by subdir, judge by topic):")
    for d, ps in sorted(clusters.items(), key=lambda kv: -len(kv[1])):
        if len(ps) >= 2 or d == "":
            tag = d or "(root)"
            if d in ("tool_experience", "feedback", "reference", "project") and len(ps) < 6:
                continue
            print(f"  {tag}: {len(ps)} files")
    byword = {}
    for p in rep["files"]:
        for w in re.split(r"[-_/]", os.path.splitext(os.path.basename(p))[0]):
            if len(w) >= 5:
                byword.setdefault(w, []).append(p)
    hot_words = [(w, ps) for w, ps in byword.items() if 3 <= len(ps) <= 8]
    if hot_words:
        print("shared-keyword groups:")
        for w, ps in sorted(hot_words, key=lambda x: -len(x[1]))[:8]:
            print(f"  '{w}' ×{len(ps)}: {', '.join(sorted(ps))}")

    if rep["over"] or rep["hot"] or f["invisible"]:
        print(f"\narchive pass overdue — run the memory-ops archive pass "
              f"(target tome: {prof['tome_global']} / {prof['tome_project_fmt']})")


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--runtime", choices=sorted(common.PROFILES))
    ap.add_argument("--profile", help="JSON profile file replacing the built-in table")
    ap.add_argument("--json", action="store_true", help="machine-readable report (findings only, no clusters)")
    ap.add_argument("dirs", nargs="*")
    args = ap.parse_args()
    if not args.runtime and not args.profile:
        ap.error("--runtime or --profile is required")
    prof = common.load_profile(args.runtime, args.profile)
    if args.dirs:
        bad = [d for d in args.dirs if not os.path.isdir(os.path.expanduser(d))]
        if bad:
            print(f"not a directory: {', '.join(bad)}", file=sys.stderr)
            return 2
        dirs = [os.path.expanduser(d) for d in args.dirs]
    else:
        dirs = common.default_dirs(prof)
        if not dirs:
            print("no memory dirs found for this profile", file=sys.stderr)
            return 2
    reports = [audit_dir(d, prof) for d in dirs]
    if args.json:
        for r in reports:
            r.pop("files", None)
        print(json.dumps(reports, ensure_ascii=False, indent=2))
    else:
        for r in reports:
            print_report(r, prof)
        print(f"\nTOTAL units across indexes: {sum(r['units'] for r in reports)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
