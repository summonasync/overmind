# Bob.Kit-Uncensored

> One agent crew. Four runtimes. Zero corporate polish.

Single source of truth for one AI agent crew running on four runtimes: **Kimi Code CLI**, **Claude Code**, **Qwen Code**, and **OpenAI Codex CLI**. The repo holds all mirrors side by side; `.kimi-code/` (incl. `AGENTS.md`) serves Kimi Code CLI, `.claude/` (incl. `CLAUDE.md`) serves Claude Code, `.qwen/` (incl. `QWEN.md`) serves Qwen Code, `.codex/` (incl. `AGENTS.md`) serves Codex CLI. Content (rules, workflows, skills, agents) is semantically identical across the four; only tool-specific details (model names, home paths, tool identifiers, runtime features, enforcement mechanics) differ.

The crew: orchestrators, coders, reviewers, architects, a product strategist (`product-strategist`), an audit machine, an AI-trace obfuscator, a CI self-healer (`pipeline-doctor`), an overmind ceremony master (`overmind-ceremony`) and a naming sensei (`naming-sensei`) — 35 agents, 43 skills, one workflow graph.

## Prerequisites

- **Kimi Code CLI** (v2 engine).
- **Overmind MCP server — REQUIRED** in every runtime you use. The crew's planning ceremonies (feature board, spec/plan/task artifacts, validation gates, sub-agent briefing) run exclusively through overmind MCP tools; without them the pipeline stops (hard requirement — see `overmind-ceremony` skill). MCP tool names are identical across runtimes.
- **Claude Code**.
- **Qwen Code** — skills deploy to `~/.qwen/skills/`, agents to `~/.qwen/agents/`, rules to `~/.qwen/QWEN.md` (all native Qwen Code locations). Sub-agent models are pinned in agent frontmatter to `qwen3.7-plus` (execution/review) and `qwen3.8-max` (orchestration, audit, detection, obfuscation); both resolve through `modelProviders.openai` in `~/.qwen/settings.json` (ModelStudio Token Plan). If your provider lineup differs, re-point the pins — see `SYNC-STRUCTURE.md` checklist item 6.
- **OpenAI Codex CLI** — skills deploy to `~/.codex/skills/`, sub-agent roles to `~/.codex/agents/` (verified format: standalone TOML with exactly `name`, `description`, `model`, `developer_instructions` — the loader is strict, an extra field silently drops the role), rules to `~/.codex/AGENTS.md`. Requires codex-cli with skills + custom-agent support (verified on 0.160.1). Sub-agent model tiers are pinned per role: `gpt-6-luna` for the fixer and language coders, `gpt-6.1-sol` for everything else; complex coder tasks escalate the same role via `spawn_agent(model="gpt-6.1-sol", fork_turns="none")` — sanctioned by `.codex/AGENTS.md`. The kit never writes `~/.codex/config.toml`; MCP servers (overmind, squeezr) are registered separately (`codex mcp add` / vendor installers).

## Layout

```
.
├── SYNC-STRUCTURE.md      # quadruple-edit contract: file mapping, translation tables, deploy modes, checklist
├── KIT-MEMBERSHIP.md      # kit-membership.json: shape + which kit owns which content item
├── kit-membership.json    # six-kit membership map, read by the bundle pipeline (feature 250)
├── manifest.json          # deploy manifest: single source of truth for install/uninstall scripts
├── tools/                 # kit maintenance: gen-kit-membership.py, check-kit-membership.py
├── install.ps1            # deploy to tool homes (Windows); -DryRun prints plan only
├── install.sh             # deploy to tool homes (Git Bash/Linux); --dry-run prints plan only
├── uninstall.ps1          # remove deployed paths (Windows); -DryRun / -Force
├── uninstall.sh           # remove deployed paths (Git Bash/Linux); --dry-run / --force
├── .kimi-code/
│   ├── AGENTS.md          # Kimi-side crew contract (global tool memory)
│   ├── agents/            # 35 agent definitions
│   ├── skills/            # 43 skill directories
│   ├── SKILLFLOW.md       # workflow graph shared by all skills
│   ├── SOCIAL_DICTIONARY.md  # tone/slang vocabulary (Kimi home in repo; deployed to all four homes)
│   ├── WAKE-CHANNEL.md    # file-based wake drop recipe for the 190 tick (Kimi home in repo; deployed to all four homes)
│   └── config.toml        # Kimi runtime config (secondary-model pool) — Kimi only
├── .claude/
│   ├── CLAUDE.md          # Claude-side mirror of AGENTS.md
│   ├── agents/            # 35 agent definitions (Claude format)
│   ├── skills/            # 43 skill directories
│   └── SKILLFLOW.md
├── .qwen/
│   ├── QWEN.md            # Qwen-side mirror of AGENTS.md (deploys to ~/.qwen/)
│   ├── agents/            # 34 agent definitions (Qwen format: model pins + canonical tool ids)
│   ├── skills/            # 42 skill directories
│   ├── SKILLFLOW.md
│   ├── settings-snippet.jsonc  # Qwen-only permissions/hooks layer — merge by hand, never auto-installed
│   └── hooks/             # Qwen-only PostToolUse verification hook (.ps1 + .sh)
├── .codex/
│   ├── AGENTS.md          # Codex-side mirror of AGENTS.md (deploys to ~/.codex/)
│   ├── agents/            # 35 sub-agent roles (TOML: name/description/model/developer_instructions)
│   ├── skills/            # 43 skill directories
│   └── SKILLFLOW.md
└── BACKLOG/                 # per-project artifact tree, materialized by Overmind (read-only mirror)
```

## Install / update deployment to tool homes

Install scripts read `manifest.json` and copy repo content into the runtime home directories (`~/.kimi-code/`, `~/.claude/`, `~/.qwen/`, `~/.codex/`). A home that does not exist is skipped with a note. Both support a dry-run mode that prints the full copy plan without touching the filesystem:

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
| `.kimi-code/agents/`, `.kimi-code/skills/`, `.kimi-code/SKILLFLOW.md`, `.kimi-code/config.toml`, `.kimi-code/AGENTS.md` | `~/.kimi-code/` |
| `.claude/agents/`, `.claude/skills/`, `.claude/SKILLFLOW.md`, `.claude/CLAUDE.md` | `~/.claude/` |
| `.qwen/agents/`, `.qwen/skills/`, `.qwen/hooks/` (merge mode), `.qwen/QWEN.md`, `.qwen/SKILLFLOW.md`, `.qwen/settings-snippet.jsonc` | `~/.qwen/` |
| `.codex/agents/`, `.codex/skills/`, `.codex/AGENTS.md`, `.codex/SKILLFLOW.md` | `~/.codex/` |
| `.kimi-code/SOCIAL_DICTIONARY.md` | all four homes |

A project's `BACKLOG/` tree is never deployed — it is materialized by the Overmind MCP server from the board, per project.

### Deploy modes

Every `manifest.json` entry carries an explicit `mode`:

- **`mirror`** — whole-directory mirror (`robocopy /MIR` on Windows, `rsync -a --delete` on Unix). Deletes anything in the destination that is not in the source. Used for crew-owned directories.
- **`merge`** — each immediate child of the source is mirrored separately. Third-party content sitting next to it in the destination survives. Used for `~/.qwen/agents`, `~/.qwen/skills`, `~/.qwen/hooks`, because `~/.qwen/` also hosts non-crew content (e.g. `bailian-*` skills installed by `bl skill init`).

`merge` uninstall removes only the children the repo still has — a crew skill deleted from the repo leaves an orphan in the home until you remove it by hand.

> Bash note: `install.sh` uses `rsync -a --delete` when rsync is available (true mirror, like robocopy `/MIR`); without rsync it falls back to `cp -r`, which is **additive only** — stale files left in the home dirs are not deleted.

> Note: `install.*` / `uninstall.*` + `manifest.json` replaced the older `sync.ps1` / `sync.sh` (history: feature 001-dual-tool-repo-layout, in git history).

## Qwen Code enforcement layer (manual, one-time)

Qwen Code is the one runtime here with real config-level enforcement, so the crew ships a ready-made layer instead of relying on prompt text alone:

1. Run `install.*` — it deploys `.qwen/settings-snippet.jsonc` and `.qwen/hooks/` into `~/.qwen/`.
2. Open `~/.qwen/settings-snippet.jsonc` and merge the `permissions` and `hooks` blocks into your own `~/.qwen/settings.json` by hand. **The install scripts never write `settings.json`** — config self-modification is human-only per the crew contract.
3. On Linux/macOS/Git Bash, swap the hook entry's `command` to `$HOME/.qwen/hooks/post-edit-verify.sh` and its `shell` to `bash` (the bash variant needs `jq`; without it the hook fails open and stays silent).
4. Restart Qwen Code, then verify with `/permissions`.

What the layer enforces: destructive git and `.git/` deletion blocked outright; secret files (`.env*`, `*secrets*`, `*.pfx`, `*.key`) unreadable — including via shell (`cat .env` is blocked by the `Read` rule, there is no bypass to patch); `git commit`/`git push`, `rm -rf`, dependency installs and all cloud CLIs confirmation-gated; crew config (`settings.json`, `hooks/`, `QWEN.md`, `agents/`, `skills/`) not agent-writable; `dotnet build` after every `*.cs` edit and `npm run lint` + `tsc --noEmit` after frontend edits, with failures fed back to the model.

Both hook scripts **fail open**: no payload, unknown tool, no project anchor, missing toolchain or uninstalled dependencies exit silently rather than wedging your edits.

## Known issues

- **`~/.qwen/QWEN.md` is global memory for every project**: Qwen Code loads it in every session on this machine, exactly like `~/.claude/CLAUDE.md` does for Claude Code. The crew contract is deliberately opinionated (Russian-only conversation, .NET/Next.js defaults) — if that leaks into unrelated work, scope it down or move the stack sections into per-project `QWEN.md`.
- **This repo's `.qwen/` is also live project config**: Qwen Code reads `.qwen/skills/` and `.qwen/agents/` as *project-level* customizations, so working inside `Skills_dotNetCrew` activates all 42 skills and 34 agents for that session. Same as `.claude/` does for Claude Code — intended, but worth knowing when you wonder where a skill came from.
- **`WebSearch` has no Qwen Code equivalent**: agents that carried `WebSearch` + `FetchURL` on the Claude side collapse to a single `web_fetch` entry on the Qwen side. Capability loss is limited to search-engine queries; direct URL fetching is unchanged.
- **`settings-snippet.jsonc` path patterns are unverified end-to-end**: the `permissions` rule syntax follows the documented specifier forms, but the snippet has not been applied to a live `settings.json` and probed. Check `/permissions` after merging.
- **`~/.codex/skills/` and `~/.codex/agents/` are mirror-owned**: both deploy with `mirror` mode by crew decision (2026-10-07) — any third-party skill or role dropped there is deleted on the next `install.*`. Move non-crew content elsewhere before installing.
- **Codex roles are strict TOML**: the role loader accepts exactly `name`, `description`, `model`, `developer_instructions`; an unknown field silently drops the role (a warning appears at session start). If a spawned role "does not exist", check the file's fields first.
- **`gpt-6.1-sol` can be at capacity**: the priority pool occasionally rejects requests with "Selected model is at capacity". Sub-agent pins stay as-is; interactive fallback to `gpt-5.6-sol` (`codex exec -m ...`) is a session-level choice, not a pin change.

## Editing rule

Any change to crew behavior, agents, skills, or policies must be applied to **all four mirrors in the same commit**. File quadruples, allowed tool-specific divergences, the Kimi→Claude, Kimi→Qwen and Kimi→Codex translation tables, deploy modes, and the pre-commit grep checklist are defined in [SYNC-STRUCTURE.md](SYNC-STRUCTURE.md). Read it before touching `.kimi-code/`, `.claude/`, `.qwen/`, or `.codex/`.

## Crew docs

- `.kimi-code/AGENTS.md` / `.claude/CLAUDE.md` / `.qwen/QWEN.md` / `.codex/AGENTS.md` — stack, conventions, workflow, delegation policy (authoritative crew contract)
- `.kimi-code/SKILLFLOW.md` / `.claude/SKILLFLOW.md` / `.qwen/SKILLFLOW.md` / `.codex/SKILLFLOW.md` — phase graph and skill output contract
- `BACKLOG/` — per-project artifact tree materialized by Overmind (read-only; state lives on the Overmind board)
