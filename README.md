# Bob.Kit-Uncensored

> One agent crew. Two runtimes. Zero corporate polish.

Single source of truth for one AI agent crew running on two runtimes: **Kimi Code CLI** and **Claude Code**. The repo holds both mirrors side by side; `.kimi-code/` (incl. `AGENTS.md`) serves Kimi Code CLI, `.claude/` (incl. `CLAUDE.md`) serves Claude Code. Content (rules, workflows, skills, agents) is semantically identical across the two; only tool-specific details (model names, home paths, runtime features) differ.

The crew: orchestrators, coders, reviewers, architects, an audit machine, an AI-trace obfuscator, a CI self-healer (`pipeline-doctor`) and a naming maniac (`naming-helper`) — 20 agents, 22 skills, one workflow graph.

## Prerequisites

- **Kimi Code CLI** (v2 engine).
- **superpowers plugin for Kimi Code CLI — REQUIRED** at `~/.kimi-code/plugins/managed/superpowers`. The crew's workflow skills (brainstorming, writing-plans, TDD, systematic-debugging, verification gates) depend on it; without the plugin the pipeline breaks.
- **Claude Code**.
- **Superpowers plugin for Claude Code** — required for workflow parity on the Claude side (same skill set, different packaging).

## Layout

```
.
├── SYNC-STRUCTURE.md      # dual-edit contract: file mapping, translation table, checklist
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
│   ├── SOCIAL_DICTIONARY.md  # tone/slang vocabulary (Kimi home in repo; deployed to both homes)
│   └── config.toml        # Kimi runtime config (secondary-model pool) — Kimi only
├── .claude/
│   ├── CLAUDE.md          # Claude-side mirror of AGENTS.md
│   ├── agents/            # 20 agent definitions (Claude format)
│   ├── skills/            # 22 skill directories
│   └── SKILLFLOW.md
└── TASKS/                 # feature tracking — lives in repo root ONLY (templates are inside each tool dir)
```

## Install / update deployment to tool homes

Install scripts read `manifest.json` and copy repo content into the runtime home directories (`~/.kimi-code/`, `~/.claude/`). A home that does not exist is skipped with a note. Both support a dry-run mode that prints the full copy plan without touching the filesystem:

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
| `.kimi-code/agents/`, `.kimi-code/skills/`, `.kimi-code/TASKS/`, `.kimi-code/SKILLFLOW.md`, `.kimi-code/config.toml`, `.kimi-code/AGENTS.md` | `~/.kimi-code/` |
| `.claude/agents/`, `.claude/skills/`, `.claude/TASKS/`, `.claude/SKILLFLOW.md`, `.claude/CLAUDE.md` | `~/.claude/` |
| `.kimi-code/SOCIAL_DICTIONARY.md` | both homes |

`TASKS/` itself (feature folders, BACKLOG.md) is never deployed — only `.kimi-code/TASKS/` / `.claude/TASKS/` (templates) go to the homes.

> Bash note: `install.sh` uses `rsync -a --delete` when rsync is available (true mirror, like robocopy `/MIR`); without rsync it falls back to `cp -r`, which is **additive only** — stale files left in the home dirs are not deleted.

> Note: `install.*` / `uninstall.*` + `manifest.json` replaced the older `sync.ps1` / `sync.sh` (see `TASKS/001-dual-tool-repo-layout/`, task 05 log).

## Editing rule

Any change to crew behavior, agents, skills, or policies must be applied to **both mirrors in the same commit**. File pairs, allowed tool-specific divergences, the Kimi→Claude translation table, and the pre-commit grep checklist are defined in [SYNC-STRUCTURE.md](SYNC-STRUCTURE.md). Read it before touching `.kimi-code/` or `.claude/`.

## Crew docs

- `.kimi-code/AGENTS.md` / `.claude/CLAUDE.md` — stack, conventions, workflow, sub-agent policy (authoritative crew contract)
- `.kimi-code/SKILLFLOW.md` / `.claude/SKILLFLOW.md` — phase graph and skill output contract
- `TASKS/` — feature tracking index (`BACKLOG.md`) and per-feature folders
