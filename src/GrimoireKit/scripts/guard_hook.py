#!/usr/bin/env python3
"""grimoire-kit session-start guard: cheap auto-memory index audit.

One warn line per index that is near its budget or holds files the index no
longer surfaces; silence on healthy trees. Reads only, exits 0 in every
branch — a missing or broken memory tree must never block or noise a session.

Thresholds and paths come from runtime-profiles/<runtime>.md. claude warns at
24000 units / 190 lines (caps ~25000/200; the runtime itself truncates with a
WARNING once past the cap, this hook is the earlier headroom nudge). qwen
warns at 23500 units / 190 lines (caps 25000/200; eviction there is silent,
so this hook is the only early warning a qwen session gets).
"""
import json
import os
import re
import sys

CAP_UNITS = 25000
CAP_LINES = 200
LINE_MARGIN = 10

PROFILES = {
    "claude": {
        "global_dir": None,
        "projects_root": ".claude/projects",
        "warn_units": 24000,
        "warn_lines": CAP_LINES - LINE_MARGIN,
        "count_all_lines": True,
        "truncates": True,
    },
    "qwen": {
        "global_dir": ".qwen/memories",
        "projects_root": ".qwen/projects",
        "warn_units": CAP_UNITS - 1500,
        "warn_lines": CAP_LINES - LINE_MARGIN,
        "count_all_lines": False,
        "truncates": False,
    },
}


def session_cwd():
    try:
        if sys.stdin is None or sys.stdin.isatty():
            return None
        data = json.loads(sys.stdin.read())
    except (OSError, ValueError):
        return None
    if not isinstance(data, dict):
        return None
    cwd = data.get("cwd")
    return cwd if isinstance(cwd, str) and cwd else None


def pick_runtimes(flag):
    if flag in PROFILES:
        return [flag]
    here = os.path.realpath(sys.argv[0]).replace("\\", "/")
    named = [n for n in PROFILES if f"/.{n}/" in here]
    return named or list(PROFILES)


def target_dirs(prof, cwd):
    home = os.path.expanduser("~")
    bases = []
    if prof["global_dir"]:
        bases.append(os.path.join(home, prof["global_dir"]))
    if cwd:
        bases.append(os.path.join(home, prof["projects_root"], cwd.replace("/", "-"), "memory"))
    return list(dict.fromkeys(bases))


def index_visible(rel, path, raw):
    if rel in raw or rel[:30] in raw:
        return True
    try:
        with open(path, encoding="utf-8", errors="ignore") as f:
            head = f.read(400)
    except OSError:
        return False
    m = re.match(r"^---\nname:\s*\"?(.*?)\"?\n", head)
    return bool(m) and m.group(1) in raw


def unindexed_files(base, raw):
    missing = []
    for root, _, files in os.walk(base):
        for name in files:
            if not name.endswith(".md") or name == "MEMORY.md":
                continue
            path = os.path.join(root, name)
            rel = os.path.relpath(path, base)
            if not index_visible(rel, path, raw):
                missing.append(rel)
    return missing


def audit_dir(base, prof):
    index = os.path.join(base, "MEMORY.md")
    if not os.path.isdir(base) or not os.path.isfile(index):
        return None
    with open(index, encoding="utf-8", errors="replace") as f:
        raw = f.read()
    units = len(raw.encode("utf-16-le")) // 2
    file_lines = raw.splitlines()
    lines = len(file_lines) if prof["count_all_lines"] else sum(
        1 for line in file_lines if line.startswith("- ["))

    if units >= prof["warn_units"] or lines >= prof["warn_lines"]:
        fate = ("the runtime will truncate it with a WARNING" if prof["truncates"]
                else "oldest entries are next to silent eviction")
        return (f"grimoire-kit: {base}: {units}/{CAP_UNITS} units, {lines}/{CAP_LINES} lines — "
                f"index near its budget, {fate}; archive pass overdue (run the memory-ops skill)")

    missing = unindexed_files(base, raw)
    if missing:
        listed = ", ".join(missing[:3]) + ("…" if len(missing) > 3 else "")
        noun = "file" if len(missing) == 1 else "files"
        return (f"grimoire-kit: {base}: {len(missing)} memory {noun} missing from the index "
                f"({listed}) — rebuild it (memory-ops skill)")
    return None


def main(argv):
    flag = "auto"
    if "--runtime" in argv:
        i = argv.index("--runtime")
        if i + 1 < len(argv):
            flag = argv[i + 1]
    if flag != "auto" and flag not in PROFILES:
        sys.stderr.write(f"grimoire-kit: unknown --runtime '{flag}', falling back to auto\n")
        flag = "auto"
    try:
        cwd = session_cwd() or os.getcwd()
        warns = []
        for name in pick_runtimes(flag):
            for base in target_dirs(PROFILES[name], cwd):
                line = audit_dir(base, PROFILES[name])
                if line:
                    warns.append(line)
        if warns:
            print("\n".join(warns))
    except Exception:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
