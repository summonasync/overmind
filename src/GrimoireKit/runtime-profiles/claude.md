# Runtime profile: Claude Code auto-memory

Runtime: Claude Code CLI 2.1.268 (Linux). Probed 2026-11-22, headless black-box
canary probes (`claude -p --model sonnet --max-turns 1` against a scratch
project tree `~/.claude/projects/-tmp-grimkit-probe/memory/`; transcripts in
`audits/grimkit-t1-probes/`, feature 270 T1).

Unit convention: 1 unit = 1 UTF-16 code unit =
`len(text.encode('utf-16-le'))//2`. All probe content was ASCII plus the BMP
em dash (U+2014), where units == raw chars == `len(text)`. Astral chars
(emoji, 2 units per char) were NOT probed: whether the cap counts them as 2
units or 1 char is UNVERIFIED. Char and unit numbers below are therefore
identical by construction; conversion is identity for BMP content.

## Paths

- Per-project memory dir: `~/.claude/projects/<project-slug>/memory/`
- Project slug: absolute cwd with `/` replaced by `-` (e.g. cwd `/tmp/grimkit-probe` → `-tmp-grimkit-probe`). VERIFIED (scratch tree was picked up by sessions with cwd `/tmp/grimkit-probe`, probe P0).
- Index file: `MEMORY.md` directly in the memory dir; entry files sit next to it, flat (no subdirs observed in live trees).
- Global memory dir: none observed for this mechanism; global context comes from `~/.claude/CLAUDE.md` (out of scope here).

## Index format

- Entry line: `- [Title](file.md) — description` (em dash separator). VERIFIED against live trees (Overmind project memory) and probe round-trips: probes with this shape were injected and quoted verbatim.
- `MEMORY.md` starts with `# Memory Index` header line plus one blank line in live trees. The cap counts them: claude counts ALL file lines, header and blanks included (see CAP_LINES).
- Entry files carry YAML frontmatter: `name`, `description`, `metadata` (`node_type: memory`, `type: user|feedback|project|reference`, `originSessionId`, `modified`), then a free body. Frontmatter is used by the runtime when writing entries; only the index line is injected into context.

## Injection mechanics

- `MEMORY.md` content is injected into every session start, headless `-p` mode included. VERIFIED (P0: 20-row index, 20/20 entries quoted verbatim).
- Injection is line-granular: lines are included whole or not at all; a non-fitting line never appears partially in the loaded block (VERIFIED: the last loaded line always matched disk byte-for-byte in P2/P3a/P3b/E1a).
- No per-line length cap exists (see CAP_LINE_CHARS).

## Caps

| Constant | Value | Status |
|---|---|---|
| CAP_LINES | 200 file lines (header + blank + entries all count) | VERIFIED 2026-11-22 (probes P2, P3a: 207- and 212-line files cut exactly at file line 200; advisory names "starting at line 201") |
| CAP_UNITS | ~25,0xx UTF-16 units; measured bracket: 25,001 units loaded whole, 25,015 units cut. Nominal guard value: 25,000. The runtime's own advisory displays the limit as "24.4KB" (= 25,000/1024 rounded) | VERIFIED bracket 2026-11-22 (probes F1/F6/F4/F5: 25,001 in, 25,015 out, 25,030 out, 25,045 out); exact internal constant UNVERIFIED (lies in [25,001, 25,015)) |
| CAP_LINE_CHARS | none — no per-line truncation. Claude's advisory only RECOMMENDS "under ~200 chars" per entry | VERIFIED 2026-11-22 (probe P3b: 265-char entries quoted verbatim to the end) |
| Astral unit cost | 2 units per astral char assumed (qwen precedent), not probed | UNVERIFIED |
| Whitespace semantics | newline counted as 1 unit; caps measured on LF-only files. CRLF files unprobed (real trees are LF) | UNVERIFIED (irrelevant in practice) |

Both caps are active simultaneously; whichever triggers first cuts the load
(probe P3a: 12,806 units — far under the unit cap — still cut at line 200;
probe P3b: 102 file lines — far under the line cap — still cut at ~25,0xx units).

## Overflow behavior: TRUNCATED + WARNED (never silent, never refused)

At or above either cap the runtime loads the fitting prefix and appends a
`WARNING` blockquote that is itself visible to the model in the same context.
The session continues normally. Verbatim formats (captured 2026-11-22):

Unit cap trigger (probe E1c, tree of 107 file lines / 25,096 units):

```
> WARNING: MEMORY.md is 24.5KB (limit: 24.4KB) — index entries are too long. Only part of it was loaded: 1 of 107 lines were cut off, starting at line 107 ("- [Final row TEST-A](row-test-a.md) — canary=TESTA-final-marker padding text…"). Keep index entries to one line under ~200 chars; move detail into topic files.
```

Line cap trigger (probe P2b, tree of 207 file lines / 25,814 units):

```
> WARNING: MEMORY.md is 207 lines and 25.2KB. Only part of it was loaded: 7 of 207 lines were cut off, starting at line 201 ("- [Memory row 199](row-199.md) — canary=R199x5178 hash tag artifact restore…"). Keep index entries to one line under ~200 chars; move detail into topic files.
```

Both variants: line-granular eviction of the first non-fitting line and all
following lines, `starting at line N` (1-based FILE line number), a quoted
preview of the first dropped line ending in `…`, and the same guidance tail.
The unit-cap variant adds the `(limit: 24.4KB)` size report.

KB display rule: shown size = total file units / 1024, 1 decimal, trailing
`.0` trimmed. VERIFIED across three advisories: 25,096 units → "24.5KB";
25,814 → "25.2KB"; 25,567 → "25KB".

## Invisible entries

A memory file present on disk but missing from the `MEMORY.md` index is NOT
surfaced: it does not appear in context, and no notice mentions unindexed
files. VERIFIED (probe P4: `ghost-entry.md` on disk, absent from index —
agent reported NO reference anywhere). Such files are reachable only when the
agent explicitly reads them. The runtime also injects static memory-system
workflow instructions (write fact files, add a one-line pointer to
`MEMORY.md`); these are generic guidance, not per-file notices.

## Import door (grimoire side)

Native door EXISTS: `grimoire auto-memory import --source <dir> --tome <key>`
(grimoire CLI 0.9.1). VERIFIED end-to-end 2026-11-22 on scratch tome
`grimkit-probe-scratch`, fixture dir `/tmp/grimkit-import-fixture`
(2 files, frontmatter + body):

- Result per file: record `kind=Fact`, `scope=Project`, tag
  `auto-memory:<filename>`, importance 0.5, actor `human`. Tag lookup by that
  tag works via `memory_list`. VERIFIED.
- Stored content is the BODY ONLY, normalized: `"\n" + body.trim() + "\n"`.
  YAML frontmatter (name, description, type, originSessionId, modified) is
  STRIPPED and NOT archived. VERIFIED: 419-byte file → 154-char record.
- Byte-exactness: restored file from record content hashes exactly to the
  state-file hash (`729d5d43…` for the beta fixture) — body round-trip is
  byte-precise modulo the normalization above. Whole-file round-trip is
  IMPOSSIBLE from the record alone; a restore tool must synthesize
  frontmatter. KEY SPEC INPUT for the kit.
- Dedup: `.grimoire-import-state.json` written into the source dir, mapping
  filename → sha256 of the NORMALIZED body (not the whole file). Re-import of
  unchanged files: `unchanged: N`, zero new records, idempotent, exit 0.
  VERIFIED.
- Deleted source files: simply not re-imported on the next pass (no error, no
  warning). VERIFIED.
- Source dir not matching a registered project: warning
  "no registered project matches the source directory '…'; importing without
  a project tag"; import proceeds. VERIFIED.
- Exit code 1 when any file fails (documented in CLI help; not triggered in
  probes — no malformed-file case exercised).

## Archive tome

Kit target tome for archived claude memory: `claude-memory-archive`
(declared constant, per feature 270 plan). Scratch verification tome for this
profile: `grimkit-probe-scratch` (safe to discard).

## Budget guards (recommended thresholds for memory-ops tooling)

- WARN when index ≥ 24,000 units (≈1,000+ units of margin below the measured
  cut bracket [25,001, 25,015)); HARD-STOP/archive trigger at 24,900.
- WARN when file lines ≥ 190 (10 lines below the measured 200-line cut).
- WARN when any single entry line > 200 chars — not because the runtime
  truncates it, but because claude's own advisory demands it and long entries
  burn the shared unit budget faster.
- Units are the binding constraint for rich descriptions (200 lines × 125
  chars ≈ 25,000 units hits both caps at once — the practical ceiling is
  ~190 entries of ~120 chars).

## Probe matrix (evidence)

| Probe | Tree | Question | Outcome |
|---|---|---|---|
| P0 | 20 rows / 2,396 units / 22 lines | injection works headless? | 20/20 verbatim |
| P1 | 195 rows / 23,773 units / 197 lines | below both hypotheses? | all visible |
| P2 | 205 rows / 25,814 units / 207 lines | past both? | cut at file line 200; rows 199–205 dropped |
| P2b | same | advisory at line cap? | WARNING captured verbatim |
| P3a | 210 short rows / 12,806 units / 212 lines | lines or units? | cut at line 200 → line cap real |
| P3b | 100 long rows / 26,392 units / 102 lines | unit cap real? per-line truncation? | cut after row 94; 265-char lines intact → unit cap real, no per-line cap |
| E1a/E1b/E1c | 104 rows + test line / 25,096 units | advisory mechanics | unit-cap WARNING captured verbatim incl `(limit: 24.4KB)` |
| F1 | total 25,001 units | boundary low side | fully loaded, no warning |
| F6 | total 25,015 units | boundary high side | cut |
| F4 | total 25,030 units | bracket | cut |
| F5 | total 25,045 units | bracket | cut |
| P4 | 2-row index + unindexed `ghost-entry.md` | invisible entry | not surfaced, no notice |

Transcripts: `audits/grimkit-t1-probes/*.out` (probe prompts and raw model
answers, exactly as received).
