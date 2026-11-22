# Qwen runtime profile

Data document consumed by the `memory-ops` skill and the session-start guard hook.
Every cap constant carries a provenance flag: `VERIFIED` means measured against a
named runtime build, `UNVERIFIED` means a conservative budget-guard.

## Paths

| what               | value                                        |
|--------------------|----------------------------------------------|
| global memories dir | `~/.qwen/memories`                          |
| per-project cache   | `~/.qwen/projects/<project-alias>/memory`   |
| index file          | `MEMORY.md` (global and per-project)        |

## Index format

- Link line: `- [name](relpath) — description`
- `description` is truncated by the runtime at 150 chars (chars, not bytes); lines
  past the limit are cut mid-content, so the tail of a description can disappear
  without the entry vanishing from the index.

## Caps

| constant              | value              | provenance |
|-----------------------|--------------------|------------|
| `MAX_INDEX_CHARS`     | 25000 UTF-16 units | VERIFIED — qwen bundle 0.27.x; source: legacy `qwen-memory-ops` skill `scripts/audit.py` (`~/.qwen/skills/qwen-memory-ops/scripts/audit.py`), which encodes the runtime's real eviction budget |
| `MAX_INDEX_LINES`     | 200                | VERIFIED — same source |
| `MAX_INDEX_LINE_CHARS`| 150                | VERIFIED — same source |

## Guard thresholds (warn margins)

- units: warn at `MAX_INDEX_CHARS − 1500` units (the legacy audit's `EVICT_MARGIN`)
- lines: warn at `MAX_INDEX_LINES − 10`

## Archive tomes

| scope        | tome name                                    |
|--------------|----------------------------------------------|
| global       | `qwen-memory-archive`                        |
| per-project  | `qwen-memory-archive-<project-alias>`        |

Project alias comes from the grimoire project registry; an unknown alias is a
STOP-and-ask, never a guess (a wrong tome means lost keys).

## Hook wiring

- script: `~/.qwen/hooks/grimoire-kit/guard_hook.py`
- settings: `~/.qwen/settings.json`, `hooks.SessionStart`, matcher `startup|resume`
- command: `python3 "$HOME/.qwen/hooks/grimoire-kit/guard_hook.py"`, shell `bash`
- timeout: `30` — seconds for command hooks since bundle 0.27.x; a value of
  `1000` or more is read as legacy milliseconds (verified in the 0.27.0 and
  0.28.0 bundle hook schemas), so the ms-era `30000` form is deprecated and
  flagged per session in the qwen debug log
- the hook warns only; it never writes and never blocks a session
