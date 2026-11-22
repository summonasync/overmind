"""Shared constants and helpers for the memory-ops scripts.

PROFILES is the executable projection of runtime-profiles/<runtime>.md (the
markdown profiles are the human-facing source of truth — keep the numbers in
sync when a profile changes). A JSON file passed as --profile replaces the
built-in table entirely for custom trees.
"""
import glob
import json
import os
import re

LEGAL_TYPES = ("user", "feedback", "project", "reference")

PROFILES = {
    "claude": {
        "runtime": "claude",
        "memories_dir": None,
        "project_globs": ["~/.claude/projects/*/memory"],
        "caps": {"units": 25000, "lines": 200, "line_chars": None},
        "warn": {"units": 24000, "lines": 190, "line_chars": 200},
        "lines_count_all": True,
        "index_header": "# Memory Index",
        "link_truncate": None,
        "type_location": "metadata",
        "tome_global": "claude-memory-archive",
        "tome_project_fmt": "claude-memory-archive-{alias}",
        "provenance": {
            "units": "VERIFIED bracket [25001, 25015) 2026-11-22, T1 probes F1/F6/F4/F5; 25000 is the nominal guard",
            "lines": "VERIFIED 2026-11-22, T1 probes P2/P3a (cut exactly at file line 200, header and blanks included)",
            "line_chars": "no runtime cap (P3b: 265-char lines intact); 200 is the runtime's own advisory",
        },
    },
    "qwen": {
        "runtime": "qwen",
        "memories_dir": "~/.qwen/memories",
        "project_globs": ["~/.qwen/projects/*/memory"],
        "caps": {"units": 25000, "lines": 200, "line_chars": 150},
        "warn": {"units": 23500, "lines": 190, "line_chars": None},
        "lines_count_all": False,
        "index_header": None,
        "link_truncate": 150,
        "type_location": "top",
        "tome_global": "qwen-memory-archive",
        "tome_project_fmt": "qwen-memory-archive-{alias}",
        "provenance": {
            "units": "VERIFIED qwen bundle 0.27.x (legacy qwen-memory-ops audit.py, measured in lib/chunks/chunk-S6UXV6IV.js)",
            "lines": "VERIFIED qwen bundle 0.27.x, same source",
            "line_chars": "VERIFIED qwen bundle 0.27.x, same source",
        },
    },
}


def load_profile(runtime=None, profile_path=None):
    if profile_path:
        with open(os.path.expanduser(profile_path), encoding="utf-8") as fh:
            prof = json.load(fh)
        prof.setdefault("runtime", runtime or "custom")
        return prof
    if runtime not in PROFILES:
        raise SystemExit(f"unknown runtime '{runtime}'; known: {', '.join(sorted(PROFILES))} (or pass --profile FILE)")
    return PROFILES[runtime]


def default_dirs(prof):
    dirs = []
    if prof.get("memories_dir"):
        d = os.path.expanduser(prof["memories_dir"])
        if os.path.isdir(d):
            dirs.append(d)
    for pat in prof.get("project_globs", []):
        for d in sorted(glob.glob(os.path.expanduser(pat))):
            if os.path.isdir(d) and any(n.endswith(".md") for _, _, ns in os.walk(d) for n in ns):
                dirs.append(d)
    return sorted(set(dirs))


def units_of(text):
    return len(text.encode("utf-16-le")) // 2


def read_entry(path):
    """Parse one memory file: {'frontmatter': flat dict, 'body': str} or None on OSError.

    Frontmatter keys are flattened: for the claude shape (type nested under the
    metadata block) indented keys land in the same dict, so frontmatter['type']
    resolves for both profile shapes.
    """
    try:
        text = open(path, encoding="utf-8").read()
    except OSError:
        return None
    fm = {}
    m = re.match(r"^---\n(.*?)\n---\n?", text, re.S)
    if m:
        in_meta = False
        for line in m.group(1).splitlines():
            top = re.match(r"^(\w+):\s*(.*)$", line)
            sub = re.match(r"^\s+(\w+):\s*(.*)$", line)
            if top:
                fm[top.group(1)] = top.group(2).strip().strip('"')
                in_meta = top.group(1) == "metadata"
            elif in_meta and sub:
                fm[sub.group(1)] = sub.group(2).strip().strip('"')
    body = text.split("---\n", 2)[-1] if text.startswith("---") else text
    return {"frontmatter": fm, "body": body}
