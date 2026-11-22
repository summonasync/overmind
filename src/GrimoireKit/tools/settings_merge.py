#!/usr/bin/env python3
"""settings.json hook-entry merge for grimoire-kit installers.

Both install.sh and install.ps1 delegate here so the merge semantics have one
implementation: existing entries (foreign hooks included) are never rewritten,
kit entries are keyed by the kit marker path and are therefore idempotent.
Installers use `apply`, which builds the entry from the manifest in-process —
no JSON ever crosses the argv boundary (Windows PowerShell 5.1 mangles quoted
native args).
"""
import json
import os
import sys

USAGE = """usage:
  settings_merge.py apply ROOT RUNTIME SETTINGS MARKER  build entry from manifest, merge into SETTINGS
  settings_merge.py entry ROOT RUNTIME                  print the {event: [group]} hook entry JSON
  settings_merge.py merge PATH EVENT ENTRY MARKER       merge pre-built entry JSON into PATH
  settings_merge.py unmerge PATH MARKER                 remove marker-matched entries from PATH
"""


def read_json(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def write_json(path, data):
    tmp = f"{path}.grimoire-kit-tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
        f.write("\n")
    os.replace(tmp, path)


def not_json(path, err):
    sys.stderr.write(f"grimoire-kit: {path} is not valid JSON ({err}) - fix or merge by hand, see README\n")
    sys.exit(1)


def build_entry(root, runtime):
    m = read_json(os.path.join(root, "manifest.json"))
    h = m["hooks"][runtime]
    inner = {"type": "command", "command": h["command"], "timeout": h["timeout"]}
    for k in ("name", "description", "shell", "statusMessage"):
        if h.get(k):
            inner[k] = h[k]
    return {h["event"]: [{"matcher": h["matcher"], "hooks": [inner]}]}


def merge(path, event, entry, marker):
    try:
        data = read_json(path)
    except FileNotFoundError:
        data = {}
    except json.JSONDecodeError as e:
        not_json(path, e)

    if marker in json.dumps(data).replace("\\\\", "/"):
        print(f"grimoire-kit: hook entry already present in {path}, nothing to do")
        return

    hooks = data.setdefault("hooks", {})
    if not isinstance(hooks, dict):
        sys.stderr.write(f"grimoire-kit: 'hooks' in {path} is not an object - merge the entry by hand\n")
        sys.exit(1)
    groups = hooks.setdefault(event, [])
    if not isinstance(groups, list):
        sys.stderr.write(f"grimoire-kit: hooks.{event} in {path} is not a list - merge the entry by hand\n")
        sys.exit(1)
    groups.extend(entry[event])
    write_json(path, data)
    print(f"grimoire-kit: hook entry added to {path} [{event}]")


def ours(hook, marker):
    cmd = str(hook.get("command", "")) if isinstance(hook, dict) else str(hook)
    return marker in cmd.replace("\\", "/") or marker in json.dumps(hook).replace("\\\\", "/")


def unmerge(path, marker):
    try:
        data = read_json(path)
    except FileNotFoundError:
        return
    except json.JSONDecodeError as e:
        not_json(path, e)

    hooks = data.get("hooks")
    if not isinstance(hooks, dict):
        return

    removed = 0
    for event in list(hooks.keys()):
        groups = hooks[event]
        if not isinstance(groups, list):
            continue
        kept = []
        for group in groups:
            if not isinstance(group, dict):
                kept.append(group)
                continue
            inner = group.get("hooks")
            if not isinstance(inner, list):
                kept.append(group)
                continue
            filtered = [h for h in inner if not ours(h, marker)]
            removed += len(inner) - len(filtered)
            if filtered:
                group["hooks"] = filtered
                kept.append(group)
        if kept:
            hooks[event] = kept
        else:
            del hooks[event]
    if not hooks:
        data.pop("hooks", None)

    if removed == 0:
        print(f"grimoire-kit: no hook entries of ours in {path}")
        return
    write_json(path, data)
    print(f"grimoire-kit: removed {removed} hook entr{'y' if removed == 1 else 'ies'} from {path}")


def apply(root, runtime, settings_path, marker):
    """Build the runtime's hook entry from its manifest and merge it in one go.

    Installers call this instead of ferrying the entry JSON through argv: plain
    strings only cross the process boundary, which Windows PowerShell 5.1
    handles without quote mangling.
    """
    entry = build_entry(root, runtime)
    event = next(iter(entry))
    merge(settings_path, event, entry, marker)


def main(argv):
    if len(argv) >= 4 and argv[1] == "entry":
        print(json.dumps(build_entry(argv[2], argv[3])))
        return 0
    if len(argv) >= 6 and argv[1] == "merge":
        merge(argv[2], argv[3], json.loads(argv[4]), argv[5])
        return 0
    if len(argv) >= 4 and argv[1] == "unmerge":
        unmerge(argv[2], argv[3])
        return 0
    if len(argv) >= 6 and argv[1] == "apply":
        apply(argv[2], argv[3], argv[4], argv[5])
        return 0
    sys.stderr.write(USAGE)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
