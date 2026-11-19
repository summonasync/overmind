#!/usr/bin/env python3
"""Generate kit-membership.json from the four mirror sides of the crew kit.

Membership is derived from what is on disk, classified by the spec R1 rules
(feature 250). Re-run after a mirror-parity change (rust skills landing on the
other sides, new skills) and commit the regenerated file.

    python3 tools/gen-kit-membership.py            # rewrite the manifest
    python3 tools/gen-kit-membership.py --check     # drift gate, writes nothing
"""

import difflib
import glob
import json
import os
import sys

USAGE = """usage: gen-kit-membership.py [--check]

  --check       generate in memory, compare with kit-membership.json on disk,
                exit 1 when it drifts; the file is never written
  -h, --help    show this help without touching the manifest
"""

SIDES = [".claude", ".codex", ".kimi-code", ".qwen"]

CEREMONY = {
    "architect-review",
    "audit-orchestrator",
    "coding-challenge-orchestrator",
    "documentation-writer",
    "fast-fixer-coder",
    "feature-orchestrator",
    "naming-sensei",
    "overmind-ceremony",
    "pipeline-doctor",
    "product-strategist",
    "solution-architect",
    "sprint-planner",
    "wave-marshal",
}

EXTRAS = {
    "ai-code-detector",
    "ai-obfuscation-reviewer",
    "ai-obfuscator",
    "augur-architect",
    "augur-challenger",
    "augur-estimator",
    "augur-method",
    "augur-orchestrator",
    "augur-researcher",
    "git-commit-ai-detector",
    "maui-coder",
    "squeezr-ops",
    "tutor-master",
    "umbraco-sensei",
}

# Per-side contract prose, workflow graph, wake recipe and runtime config: the
# mandatory core owns them, a side without its contract is not a working crew.
CEREMONY_PROSE = {
    ".claude/CLAUDE.md",
    ".claude/SKILLFLOW.md",
    ".claude/settings.json",
    ".codex/AGENTS.md",
    ".codex/SKILLFLOW.md",
    ".kimi-code/AGENTS.md",
    ".kimi-code/SKILLFLOW.md",
    ".kimi-code/WAKE-CHANNEL.md",
    ".kimi-code/config.toml",
    ".qwen/QWEN.md",
    ".qwen/SKILLFLOW.md",
    ".qwen/hooks/post-edit-verify.ps1",
    ".qwen/hooks/post-edit-verify.sh",
    ".qwen/settings-snippet.jsonc",
}

# Single source in the repo, deployed into all four homes by install.sh/ps1.
SOCIALITY_PROSE = {".kimi-code/SOCIAL_DICTIONARY.md"}

KITS = ["overmind-ceremony", "dotnet-crew", "rust-crew", "js-crew", "sociality", "crew-extras"]


def classify_item(name: str) -> str:
    if name in CEREMONY:
        return "overmind-ceremony"
    if name in EXTRAS:
        return "crew-extras"
    if name.startswith("dotnet-") or name == "nuget-licensing-guard":
        return "dotnet-crew"
    if name.startswith("rust-") or name == "cargo-licensing-guard":
        return "rust-crew"
    if name.startswith(("web-", "node-", "js-platform-")) or name == "frontend-designer":
        return "js-crew"
    if name.startswith(("go-", "python-")):
        return "crew-extras"
    raise ValueError(f"unclassified content item: {name}")


def classify_prose(path: str) -> str:
    if path in SOCIALITY_PROSE:
        return "sociality"
    if path in CEREMONY_PROSE:
        return "overmind-ceremony"
    raise ValueError(f"unclassified prose path: {path}")


def collect() -> dict:
    members: dict = {kit: [] for kit in KITS}
    for side in SIDES:
        for path in sorted(glob.glob(f"{side}/skills/*")):
            if not os.path.isdir(path):
                continue
            kit = classify_item(os.path.basename(path))
            members[kit].append(path)
        for path in sorted(glob.glob(f"{side}/agents/*")):
            name, ext = os.path.splitext(os.path.basename(path))
            if ext not in (".md", ".toml"):
                continue
            members[classify_item(name)].append(path)
        for path in sorted(glob.glob(f"{side}/*")) + sorted(glob.glob(f"{side}/hooks/*")):
            rel = path if os.path.isfile(path) else None
            if rel and rel in CEREMONY_PROSE | SOCIALITY_PROSE:
                members[classify_prose(rel)].append(rel)
    for kit in members:
        members[kit] = sorted(set(members[kit]))
    return members


def render(members: dict) -> str:
    return json.dumps(members, indent=2, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    flags = argv[1:]
    if {"-h", "--help"} & set(flags):
        sys.stdout.write(USAGE)
        return 0
    unknown = [flag for flag in flags if flag != "--check"]
    if unknown:
        sys.stderr.write(f"unrecognized argument: {unknown[0]}\n\n{USAGE}")
        return 2
    check = "--check" in flags

    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    os.chdir(root)
    members = collect()
    generated = render(members)
    total = sum(len(paths) for paths in members.values())

    if check:
        try:
            with open("kit-membership.json", encoding="utf-8") as handle:
                on_disk = handle.read()
        except FileNotFoundError:
            print("kit-membership.json is missing — run without --check to generate it",
                  file=sys.stderr)
            return 1
        if on_disk.replace("\r\n", "\n") == generated:
            print(f"kit-membership.json matches the mirror sides: {total} paths")
            return 0
        print("kit-membership.json is stale — regenerate it without --check", file=sys.stderr)
        report = difflib.unified_diff(on_disk.splitlines(), generated.splitlines(),
                                      fromfile="kit-membership.json", tofile="generated",
                                      lineterm="")
        for line in list(report)[:20]:
            print(line, file=sys.stderr)
        return 1

    with open("kit-membership.json", "w", encoding="utf-8", newline="\n") as handle:
        handle.write(generated)
    print(f"wrote kit-membership.json: {total} paths")
    for kit in KITS:
        print(f"  {kit}: {len(members[kit])}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
