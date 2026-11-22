#!/usr/bin/env python3
"""Fixture runner for the memory-ops scripts.

Asserts audit finds all five seeded anomaly classes per runtime family, the
healthy fixtures stay clean, and rebuild_index regenerates the committed
expected indexes byte-for-byte (and never writes during a dry-run). Exits
non-zero on any failure.
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.normpath(os.path.join(HERE, "..", "skills", "memory-ops", "scripts"))
FIXTURES = os.path.join(HERE, "fixtures")
PY = sys.executable or "python3"

EXPECTED = {
    "healthy": {},
    "invisible-file": {"invisible": True},
    "broken-link": {"ghost_links": True},
    "bad-type": {"bad_type": True},
    "near-dup": {"near_dup": True},
    "over-budget": {"over_budget": True},
}

failures = []
checks = 0


def check(name, cond, detail=""):
    global checks
    checks += 1
    if cond:
        print(f"  PASS {name}")
    else:
        print(f"  FAIL {name} {detail}")
        failures.append(name)


def run_audit(family, fixture):
    return subprocess.run(
        [PY, os.path.join(SCRIPTS, "audit.py"), "--runtime", family, "--json",
         os.path.join(FIXTURES, family, fixture)],
        capture_output=True, text=True)


def test_audits():
    for family in ("claude", "qwen"):
        for fixture, expected in EXPECTED.items():
            tag = f"{family}/{fixture}"
            proc = run_audit(family, fixture)
            check(f"{tag} exit 0", proc.returncode == 0, f"rc={proc.returncode} err={proc.stderr[:200]}")
            if proc.returncode != 0:
                continue
            rep = json.loads(proc.stdout)[0]
            found = rep["findings"]
            if not expected:
                check(f"{tag} clean", not any(found.values()), str({k: v for k, v in found.items() if v}))
            else:
                for key in expected:
                    check(f"{tag} finds {key}", bool(found.get(key)),
                          str({k: v for k, v in found.items() if v}))


def test_invocation_failures():
    missing = os.path.join(FIXTURES, "does-not-exist")
    proc = subprocess.run([PY, os.path.join(SCRIPTS, "audit.py"), "--runtime", "claude", missing],
                          capture_output=True, text=True)
    check("missing dir exits non-zero", proc.returncode != 0, f"rc={proc.returncode}")
    proc = subprocess.run([PY, os.path.join(SCRIPTS, "audit.py"), "--json"], capture_output=True, text=True)
    check("no runtime exits non-zero", proc.returncode != 0, f"rc={proc.returncode}")


def test_rebuild(family):
    src = os.path.join(FIXTURES, "rebuild", family, "tree")
    expected = open(os.path.join(FIXTURES, "rebuild", family, "expected-MEMORY.md"),
                    encoding="utf-8").read()
    mtimes = json.load(open(os.path.join(FIXTURES, "rebuild", family, "mtimes.json"), encoding="utf-8"))
    tag = f"rebuild/{family}"
    with tempfile.TemporaryDirectory() as tmp:
        tree = os.path.join(tmp, "tree")
        shutil.copytree(src, tree)
        for rel, ts in mtimes.items():
            os.utime(os.path.join(tree, rel), (ts, ts))
        before = open(os.path.join(tree, "MEMORY.md"), encoding="utf-8").read()
        dry = subprocess.run([PY, os.path.join(SCRIPTS, "rebuild_index.py"), "--runtime", family, tree],
                             capture_output=True, text=True)
        check(f"{tag} dry-run exit 0", dry.returncode == 0, dry.stderr[:200])
        after = open(os.path.join(tree, "MEMORY.md"), encoding="utf-8").read()
        check(f"{tag} dry-run does not write", before == after)
        applied = subprocess.run([PY, os.path.join(SCRIPTS, "rebuild_index.py"), "--runtime", family, tree, "--apply"],
                                 capture_output=True, text=True)
        check(f"{tag} apply exit 0", applied.returncode == 0, applied.stderr[:200])
        written = open(os.path.join(tree, "MEMORY.md"), encoding="utf-8").read()
        check(f"{tag} regenerates expected index", written == expected,
              f"len {len(written)} vs {len(expected)}")


def main():
    test_audits()
    test_invocation_failures()
    test_rebuild("claude")
    test_rebuild("qwen")
    print(f"\n{len(failures)} failure(s) of {checks} checks")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
