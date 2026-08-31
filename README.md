# bob.ai .NET Coding Agents Crew

Single source of truth for one AI agent crew running on two runtimes: **Kimi Code CLI** and **Claude Code**. The repo holds both mirrors side by side; `.kimi-code/` (incl. `AGENTS.md`) serves Kimi Code CLI, `.claude/` (incl. `CLAUDE.md`) serves Claude Code. Content (rules, workflows, skills, agents) is semantically identical across the two; only tool-specific details (model names, home paths, runtime features) differ.

## Prerequisites

- **Kimi Code CLI** (v2 engine).
- **superpowers plugin for Kimi Code CLI — REQUIRED** at `~/.kimi-code/plugins/managed/superpowers`. The crew's workflow skills (brainstorming, writing-plans, TDD, systematic-debugging, verification gates) depend on it; without the plugin the pipeline breaks.
- **Claude Code**.
- **Superpowers plugin for Claude Code** — required for workflow parity on the Claude side (same skill set, different packaging).

## Layout

```
.
├── SYNC-STRUCTURE.md      # dual-edit contract: file mapping, translation table, checklist
├── sync.ps1               # deploy to tool homes (Windows); -DryRun prints plan only
├── sync.sh                # deploy to tool homes (Git Bash/Linux); --dry-run prints plan only
├── .kimi-code/
│   ├── AGENTS.md          # Kimi-side crew contract (global tool memory)
│   ├── agents/            # 18 agent definitions
│   ├── skills/            # 20 skill directories
│   ├── SKILLFLOW.md       # workflow graph shared by all skills
│   ├── SOCIAL_DICTIONARY.md  # tone/slang vocabulary (Kimi home in repo; deployed to both homes)
│   └── config.toml        # Kimi runtime config (secondary-model pool) — Kimi only
├── .claude/
│   ├── CLAUDE.md          # Claude-side mirror of AGENTS.md
│   ├── agents/            # 18 agent definitions (Claude format)
│   ├── skills/            # 20 skill directories
│   └── SKILLFLOW.md
└── TASKS/                 # feature tracking — lives in repo root ONLY
    └── _TEMPLATE/         # task-file templates; the only TASKS/ part deployed to tool homes
```

## Install / update deployment to tool homes

Sync scripts copy repo content into the runtime home directories (`~/.kimi-code/`, `~/.claude/`). Both support a dry-run mode that prints the full copy plan without touching the filesystem:

```powershell
powershell -File sync.ps1 -DryRun    # plan only
powershell -File sync.ps1            # apply
```

```bash
./sync.sh --dry-run                  # plan only
./sync.sh                            # apply
```

What goes where:

| Source (repo) | Destination |
|---------------|-------------|
| `.kimi-code/agents/`, `.kimi-code/skills/`, `.kimi-code/SKILLFLOW.md`, `.kimi-code/config.toml`, `.kimi-code/AGENTS.md` | `~/.kimi-code/` |
| `.claude/agents/`, `.claude/skills/`, `.claude/SKILLFLOW.md`, `.claude/CLAUDE.md` | `~/.claude/` |
| `.kimi-code/SOCIAL_DICTIONARY.md` | both homes |
| `TASKS/_TEMPLATE/` | both homes |

`TASKS/` itself (feature folders, BACKLOG.md) is never deployed — it is repo-local tracking.

> Note: `sync.ps1` / `sync.sh` are implemented (see `TASKS/001-dual-tool-repo-layout/`, task 05).

## Editing rule

Any change to crew behavior, agents, skills, or policies must be applied to **both mirrors in the same commit**. File pairs, allowed tool-specific divergences, the Kimi→Claude translation table, and the pre-commit grep checklist are defined in [SYNC-STRUCTURE.md](SYNC-STRUCTURE.md). Read it before touching `.kimi-code/` or `.claude/`.

## Crew docs

- `.kimi-code/AGENTS.md` / `.claude/CLAUDE.md` — stack, conventions, workflow, sub-agent policy (authoritative crew contract)
- `.kimi-code/SKILLFLOW.md` / `.claude/SKILLFLOW.md` — phase graph and skill output contract
- `TASKS/` — feature tracking index (`BACKLOG.md`) and per-feature folders
