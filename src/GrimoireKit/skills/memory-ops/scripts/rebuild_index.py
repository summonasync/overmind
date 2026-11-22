#!/usr/bin/env python3
"""memory-ops rebuild: regenerate MEMORY.md index lines from current frontmatter.

Usage:
  rebuild_index.py --runtime claude|qwen <dir> [--apply]
  rebuild_index.py --profile FILE <dir> [--apply]

Dry-run by default; --apply writes the index in place. Rows sort by file mtime
descending (ties break alphabetically). Committed <!-- --> note lines at the
tail of the old index survive. Claude entries keep their description verbatim
(the runtime never truncates them); qwen entries shrink the description until
the line fits the 150-char budget so the link target never gets cut.
"""
import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common


def entry_line(rel, path, prof, advisory):
    entry = common.read_entry(path)
    if entry is None:
        print(f"skip unreadable: {rel}", file=sys.stderr)
        return None
    fm = entry["frontmatter"]
    title = fm.get("name") or os.path.splitext(os.path.basename(rel))[0]
    desc = fm.get("description", "")
    prefix = f"- [{title}]({rel})"
    line = f"{prefix} — {desc}" if desc else prefix
    lim = prof.get("link_truncate")
    if lim and len(line) > lim and desc:
        keep = lim - (len(line) - len(desc))
        if keep >= 2:
            desc = desc[: keep - 1].rstrip() + "…"
            line = f"{prefix} — {desc}"
    if lim and len(line) > lim:
        line = line[:lim]
    warn_lc = prof["warn"].get("line_chars")
    if warn_lc and len(line) > warn_lc:
        advisory.append(f"{rel}: index line is {len(line)} chars (advisory max {warn_lc})")
    return line


def rebuild(base, prof, apply):
    idx = os.path.join(base, "MEMORY.md")
    old = open(idx, encoding="utf-8").read() if os.path.exists(idx) else ""
    notes = [l for l in old.splitlines() if l.startswith("<!--")]
    files = {}
    for root, _, names in os.walk(base):
        for n in names:
            if n.endswith(".md") and n != "MEMORY.md":
                p = os.path.join(root, n)
                files[os.path.relpath(p, base)] = p
    advisory = []
    rows = []
    for rel in sorted(files, key=lambda p: (-os.path.getmtime(files[p]), p)):
        line = entry_line(rel, files[rel], prof, advisory)
        if line is not None:
            rows.append(line)
    parts = []
    if prof.get("index_header"):
        parts.extend([prof["index_header"], ""])
    parts.extend(rows)
    if notes:
        parts.append("")
        parts.extend(notes)
    out = "\n".join(parts) + "\n"
    if apply:
        with open(idx, "w", encoding="utf-8") as fh:
            fh.write(out)
    for a in advisory:
        print(f"advisory: {a}")
    print(f"{base}: {len(rows)} entries, {common.units_of(out)} units, "
          f"{len(out.splitlines())} file lines {'WRITTEN' if apply else '(dry-run)'}")
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--runtime", choices=sorted(common.PROFILES))
    ap.add_argument("--profile", help="JSON profile file replacing the built-in table")
    ap.add_argument("dir")
    ap.add_argument("--apply", action="store_true", help="write MEMORY.md (default: dry-run)")
    args = ap.parse_args()
    if not args.runtime and not args.profile:
        ap.error("--runtime or --profile is required")
    prof = common.load_profile(args.runtime, args.profile)
    base = os.path.expanduser(args.dir)
    if not os.path.isdir(base):
        print(f"not a directory: {args.dir}", file=sys.stderr)
        return 2
    rebuild(base, prof, args.apply)
    return 0


if __name__ == "__main__":
    sys.exit(main())
