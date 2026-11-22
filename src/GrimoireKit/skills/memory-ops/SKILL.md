---
name: memory-ops
description: "Auto-memory hygiene for this runtime: audit the MEMORY.md indexes (budget, invisible entries, drift), archive bodies into grimoire tomes before deletion, restore from archive."
---

# memory-ops — audit / archive / restore for runtime auto-memory

Generalized over runtime profiles. The profile for the current runtime is
`runtime-profiles/<runtime>.md` next to this skill in the installed bundle; it
is data, not code. Select it with `--runtime claude|qwen` (built-in constants
mirror the profile doc) or `--profile <file.json>` for a custom tree. Never
copy one runtime's mechanics onto the other:

- claude: overflow is truncated with a visible WARNING (line-granular, no
  per-line cap, both caps ~25,000 UTF-16 units / 200 file lines including the
  header count); index line `- [Title](file.md) — description`, files flat;
  frontmatter `type` lives under the `metadata:` block.
- qwen: overflow silently evicts the OLDEST entries (index sorted by mtime
  desc) and cuts descriptions at 150 chars mid-content; index line
  `- [name](relpath) — description` truncated, files may sit in subdirs;
  frontmatter `type` is a top-level key.

Owner ruling 2026-11-21, carried verbatim from the qwen-local predecessor:
cold memory knowledge is archived to grimoire tomes named after the runtime —
global memories go to `<runtime>-memory-archive`, project memories go to
`<runtime>-memory-archive-<project-alias>` (grimoire project registry alias,
see `grimoire projects`). NEVER hard-delete without the archive landing first.

## 0. Principle

- **Archive first, delete second** — every removed file must exist in an archive tome
  byte-for-byte as its body, findable by tag. This is non-negotiable, no exceptions,
  including "it's trivial".
- Merging and selection are judgment calls: the audit PROPOSES clusters, the acting
  agent (with the owner when available) DECIDES. Never auto-merge across unrelated topics.
- The runtime regenerates MEMORY.md at its own checkpoints; after filesystem-level edits
  the index stays stale — rebuild it mechanically (section 2, step 5).
- Bodies are sacred: merging rewrites index lines and prose wrappers, it never re-summarizes
  an archived body (archived copies are verbatim; the merged file may point to them).

## 1. audit

Run the script (mechanical, read-only), from this skill's directory:

```bash
python3 scripts/audit.py --runtime <runtime>            # profile default tree
python3 scripts/audit.py --runtime <runtime> <dir ...>  # explicit dirs
python3 scripts/audit.py --runtime <runtime> --json <dir>   # machine-readable findings
```

It reports per index: UTF-16 units vs the profile cap, lines vs the line cap
(claude counts all file lines including the header; qwen counts entry lines),
over-length lines (qwen: the 150-char cut budget; claude: the 200-char
advisory), files vs index in BOTH directions (files on disk missing from the
index — silent knowledge; index links pointing at missing files — the drift
class), frontmatter `type` violations (only `user|feedback|project|reference`
are legal — a bad category name silently erases the entry), near-duplicate
bodies (>0.85 similarity), and proposed merge clusters grouped by subdirectory
and shared title keywords.

Exit code is 0 on a healthy tree AND on findings; non-zero means a real
failure (bad invocation, missing dir, unreadable index) — treat it as a bug,
not as dirt.

- **Auto-trigger:** past the profile warn thresholds (claude: 24,000 units /
  190 lines; qwen: 23,500 units / 190 lines), or any invisible file → run the
  archive pass NOW. Headroom below one day of work is not a number to carry —
  it is a task.
- Clusters are candidates, not decisions. Present them to the owner as a
  compact list (N groups, files each) when he is on-screen; proceed with the
  obvious same-class merges when he is not, keeping every body in the archive
  regardless.
- A SessionStart guard hook (kit `scripts/guard_hook.py`) warns into every
  fresh session when the state is overdue — treat its line as a standing task.

## 2. archive

For each index (global memories dir first, then each project cache with a hot
index):

1. **Merge**: write merged files first (keep the richest body verbatim under a
   `## <path>` heading per source — see the cold-drawer pattern — or
   prose-merge when one rule subsumes another; record which sources folded).
2. **Tome check**: probe the tome (`grimoire list --tome <tome> --limit 1`
   style); if it does not exist, create it via MCP `memory_tome_create` with a
   description naming the source index and the `auto-memory:<file>` tag
   convention.
3. **Bulk import (the native door)**:
   ```bash
   grimoire auto-memory import --source <dir-or-chunk-dir> --tome <runtime>-memory-archive --json
   ```
   - tags each record `auto-memory:<relative-file>`, project-tags from the
     registry, dedups on rerun, writes an import marker IN THE SOURCE dir
     (idempotent, resumable).
   - For per-file control or when the marker would pollute a live dir, stage
     the doomed files into a scratch dir first (same filesystem), then import
     from there.
   - Project caches: `--tome <runtime>-memory-archive-<project-alias>` (alias
     from `grimoire projects`; if the alias is unknown, STOP and ask — wrong
     tome = lost keys).
   - The door stores the normalized BODY only; frontmatter is stripped. That
     is expected — the restore operation synthesizes it (section 3).
4. **Verify before rm**: every doomed file resolves via
   `grimoire list --tome <tome> --tag auto-memory:<path>` (or MCP memory_list)
   with a matching body hash. Only then `rm` the originals. No marker, no rm.
5. **Rebuild the index**: regenerate each MEMORY.md from CURRENT frontmatter
   with the script — never hand-edit content lines:
   ```bash
   python3 scripts/rebuild_index.py --runtime <runtime> <dir> [--apply]
   ```
6. **Report**: units before → after, files removed, tomes + marker paths,
   anything left because verification failed.

## 3. restore

- Find: `grimoire recall "<query>" --tome <tome>` or browse by tag
  `grimoire list --tome <tome> --tag auto-memory:<old/path.md>`; MCP
  `memory_scry` works too when your tome policy allows. The tag IS the old
  path — restore needs no map.
- Decide the landing shape with the requester:
  - **back to runtime**: write the archived body verbatim to its original
    path, then rebuild the index line (section 2 step 5). The import door
    strips frontmatter, so SYNTHESIZE it: `name`/`description` from the
    record (or the old index line), path from the `auto-memory:` tag, `type`
    defaulting to `project` unless the requester says otherwise (claude: under
    `metadata:`; qwen: top-level), plus the back-link below.
  - **to active memory**: `memory_remember` into the working tome as a durable
    fact (kind=Fact), citing the archive record id;
  - **read-only**: answer from the record, restore nothing.
- Restored records stay in the archive tome (append-only truth); the runtime
  copy links back via `archive://<memoryId>` in its frontmatter — qwen: an
  entry in `keywords:`, claude: a `metadata.archive: "archive://<id>"` field.

## 4. Hygiene cadence

The guard hook reports overdue state automatically — when it fires (or any
audit shows past the warn thresholds), run the archive pass in the same
conversation; do not leave the owner a number without the action. After any
pass report units before → after and the tome + marker paths so recovery is
proven, not promised.
