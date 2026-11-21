#!/usr/bin/env python3
"""Assignment + frontmatter check for kit-membership.json (feature 250, task T1).

Two independent gates, one exit code:

1. Membership — every skill dir, agent definition and side prose/config file on
   disk is assigned to exactly one kit: zero orphans (unassigned), zero
   duplicates, zero stale entries pointing at paths that no longer exist.
2. Frontmatter — every agent file on the markdown sides parses as YAML, no
   `- mcp__…` grant line survives below the closing `---` (feature 242), and
   check (c): every allowlist-bearing role carries exactly the ruled MCP set
   (RULED_MCP_GRANTS) and sits on the RESTRICTED_ROLES roster, while every
   rostered role carries its allowlist (feature 242, Option A trim). The
   stray-grant sweep covers every markdown file under the four sides
   (skills included), so the same defect shape cannot hide outside the agent
   files. The Codex side has no YAML frontmatter by design (TOML roles carry
   `name`, `description`, `model`, `developer_instructions` only) and is
   reported, not failed.

    python3 tools/check-kit-membership.py            # summary
    python3 tools/check-kit-membership.py --verbose  # per-side detail
"""

import glob
import json
import os
import re
import sys

SIDES = [".claude", ".codex", ".kimi-code", ".qwen"]
MD_SIDES = [".claude", ".kimi-code", ".qwen"]
GRANT_RE = re.compile(r"^\s*-\s*(mcp__\S+)\s*$")
KIT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# Feature 242 (owner ballot 2026-11-16 + Option A trim 2026-11-20): the restricted
# class — every role that carries a tools: allowlist — holds exactly this MCP set,
# nothing more. A reviewer must not be able to close the gate it signs, so the
# write-side tools (set_status, upsert_artifact, validate, claims, issues_*, ...)
# are withheld from the allowlist wholesale; squeezr tools are read-only and named
# explicitly. This list is the single source of that contract; KIT-MEMBERSHIP.md
# links here instead of restating it.
RULED_MCP_GRANTS = frozenset(
    [
        "mcp__overmind__planning_read_artifact",
        "mcp__overmind__planning_read_feature",
        "mcp__overmind__planning_list_artifacts",
        "mcp__overmind__planning_list_features",
        "mcp__overmind__list_entries",
        "mcp__overmind__read_entry",
        "mcp__overmind__search",
        "mcp__overmind__write_entry",
        "mcp__squeezr__index_status",
        "mcp__squeezr__repo_map",
        "mcp__squeezr__code_search",
        "mcp__squeezr__symbol_lookup",
        "mcp__squeezr__expand_span",
        "mcp__squeezr__find_references",
    ]
)

RESTRICTED_ROLES = frozenset(
    [
        "ai-code-detector",
        "ai-obfuscation-reviewer",
        "architect-review",
        "augur-challenger",
        "augur-researcher",
        "dotnet-code-reviewer",
        "git-commit-ai-detector",
        "go-code-reviewer",
        "js-platform-code-reviewer",
        "naming-sensei",
        "node-code-reviewer",
        "pipeline-doctor",
        "python-code-reviewer",
        "rust-code-reviewer",
        "web-code-reviewer",
    ]
)

# Repo infrastructure at the kit root: deployment mechanics, not kit content.
# Not materialized into any agent home, so not assignable to a kit.
INFRA_EXCLUDED = [
    "README.md",
    "SYNC-STRUCTURE.md",
    "kit-membership.json",
    "manifest.json",
    "install.sh",
    "install.ps1",
    "uninstall.sh",
    "uninstall.ps1",
    "tools",
    "BACKLOG",
    "BACKLOG.md",
    "audits",
    "audits/audit-2026-09-01.md",
    ".gitattributes",
    ".gitignore",
]


def disk_items():
    items = []
    for side in SIDES:
        for path in sorted(glob.glob(f"{side}/skills/*")):
            if os.path.isdir(path):
                items.append(path)
        for path in sorted(glob.glob(f"{side}/agents/*")):
            if os.path.splitext(path)[1] in (".md", ".toml"):
                items.append(path)
        for path in sorted(glob.glob(f"{side}/*")) + sorted(glob.glob(f"{side}/hooks/*")):
            if os.path.isfile(path):
                items.append(path)
    return items


def check_membership(manifest, errors):
    counts = {kit: len(paths) for kit, paths in manifest.items()}
    seen = {}
    for kit, paths in manifest.items():
        for path in paths:
            if path in seen:
                errors.append(f"duplicate assignment: {path} in {seen[path]} and {kit}")
            else:
                seen[path] = kit
            if not os.path.exists(path):
                errors.append(f"stale manifest entry (no such path): {kit} -> {path}")

    on_disk = set(disk_items())
    orphans = sorted(on_disk - set(seen))
    for path in orphans:
        errors.append(f"orphan: {path} assigned to no kit")
    return counts, len(seen), len(on_disk), orphans


def frontmatter_bounds(lines):
    if not lines or lines[0].strip() != "---":
        return None
    for i in range(1, len(lines)):
        if lines[i].strip() == "---":
            return i
    return None


def side_md_files():
    """Every markdown file under the four side dirs. glob's `*` skips dot-names,
    so the side roots are walked explicitly — otherwise the whole kit reads clean
    while zero side files were actually opened."""
    for side in SIDES:
        for root, _dirs, names in os.walk(side):
            for name in sorted(names):
                if name.endswith(".md"):
                    yield os.path.join(root, name)


def check_stray_grants(errors, notes):
    """Feature 242's defect shape: the `mcp__…` grant list sitting below the
    closing `---`, i.e. as body prose instead of frontmatter. Swept across every
    side markdown file, skills included, not just the agent files.

    A file without frontmatter bounds is skipped — and a skipped file is a file
    the sweep never opened, so the skipped denominator is printed (T5/S1: a green
    run must show how much of the tree it actually looked at). A SKILL.md without
    frontmatter is a defect, not a skip: the skill loaders parse exactly that
    block, so it hard-fails instead of silently shrinking the scan."""
    scanned = 0
    skipped = []
    stray = 0
    for path in side_md_files():
        lines = open(path, encoding="utf-8").read().split("\n")
        close = frontmatter_bounds(lines)
        if close is None:
            skipped.append(path)
            continue
        scanned += 1
        hits = [i + 1 for i, line in enumerate(lines) if i > close and GRANT_RE.match(line)]
        if hits:
            stray += len(hits)
            errors.append(
                f"{path}: {len(hits)} mcp grant lines below the frontmatter (first at line {hits[0]})"
            )
    notes.append(
        f"side markdown files swept for stray grants: {scanned}   skipped (no frontmatter): {len(skipped)}   stray lines: {stray}"
    )
    for path in skipped:
        print(f"  skipped: {path}")
    bad_skill_md = [p for p in skipped if re.fullmatch(r"\.\w[\w-]*/skills/[^/]+/SKILL\.md", p)]
    for path in bad_skill_md:
        errors.append(f"{path}: SKILL.md without frontmatter — excluded from the sweep and unloadable as a skill")


def check_frontmatter(errors, notes):
    import yaml

    parsed = 0
    granted = []
    restricted_seen = {side: set() for side in MD_SIDES}
    for side in MD_SIDES:
        for path in sorted(glob.glob(f"{side}/agents/*.md")):
            lines = open(path, encoding="utf-8").read().split("\n")
            close = frontmatter_bounds(lines)
            if close is None:
                errors.append(f"{path}: no closing --- for frontmatter")
                continue
            try:
                data = yaml.safe_load("\n".join(lines[1:close]))
            except Exception as exc:
                errors.append(f"{path}: frontmatter YAML rejected: {str(exc).splitlines()[0]}")
                continue
            if not isinstance(data, dict) or "name" not in data:
                errors.append(f"{path}: frontmatter is not a mapping with a name")
                continue
            parsed += 1
            role = data["name"]
            tools = data.get("tools") or []
            grant = [t for t in tools if isinstance(t, str) and t.startswith("mcp__")]
            if grant:
                granted.append((path, len(grant)))
                # Check (c): an allowlist-bearing role is the restricted class —
                # its MCP grants must equal the ruled set exactly, and the role
                # must be on the RESTRICTED_ROLES roster (an allowlist appearing
                # on an orchestrator would widen a gate-signing role by accident).
                extra = sorted(set(grant) - RULED_MCP_GRANTS)
                missing = sorted(RULED_MCP_GRANTS - set(grant))
                if extra:
                    errors.append(f"{path}: non-ruled MCP grants present ({', '.join(extra)})")
                if missing:
                    errors.append(f"{path}: ruled MCP grants missing ({', '.join(missing)})")
                if role not in RESTRICTED_ROLES:
                    errors.append(
                        f"{path}: allowlist on role '{role}' outside RESTRICTED_ROLES — "
                        "restrict the role or extend the roster deliberately"
                    )
                restricted_seen[side].add(role)
            stray = [
                (i + 1, m.group(1))
                for i, line in enumerate(lines)
                if i > close and (m := GRANT_RE.match(line))
            ]
            if stray:
                errors.append(
                    f"{path}: {len(stray)} mcp grant lines below the frontmatter (first at line {stray[0][0]})"
                )
    for side in MD_SIDES:
        absent = sorted(RESTRICTED_ROLES - restricted_seen[side])
        for role in absent:
            errors.append(f"{side}/agents/{role}.md: restricted role carries no allowlist — trimmed by accident?")
    notes.append(f"markdown agent files parsed: {parsed}")
    notes.append(f"files with the mcp grant inside frontmatter: {len(granted)}")
    by_side = {}
    for path, count in granted:
        by_side.setdefault(path.split("/")[0], []).append(count)
    for side in MD_SIDES:
        sizes = sorted(set(by_side.get(side, [])))
        notes.append(
            f"  {side}: {len(by_side.get(side, []))} grant-bearing roles"
            + (f" (grant size {sizes})" if len(sizes) == 1 else "")
        )
    notes.append(
        f"restricted-class check (c): roster {len(RESTRICTED_ROLES)} roles x {len(MD_SIDES)} sides, "
        f"ruled MCP set = {len(RULED_MCP_GRANTS)} tools"
    )
    return granted


def check_codex(notes):
    # TOML roles have no frontmatter; the grant mechanism does not exist there.
    # The strict deserializer drops a role carrying any field beyond the four
    # allowed ones, so a `tools` list cannot be added — see SYNC-STRUCTURE.md.
    allowed = {"name", "description", "model", "developer_instructions"}
    import tomllib

    rows = []
    for path in sorted(glob.glob(".codex/agents/*.toml")):
        with open(path, "rb") as handle:
            data = tomllib.load(handle)
        extra = sorted(set(data) - allowed)
        rows.append((path, extra))
    notes.append(f"codex TOML roles parsed: {len(rows)} (no YAML frontmatter by design)")
    offenders = [path for path, extra in rows if extra]
    return offenders


def main():
    verbose = "--verbose" in sys.argv
    os.chdir(KIT_ROOT)
    errors = []
    notes = []
    with open("kit-membership.json", encoding="utf-8") as handle:
        manifest = json.load(handle)

    counts, assigned, on_disk, orphans = check_membership(manifest, errors)
    granted = check_frontmatter(errors, notes)
    check_stray_grants(errors, notes)
    codex_offenders = check_codex(notes)
    if codex_offenders:
        errors.extend(f"{path}: codex role carries non-TOML-allowed fields" for path in codex_offenders)

    print("kit-membership check")
    print(f"  kit root: {KIT_ROOT}")
    print("  per-kit path counts:")
    for kit, count in counts.items():
        print(f"    {kit:<18} {count}")
    print(f"  assigned paths: {assigned}   distinct paths on disk: {on_disk}   orphans: {len(orphans)}")
    print(f"  duplicates / stale entries: {len([e for e in errors if 'duplicate' in e or 'stale' in e])}")

    if verbose:
        print("  per-side coverage:")
        for side in SIDES:
            skills = [p for p in manifest_paths(manifest) if p.startswith(f"{side}/skills/")]
            agents = [p for p in manifest_paths(manifest) if p.startswith(f"{side}/agents/")]
            prose = [p for p in manifest_paths(manifest) if p.startswith(f"{side}/") and p not in skills and p not in agents]
            print(f"    {side:<12} skills={len(skills)} agents={len(agents)} prose/config={len(prose)}")
        print("  grant sizes (mcp__ entries inside frontmatter):")
        for path, count in granted:
            print(f"    {path} {count}")
        print("  kit-root infrastructure excluded from membership:")
        for path in INFRA_EXCLUDED:
            print(f"    {path}")

    for note in notes:
        print("  " + note)
    if errors:
        print(f"RESULT: {len(errors)} problem(s)")
        for err in errors[:40]:
            print("  ! " + err)
        return 1
    print("RESULT: PASS — every item assigned exactly once, zero orphans, grant inside frontmatter on every markdown side")
    return 0


def manifest_paths(manifest):
    return [p for paths in manifest.values() for p in paths]


if __name__ == "__main__":
    raise SystemExit(main())
