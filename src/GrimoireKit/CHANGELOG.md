# Changelog

## 0.1.0 — 2026-11-22

Kit skeleton: installer plumbing, manifest, runtime profiles, docs.

- `manifest.json` v2-style entry list (source / home / type / mode / dest) with
  crew-kit mirror/merge semantics: `skills/memory-ops` merges additively into
  claude and qwen skills dirs; the guard hook mirrors into kit-owned
  `hooks/grimoire-kit/` subpaths; per-runtime hook wiring (settings path, event,
  command) lives in a `hooks` section.
- `install.sh` / `install.ps1`: idempotent, merge-safe install and uninstall
  (`--home`, `--runtime claude|qwen|all`, `--uninstall`, `--dry-run`). settings.json
  hook entries are merged by a real JSON parser (`tools/settings_merge.py`,
  shared by both installers) — foreign hooks and settings content survive;
  JSONC settings files are refused, not mangled.
- `pack.sh`: self-contained client bundle (dir + tar.gz) from the manifest version.
- `runtime-profiles/qwen.md`: VERIFIED cap constants (25000 UTF-16 units / 200
  lines / 150 chars per line, warn margins 1500 units / 10 lines — qwen bundle
  0.27.x, source: legacy qwen-memory-ops audit.py), index link format, archive
  tome naming (`qwen-memory-archive[-<project-alias>]`), memory paths.
  `runtime-profiles/claude.md` arrives with the claude cap measurement.
- `skills/memory-ops` and `scripts/guard_hook.py`: placeholders — skill body and
  hook logic land in subsequent releases.
