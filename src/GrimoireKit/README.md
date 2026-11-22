# grimoire-kit

Memory hygiene kit for [Claude Code](https://claude.com/claude-code) and
[Qwen Code](https://github.com/QwenLM/qwen-code) sessions, backed by the
[grimoire](https://github.com/summonasync/grimoire) local memory service.

Both runtimes keep a file-based auto-memory: a `MEMORY.md` index plus
per-fact/per-project files, injected into every session. That index has no
garbage collection — entries silently fall past the budget cap and become
invisible to recall while their files stay on disk, indexes drift out of sync
with the files, near-duplicates accumulate. grimoire-kit ships the fix as one
runtime-profiled `memory-ops` skill (audit / archive / restore) plus a cheap
session-start guard hook that warns when an index nears its eviction budget.

Archive passes move bodies verbatim into grimoire tomes before anything is
deleted, so nothing is ever lost — restore can bring a record back to the
runtime, into active memory, or as a read-only answer.

## Status (0.1.0)

This is the kit skeleton: installer plumbing, manifest, profiles, docs. The
skill body and the real guard-hook logic land in subsequent releases — the
packaged `SKILL.md` and `guard_hook.py` are placeholders that do nothing.

## Requirements

- python3 — settings.json hook entries are merged with a real JSON parser, never
  by text surgery (install refuses to run without it)
- rsync — Linux/macOS installs use it for exact directory mirrors; without it the
  installer falls back to additive `cp` and tells you (stale kit files then need
  a manual clean)
- tar — only needed to unpack a bundle (or use any un-tar tool on Windows)
- grimoire MCP server or `grimoire` CLI — needed by the skill's archive/restore
  operations at usage time (the installer itself does not need it)
- Claude Code and/or Qwen Code home dir on the machine (`~/.claude` / `~/.qwen`);
  runtimes without their home dir are skipped

## Install

```
tar -xzf grimoire-kit-<version>.tar.gz
cd grimoire-kit-<version>
./install.sh                # both runtimes found on the machine
./install.sh --runtime claude   # one runtime only (claude | qwen | all)
./install.sh --dry-run      # show the plan, change nothing
```

Windows PowerShell: `install.ps1` takes `-Runtime claude|qwen|all`, `-Uninstall`,
`-DryRun`, `-BaseHome <dir>`.

Keep the unpacked bundle directory after installing: the skill reads its runtime
profile (`runtime-profiles/<runtime>.md`) from it.

`--home DIR` (PS: `-BaseHome`) redirects the install under another base home —
used for sandbox testing; under an explicit `--home` the installer creates
missing runtime dirs instead of skipping them. Do not use it for a normal
machine install.

## What it touches

Nothing outside these paths, ever:

| runtime | files added | settings change |
|---------|-------------|-----------------|
| claude  | `~/.claude/skills/memory-ops/**` (additive) | `~/.claude/settings.json`: one SessionStart hook entry pointing at `~/.claude/hooks/grimoire-kit/guard_hook.py` |
| claude  | `~/.claude/hooks/grimoire-kit/guard_hook.py` | |
| qwen    | `~/.qwen/skills/memory-ops/**` (additive) | `~/.qwen/settings.json`: one SessionStart hook entry pointing at `~/.qwen/hooks/grimoire-kit/guard_hook.py` |
| qwen    | `~/.qwen/hooks/grimoire-kit/guard_hook.py` | |

Merge discipline:

- skills and hooks land in kit-owned subpaths (`skills/memory-ops`,
  `hooks/grimoire-kit/`) — your other skills and other hooks are never mirrored
  over or deleted;
- settings.json is loaded as JSON, one entry appended under
  `hooks.SessionStart`, written back — every pre-existing key and hook entry
  survives byte-for-byte in meaning (key order is JSON-object order, python
  preserves it);
- if your settings file contains JSONC comments (some qwen builds allow that)
  the installer refuses to touch it and tells you to merge the entry by hand —
  it will never strip comments or guess;
- rerunning the installer is a no-op: the hook entry is keyed by the
  `hooks/grimoire-kit/` path marker and is not appended twice;
- the installer never removes the `hooks` key, other SessionStart entries, or
  any other settings content on uninstall — only its own entry and its own files.

The guard hook itself is read-only: it audits indexes at session start, prints a
warn line when the budget is overdue or entries are invisible, and stays silent
otherwise. It never blocks a session.

Runtime profiles are data documents (caps, warn margins, index link format,
archive tome names, memory paths). The qwen profile constants are VERIFIED
against qwen bundle 0.27.x; the claude profile ships once its cap constants have
been measured — until that release, claude-side audit runs drift checks without
a budget verdict.

## Uninstall

```
./install.sh --uninstall            # or with --runtime claude / --runtime qwen
./install.sh --uninstall --dry-run  # list what would go
```

Removes exactly the kit's own files (the `memory-ops` skill dir, the
`hooks/grimoire-kit/` dir, emptied parents) and its own settings hook entries.
Foreign skills, foreign hooks, and all other settings content survive. Kit-owned
parent dirs are removed only when they are left empty.

## Pack (for distributors)

`./pack.sh` builds `dist/grimoire-kit-<version>/` and `dist/grimoire-kit-<version>.tar.gz`
from the manifest version — the bundle is self-contained and installs on a
machine that has never seen this repository.

## Versioning

`manifest.json` `version` is the release version (mirrored in `CHANGELOG.md`);
`format` is the manifest schema version, bumped only when the entry schema
changes.

## License

Same license as the Overmind repository this kit ships in.
