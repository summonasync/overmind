# Bob.Kit-Uncensored

> One agent crew. Three runtimes. Zero corporate polish.

Single source of truth for one AI agent crew running on three runtimes: **Kimi Code CLI**, **Claude Code**, and **Google Antigravity**. The repo holds all mirrors side by side; `.kimi-code/` (incl. `AGENTS.md`) serves Kimi Code CLI, `.claude/` (incl. `CLAUDE.md`) serves Claude Code, `.gemini/` (incl. `GEMINI.md`) serves Antigravity. Content (rules, workflows, skills, agents) is semantically identical across the three; only tool-specific details (model names, home paths, runtime features, enforcement mechanics) differ.

The crew: orchestrators, coders, reviewers, architects, an audit machine, an AI-trace obfuscator, a CI self-healer (`pipeline-doctor`) and a naming maniac (`naming-helper`) — 20 agents, 22 skills, one workflow graph.

## Prerequisites

- **Kimi Code CLI** (v2 engine).
- **superpowers plugin for Kimi Code CLI — REQUIRED** at `~/.kimi-code/plugins/managed/superpowers`. The crew's workflow skills (brainstorming, writing-plans, TDD, systematic-debugging, verification gates) depend on it; without the plugin the pipeline breaks.
- **Claude Code**.
- **Superpowers plugin for Claude Code** — required for workflow parity on the Claude side (same skill set, different packaging).
- **Google Antigravity** — skills/agents deploy to `~/.gemini/antigravity/` (open `SKILL.md` standard, picked up natively); rules deploy as `~/.gemini/GEMINI.md`. Superpowers plugin is optional here — without it the crew follows `SKILLFLOW.md` transitions directly.

## Layout

```
.
├── SYNC-STRUCTURE.md      # triple-edit contract: file mapping, translation table, checklist
├── manifest.json          # deploy manifest: single source of truth for install/uninstall scripts
├── install.ps1            # deploy to tool homes (Windows); -DryRun prints plan only
├── install.sh             # deploy to tool homes (Git Bash/Linux); --dry-run prints plan only
├── uninstall.ps1          # remove deployed paths (Windows); -DryRun / -Force
├── uninstall.sh           # remove deployed paths (Git Bash/Linux); --dry-run / --force
├── .kimi-code/
│   ├── AGENTS.md          # Kimi-side crew contract (global tool memory)
│   ├── agents/            # 20 agent definitions
│   ├── skills/            # 22 skill directories
│   ├── SKILLFLOW.md       # workflow graph shared by all skills
│   ├── SOCIAL_DICTIONARY.md  # tone/slang vocabulary (Kimi home in repo; deployed to all three homes)
│   └── config.toml        # Kimi runtime config (secondary-model pool) — Kimi only
├── .claude/
│   ├── CLAUDE.md          # Claude-side mirror of AGENTS.md
│   ├── agents/            # 20 agent definitions (Claude format)
│   ├── skills/            # 22 skill directories
│   └── SKILLFLOW.md
├── .gemini/
│   ├── GEMINI.md          # Antigravity-side mirror of AGENTS.md (deploys to ~/.gemini/)
│   └── antigravity/
│       ├── agents/        # 20 agent definitions (deploys to ~/.gemini/antigravity/)
│       ├── skills/        # 22 skill directories (open SKILL.md standard)
│       └── SKILLFLOW.md
└── BACKLOG/                 # feature tracking — lives in repo root ONLY (templates are inside each tool dir)
```

## Install / update deployment to tool homes

Install scripts read `manifest.json` and copy repo content into the runtime home directories (`~/.kimi-code/`, `~/.claude/`, `~/.gemini/`, `~/.gemini/antigravity/`). A home that does not exist is skipped with a note (for `antigravity`, an existing `~/.gemini/` is enough — the subdirectory is created on copy). Both support a dry-run mode that prints the full copy plan without touching the filesystem:

```powershell
powershell -File install.ps1 -DryRun    # plan only
powershell -File install.ps1            # apply
```

```bash
./install.sh --dry-run                  # plan only
./install.sh                            # apply
```

Uninstall removes exactly the manifest's deployed paths from existing homes (never the home root itself), with empty-parent cleanup:

```powershell
powershell -File uninstall.ps1 -DryRun  # plan only
powershell -File uninstall.ps1          # asks for confirmation
powershell -File uninstall.ps1 -Force   # no confirmation
```

```bash
./uninstall.sh --dry-run                # plan only
./uninstall.sh                          # asks for confirmation
./uninstall.sh --force                  # no confirmation
```

What goes where (mirrors `manifest.json`):

| Source (repo) | Destination |
|---------------|-------------|
| `.kimi-code/agents/`, `.kimi-code/skills/`, `.kimi-code/BACKLOG/`, `.kimi-code/SKILLFLOW.md`, `.kimi-code/config.toml`, `.kimi-code/AGENTS.md` | `~/.kimi-code/` |
| `.claude/agents/`, `.claude/skills/`, `.claude/BACKLOG/`, `.claude/SKILLFLOW.md`, `.claude/CLAUDE.md` | `~/.claude/` |
| `.gemini/GEMINI.md` | `~/.gemini/` |
| `.gemini/antigravity/agents/`, `.gemini/antigravity/skills/`, `.gemini/antigravity/SKILLFLOW.md`, `.kimi-code/BACKLOG/` (runtime-agnostic templates) | `~/.gemini/antigravity/` |
| `.kimi-code/SOCIAL_DICTIONARY.md` | all three homes |

`BACKLOG/` itself (feature folders, BACKLOG.md) is never deployed — only `.kimi-code/BACKLOG/` / `.claude/BACKLOG/` (templates) go to the homes.

> Bash note: `install.sh` uses `rsync -a --delete` when rsync is available (true mirror, like robocopy `/MIR`); without rsync it falls back to `cp -r`, which is **additive only** — stale files left in the home dirs are not deleted.

> Note: `install.*` / `uninstall.*` + `manifest.json` replaced the older `sync.ps1` / `sync.sh` (see `BACKLOG/001-dual-tool-repo-layout/`, task 05 log).

## Known issues

- **`~/.gemini/GEMINI.md` conflicts with the Gemini CLI**: both Antigravity and the Gemini CLI hardcode the same global config path and overwrite each other's rules (tracked in [google-gemini/gemini-cli#16058](https://github.com/google-gemini/gemini-cli/issues/16058)). If you use both tools, expect GEMINI.md edits from one side to leak into the other; re-run `install.*` to restore the crew version.

## Editing rule

Any change to crew behavior, agents, skills, or policies must be applied to **all three mirrors in the same commit**. File triples, allowed tool-specific divergences, the Kimi→Claude and Kimi→Antigravity translation tables, and the pre-commit grep checklist are defined in [SYNC-STRUCTURE.md](SYNC-STRUCTURE.md). Read it before touching `.kimi-code/`, `.claude/`, or `.gemini/`.

## Crew docs

- `.kimi-code/AGENTS.md` / `.claude/CLAUDE.md` / `.gemini/GEMINI.md` — stack, conventions, workflow, delegation policy (authoritative crew contract)
- `.kimi-code/SKILLFLOW.md` / `.claude/SKILLFLOW.md` / `.gemini/antigravity/SKILLFLOW.md` — phase graph and skill output contract
- `BACKLOG/` — feature tracking index (`BACKLOG.md`) and per-feature folders
