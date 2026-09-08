# Bob.Kit-Uncensored

> One agent crew. Four runtimes. Zero corporate polish.

Single source of truth for one AI agent crew running on four runtimes: **Kimi Code CLI**, **Claude Code**, **Google Antigravity**, and **Qwen Code**. The repo holds all mirrors side by side; `.kimi-code/` (incl. `AGENTS.md`) serves Kimi Code CLI, `.claude/` (incl. `CLAUDE.md`) serves Claude Code, `.gemini/` (incl. `GEMINI.md`) serves Antigravity, `.qwen/` (incl. `QWEN.md`) serves Qwen Code. Content (rules, workflows, skills, agents) is semantically identical across the four; only tool-specific details (model names, home paths, tool identifiers, runtime features, enforcement mechanics) differ.

The crew: orchestrators, coders, reviewers, architects, an audit machine, an AI-trace obfuscator, a CI self-healer (`pipeline-doctor`) and a naming maniac (`naming-helper`) — 20 agents, 22 skills, one workflow graph.

## Prerequisites

- **Kimi Code CLI** (v2 engine).
- **superpowers plugin for Kimi Code CLI — REQUIRED** at `~/.kimi-code/plugins/managed/superpowers`. The crew's workflow skills (brainstorming, writing-plans, TDD, systematic-debugging, verification gates) depend on it; without the plugin the pipeline breaks.
- **Claude Code**.
- **Superpowers plugin for Claude Code** — required for workflow parity on the Claude side (same skill set, different packaging).
- **Google Antigravity** — skills/agents deploy to `~/.gemini/antigravity/` (open `SKILL.md` standard, picked up natively); rules deploy as `~/.gemini/GEMINI.md`. Superpowers plugin is optional here — without it the crew follows `SKILLFLOW.md` transitions directly.
- **Qwen Code** — skills deploy to `~/.qwen/skills/`, agents to `~/.qwen/agents/`, rules to `~/.qwen/QWEN.md` (all native Qwen Code locations). Superpowers is optional here too — no Qwen Code port exists, so without it the crew follows `SKILLFLOW.md` transitions directly. Sub-agent models are pinned in agent frontmatter to `qwen3.7-plus` (execution/review) and `qwen3.8-max` (orchestration, audit, detection, obfuscation); both resolve through `modelProviders.openai` in `~/.qwen/settings.json` (ModelStudio Token Plan). If your provider lineup differs, re-point the pins — see `SYNC-STRUCTURE.md` checklist item 6.

## Layout

```
.
├── SYNC-STRUCTURE.md      # quadruple-edit contract: file mapping, translation tables, deploy modes, checklist
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
│   ├── SOCIAL_DICTIONARY.md  # tone/slang vocabulary (Kimi home in repo; deployed to all four homes)
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
├── .qwen/
│   ├── QWEN.md            # Qwen-side mirror of AGENTS.md (deploys to ~/.qwen/)
│   ├── agents/            # 20 agent definitions (Qwen format: model pins + canonical tool ids)
│   ├── skills/            # 22 skill directories
│   ├── SKILLFLOW.md
│   ├── settings-snippet.jsonc  # Qwen-only permissions/hooks layer — merge by hand, never auto-installed
│   └── hooks/             # Qwen-only PostToolUse verification hook (.ps1 + .sh)
└── BACKLOG/                 # feature tracking — lives in repo root ONLY (templates are inside each tool dir)
```

## Install / update deployment to tool homes

Install scripts read `manifest.json` and copy repo content into the runtime home directories (`~/.kimi-code/`, `~/.claude/`, `~/.gemini/`, `~/.gemini/antigravity/`, `~/.qwen/`). A home that does not exist is skipped with a note (for `antigravity`, an existing `~/.gemini/` is enough — the subdirectory is created on copy). Both support a dry-run mode that prints the full copy plan without touching the filesystem:

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
| `.qwen/agents/`, `.qwen/skills/`, `.qwen/hooks/` (merge mode), `.kimi-code/BACKLOG/` (templates), `.qwen/QWEN.md`, `.qwen/SKILLFLOW.md`, `.qwen/settings-snippet.jsonc` | `~/.qwen/` |
| `.kimi-code/SOCIAL_DICTIONARY.md` | all four homes |

`BACKLOG/` itself (feature folders, BACKLOG.md) is never deployed — only `.kimi-code/BACKLOG/` / `.claude/BACKLOG/` (templates) go to the homes.

### Deploy modes

Every `manifest.json` entry carries an explicit `mode`:

- **`mirror`** — whole-directory mirror (`robocopy /MIR` on Windows, `rsync -a --delete` on Unix). Deletes anything in the destination that is not in the source. Used for crew-owned directories.
- **`merge`** — each immediate child of the source is mirrored separately. Third-party content sitting next to it in the destination survives. Used for `~/.qwen/agents`, `~/.qwen/skills`, `~/.qwen/hooks`, because `~/.qwen/` also hosts non-crew content (e.g. `bailian-*` skills installed by `bl skill init`).

`merge` uninstall removes only the children the repo still has — a crew skill deleted from the repo leaves an orphan in the home until you remove it by hand.

> Bash note: `install.sh` uses `rsync -a --delete` when rsync is available (true mirror, like robocopy `/MIR`); without rsync it falls back to `cp -r`, which is **additive only** — stale files left in the home dirs are not deleted.

> Note: `install.*` / `uninstall.*` + `manifest.json` replaced the older `sync.ps1` / `sync.sh` (see `BACKLOG/001-dual-tool-repo-layout/`, task 05 log).

## Qwen Code enforcement layer (manual, one-time)

Qwen Code is the one runtime here with real config-level enforcement, so the crew ships a ready-made layer instead of relying on prompt text alone:

1. Run `install.*` — it deploys `.qwen/settings-snippet.jsonc` and `.qwen/hooks/` into `~/.qwen/`.
2. Open `~/.qwen/settings-snippet.jsonc` and merge the `permissions` and `hooks` blocks into your own `~/.qwen/settings.json` by hand. **The install scripts never write `settings.json`** — config self-modification is human-only per the crew contract.
3. On Linux/macOS/Git Bash, swap the hook entry's `command` to `$HOME/.qwen/hooks/post-edit-verify.sh` and its `shell` to `bash` (the bash variant needs `jq`; without it the hook fails open and stays silent).
4. Restart Qwen Code, then verify with `/permissions`.

What the layer enforces: destructive git and `.git/` deletion blocked outright; secret files (`.env*`, `*secrets*`, `*.pfx`, `*.key`) unreadable — including via shell (`cat .env` is blocked by the `Read` rule, there is no bypass to patch); `git commit`/`git push`, `rm -rf`, dependency installs and all cloud CLIs confirmation-gated; crew config (`settings.json`, `hooks/`, `QWEN.md`, `agents/`, `skills/`) not agent-writable; `dotnet build` after every `*.cs` edit and `npm run lint` + `tsc --noEmit` after frontend edits, with failures fed back to the model.

Both hook scripts **fail open**: no payload, unknown tool, no project anchor, missing toolchain or uninstalled dependencies exit silently rather than wedging your edits.

## Known issues

- **`~/.gemini/GEMINI.md` conflicts with the Gemini CLI**: both Antigravity and the Gemini CLI hardcode the same global config path and overwrite each other's rules (tracked in [google-gemini/gemini-cli#16058](https://github.com/google-gemini/gemini-cli/issues/16058)). If you use both tools, expect GEMINI.md edits from one side to leak into the other; re-run `install.*` to restore the crew version.
- **`~/.qwen/QWEN.md` is global memory for every project**: Qwen Code loads it in every session on this machine, exactly like `~/.claude/CLAUDE.md` does for Claude Code. The crew contract is deliberately opinionated (Russian-only conversation, .NET/Next.js defaults) — if that leaks into unrelated work, scope it down or move the stack sections into per-project `QWEN.md`.
- **This repo's `.qwen/` is also live project config**: Qwen Code reads `.qwen/skills/` and `.qwen/agents/` as *project-level* customizations, so working inside `Skills_dotNetCrew` activates all 22 skills and 20 agents for that session. Same as `.claude/` does for Claude Code — intended, but worth knowing when you wonder where a skill came from.
- **`WebSearch` has no Qwen Code equivalent**: agents that carried `WebSearch` + `FetchURL` on the Claude side collapse to a single `web_fetch` entry on the Qwen side. Capability loss is limited to search-engine queries; direct URL fetching is unchanged.
- **`settings-snippet.jsonc` path patterns are unverified end-to-end**: the `permissions` rule syntax follows the documented specifier forms, but the snippet has not been applied to a live `settings.json` and probed. Check `/permissions` after merging.

## Editing rule

Any change to crew behavior, agents, skills, or policies must be applied to **all four mirrors in the same commit**. File quadruples, allowed tool-specific divergences, the Kimi→Claude, Kimi→Antigravity and Kimi→Qwen translation tables, deploy modes, and the pre-commit grep checklist are defined in [SYNC-STRUCTURE.md](SYNC-STRUCTURE.md). Read it before touching `.kimi-code/`, `.claude/`, `.gemini/`, or `.qwen/`.

## Crew docs

- `.kimi-code/AGENTS.md` / `.claude/CLAUDE.md` / `.gemini/GEMINI.md` / `.qwen/QWEN.md` — stack, conventions, workflow, delegation policy (authoritative crew contract)
- `.kimi-code/SKILLFLOW.md` / `.claude/SKILLFLOW.md` / `.gemini/antigravity/SKILLFLOW.md` / `.qwen/SKILLFLOW.md` — phase graph and skill output contract
- `BACKLOG/` — feature tracking index (`BACKLOG.md`) and per-feature folders
