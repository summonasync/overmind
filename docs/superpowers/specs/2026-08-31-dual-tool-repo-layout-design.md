# Design: Dual-tool repo layout (.kimi-code + .claude)

Date: 2026-08-31
Status: approved (verbal), pending written-spec review

## Goal

Restructure the repo so it serves as the single source of truth for TWO agent runtimes:
Kimi Code CLI (`~/.kimi-code`) and Claude Code (`~/.claude`). Same agent crew, same skills,
adapted to each tool's conventions, with an explicit sync contract to prevent drift.

## Chosen approach

**Mirror with full adapted copies** (approved over: shared-core generator, symlinks).

- Physical `git mv` of current content into `.kimi-code/` — single home for Kimi files.
- `.claude/` holds full, adapted copies of agents and skills (no symlinks, no shared references).
- Duplication risk is managed by `SYNC-STRUCTURE.md` (explicit file-to-file mapping + edit-both rule).

Rejected: generator/templating layer (YAGNI for markdown), symlinks (Windows pain + diverging frontmatter formats anyway).

## Target layout

```
├── .kimi-code/
│   ├── agents/                  # git mv from agents/ (17 files)
│   ├── skills/                  # git mv from skills/ (18 dirs)
│   ├── TASK/                    # git mv from TASK/ (_TEMPLATE.md + future task files)│   ├── SOCIAL_DICTIONARY.md     # git mv from root
│   └── config.toml              # git mv from root (Kimi-specific config)
├── .claude/
│   ├── agents/                  # adapted copies, Claude frontmatter
│   ├── skills/                  # adapted full copies, Kimi refs replaced
│   └── TASK/                    # verbatim mirror of .kimi-code/TASK/
├── CLAUDE.md                    # full port of AGENTS.md, Kimi-specifics swapped to Claude equivalents
├── AGENTS.md                    # stays in root; updated to reflect new paths/structure
├── README.md                    # repo overview + prerequisites + sync usage
├── SYNC-STRUCTURE.md            # dual-edit contract + file mapping
├── sync.ps1                     # deploy: .kimi-code/* -> ~/.kimi-code, .claude/* + CLAUDE.md -> ~/.claude
├── sync.sh                      # same for *nix
└── .gitignore
```

`TASK/` is part of the global workflow documentation system (referenced by AGENTS.md), not Kimi-specific
config. It moves to `.kimi-code/TASK/` and is mirrored verbatim to `.claude/TASK/` (template content is
tool-agnostic — identical copy, covered by SYNC-STRUCTURE.md). Both AGENTS.md and CLAUDE.md reference it
via tool-home-relative paths (`~/.kimi-code/TASK/...` / `~/.claude/TASK/...`).

## Adaptation rules: Kimi -> Claude

### Agents (.claude/agents/*.md)

- Frontmatter: `name`, `description`, `tools`, `model`.
- Skill references: `~/.kimi-code/skills/<name>/SKILL.md` -> `~/.claude/skills/<name>/SKILL.md`.
- Model mapping (sub-agent rules): K2.7 (default coder/reviewer) -> `sonnet`; K3 (orchestration, audit, AI-code detection, obfuscation) -> `opus`. Cheap/fast tier -> `haiku` where a skill mentions lightweight work.

### Skills (.claude/skills/<name>/SKILL.md)

- Content copied in full; code/style/workflow rules unchanged (they are tool-agnostic).
- Kimi-specific fragments replaced: model names (K2.7/K3 -> sonnet/opus), paths (`~/.kimi-code` -> `~/.claude`), Kimi CLI features (secondary_model pool, `Agent`/`AgentSwarm` params) -> Claude Code equivalents (Task tool, `model` frontmatter field).

### CLAUDE.md (root)

- Full port of AGENTS.md content.
- `Sub-Agent Rules` rewritten for Claude Code: allowed models `sonnet`/`opus` (sonnet default, opus for orchestration/audit/obfuscation), enforcement via `.claude/settings.json` note instead of `config.toml` pool.
- `Documentation Files` table: TASK paths updated to `~/.kimi-code/TASK/...`; the mirrored copy on the Claude side is `~/.claude/TASK/...`.
- Everything else (tone, slang, .NET stack, frontend stack, AI-traces, workflow) carried over verbatim.

## README.md (root)

- What this repo is: single source of truth for the agent crew across Kimi Code CLI and Claude Code.
- **Prerequisites section (explicit install requirements):**
  - Kimi Code CLI + the `superpowers` plugin installed at `~/.kimi-code/plugins/managed/superpowers` — the workflow skills (brainstorming, writing-plans, TDD, etc.) depend on it.
  - Claude Code; optionally the Superpowers plugin for Claude Code for workflow parity.
- Layout overview (`.kimi-code/` vs `.claude/`), sync script usage (`sync.ps1` / `sync.sh`, dry-run), pointer to `SYNC-STRUCTURE.md` for the dual-edit rule.

## SYNC-STRUCTURE.md

- Mapping table: every `.kimi-code/skills/X` <-> `.claude/skills/X`, `.kimi-code/agents/Y` <-> `.claude/agents/Y`, `AGENTS.md` <-> `CLAUDE.md`.
- Rule: edit one side -> edit the other in the same commit.
- Pre-commit checklist: pairs in sync? Kimi-specifics not leaked into `.claude/`? Claude-specifics not leaked into `.kimi-code/`?
- Drift markers: each adapted file keeps no AI-style banner; drift is caught by review, not by file headers.

## Sync script

- `sync.ps1` (primary, Windows): robocopy `/MIR` `.kimi-code/agents` -> `~/.kimi-code/agents`, `.kimi-code/skills` -> `~/.kimi-code/skills`, `.kimi-code/TASK` -> `~/.kimi-code/TASK`, plus config/dictionary; robocopy `.claude/agents`, `.claude/skills`, `.claude/TASK` -> `~/.claude/...`, copy `CLAUDE.md` -> `~/.claude/CLAUDE.md`.
- `-DryRun` switch (robocopy `/L`).
- `sync.sh` — cp -r based equivalent, `--dry-run` via rsync if available, else plain cp.
- Idempotent; no deletes outside the mirrored target dirs.

## Error handling / risks

- `git mv` preserves history; verify with `git log --follow`.
- Claude frontmatter validation: each agent file must parse (name/description/tools/model) — checked manually in review gate.
- Risk of drift between copies: mitigated only by SYNC-STRUCTURE.md discipline (accepted trade-off).

## Testing / verification

- After move: `git status` clean renames, no lost files (17 agents, 18 skills, TASK/, dictionary, config).
- Diff audit: every `.claude` file contains zero `~/.kimi-code` / `K2.7` / `K3` / `kimi-code` strings (grep gate).
- Reverse grep: `.kimi-code/` contains zero `~/.claude` / `sonnet` / `opus` strings.
- `sync.ps1 -DryRun` prints expected copy plan without touching the filesystem.
