# SYNC-STRUCTURE — Quadruple-Edit Contract

This repo maintains one agent crew for four runtimes: **Kimi Code CLI** (`.kimi-code/` incl. `AGENTS.md`), **Claude Code** (`.claude/` incl. `CLAUDE.md`), **Google Antigravity** (`.gemini/` incl. `GEMINI.md`, deployed to `~/.gemini` + `~/.gemini/antigravity`), and **Qwen Code** (`.qwen/` incl. `QWEN.md`, deployed to `~/.qwen`). All four sides must stay semantically identical.

## Core rule

**Edit one side — edit the other three in the same commit.** No asymmetric changes to crew behavior, policies, workflows, or skill logic. Divergence is allowed ONLY for tool-specific concerns:

- model names, aliases, and selection mechanics (Kimi `K2.7`/`K3` pool vs Claude `sonnet`/`opus` vs Antigravity role-based choice in Agent Manager vs Qwen `qwen3.7-plus`/`qwen3.8-max` pins)
- home directory paths (`~/.kimi-code/` vs `~/.claude/` vs `~/.gemini/` + `~/.gemini/antigravity/` vs `~/.qwen/`)
- tool identifier names inside agent `tools:` allowlists (Claude Code `Read`/`Grep`/`Glob`/`Bash`/`WebSearch`/`FetchURL` vs canonical Qwen `read_file`/`grep_search`/`glob`/`run_shell_command`/`web_fetch`) — the *restriction* is mirrored, only the spelling follows the runtime
- runtime-specific features (Kimi `AgentSwarm` + config.toml secondary-model pool; Claude Task tool + settings.json; Antigravity Agent Manager + UI permission settings; Qwen `agent` tool + `subagent_type: "fork"` + settings.json `permissions`/`hooks`)
- enforcement mechanics (Kimi hooks/config, Claude prompt-enforced, Antigravity behavioral rules + IDE permission settings, Qwen `permissions.deny`/`ask` + `PostToolUse` hooks in `~/.qwen/settings.json`)
- Overmind MCP is the planning backend on all four sides — identical tool names (`mcp__overmind__*`), hard requirement everywhere; on Antigravity the MCP setup is manual/unverified (see `overmind-ceremony`)

Everything else — rules, workflow phases, review gates, delegation policy, conventions — is mirrored verbatim.

## Frontmatter convention (all four sides)

A single-line `description:` / `whenToUse:` value that contains `: ` (colon + space) is **invalid YAML** — the value parses as a nested mapping and strict parsers reject the whole frontmatter block. Qwen Code then falls back to a naive line parser and logs a `YAML_PARSER` warning on every session start.

**Rule: wrap such a value in double quotes.** Double quotes, not single quotes and not a `>-`/`|-` block scalar:

- double quotes are valid YAML *and* are correctly unquoted by naive fallback parsers (Qwen's `parseValue` strips `"` and unescapes `\"`/`\\`); single quotes are not handled there
- a block scalar splits the value over two lines, which line-oriented frontmatter parsers in other runtimes may mishandle
- keep it one line so the diff stays one line per file

Escaping: `"` → `\"`, and never write a bare `\` inside a double-quoted value (`C:\foo` becomes an escape sequence) — switch that value to a `>-` block scalar instead. Multi-line `description: |` block scalars (used by all crew skills) are already valid and need no quotes.

Applies to every side. As of this writing the affected set is 8 agents — `architect-review` (`review: hard` in backticks), `dotnet-architect`, `dotnet-code-reviewer`, `dotnet-init`, `frontend-designer`, `next-js-architect`, `next-js-code-reviewer`, `feature-orchestrator` — quoted in the `description:` field on the Claude/Qwen sides and in `whenToUse:` for `architect-review` on the Kimi/Antigravity sides, where the description is not folded.

## File mapping

| Kimi side | Claude side | Antigravity side | Qwen side |
|-----------|-------------|------------------|-----------|
| `.kimi-code/AGENTS.md` | `.claude/CLAUDE.md` | `.gemini/GEMINI.md` | `.qwen/QWEN.md` |
| `.kimi-code/SKILLFLOW.md` | `.claude/SKILLFLOW.md` | `.gemini/antigravity/SKILLFLOW.md` | `.qwen/SKILLFLOW.md` |
| `.kimi-code/agents/ai-code-detector.md` | `.claude/agents/ai-code-detector.md` | `.gemini/antigravity/agents/ai-code-detector.md` | `.qwen/agents/ai-code-detector.md` |
| `.kimi-code/agents/ai-obfuscation-reviewer.md` | `.claude/agents/ai-obfuscation-reviewer.md` | `.gemini/antigravity/agents/ai-obfuscation-reviewer.md` | `.qwen/agents/ai-obfuscation-reviewer.md` |
| `.kimi-code/agents/ai-obfuscator.md` | `.claude/agents/ai-obfuscator.md` | `.gemini/antigravity/agents/ai-obfuscator.md` | `.qwen/agents/ai-obfuscator.md` |
| `.kimi-code/agents/architect-review.md` | `.claude/agents/architect-review.md` | `.gemini/antigravity/agents/architect-review.md` | `.qwen/agents/architect-review.md` |
| `.kimi-code/agents/audit-orchestrator.md` | `.claude/agents/audit-orchestrator.md` | `.gemini/antigravity/agents/audit-orchestrator.md` | `.qwen/agents/audit-orchestrator.md` |
| `.kimi-code/agents/coding-challenge-orchestrator.md` | `.claude/agents/coding-challenge-orchestrator.md` | `.gemini/antigravity/agents/coding-challenge-orchestrator.md` | `.qwen/agents/coding-challenge-orchestrator.md` |
| `.kimi-code/agents/documentation-writer.md` | `.claude/agents/documentation-writer.md` | `.gemini/antigravity/agents/documentation-writer.md` | `.qwen/agents/documentation-writer.md` |
| `.kimi-code/agents/dotnet-architect.md` | `.claude/agents/dotnet-architect.md` | `.gemini/antigravity/agents/dotnet-architect.md` | `.qwen/agents/dotnet-architect.md` |
| `.kimi-code/agents/dotnet-code-reviewer.md` | `.claude/agents/dotnet-code-reviewer.md` | `.gemini/antigravity/agents/dotnet-code-reviewer.md` | `.qwen/agents/dotnet-code-reviewer.md` |
| `.kimi-code/agents/dotnet-coder.md` | `.claude/agents/dotnet-coder.md` | `.gemini/antigravity/agents/dotnet-coder.md` | `.qwen/agents/dotnet-coder.md` |
| `.kimi-code/agents/dotnet-init.md` | `.claude/agents/dotnet-init.md` | `.gemini/antigravity/agents/dotnet-init.md` | `.qwen/agents/dotnet-init.md` |
| `.kimi-code/agents/fast-fixer-coder.md` | `.claude/agents/fast-fixer-coder.md` | `.gemini/antigravity/agents/fast-fixer-coder.md` | `.qwen/agents/fast-fixer-coder.md` |
| `.kimi-code/agents/frontend-designer.md` | `.claude/agents/frontend-designer.md` | `.gemini/antigravity/agents/frontend-designer.md` | `.qwen/agents/frontend-designer.md` |
| `.kimi-code/agents/maui-coder.md` | `.claude/agents/maui-coder.md` | `.gemini/antigravity/agents/maui-coder.md` | `.qwen/agents/maui-coder.md` |
| `.kimi-code/agents/naming-helper.md` | `.claude/agents/naming-helper.md` | `.gemini/antigravity/agents/naming-helper.md` | `.qwen/agents/naming-helper.md` |
| `.kimi-code/agents/next-js-architect.md` | `.claude/agents/next-js-architect.md` | `.gemini/antigravity/agents/next-js-architect.md` | `.qwen/agents/next-js-architect.md` |
| `.kimi-code/agents/next-js-code-reviewer.md` | `.claude/agents/next-js-code-reviewer.md` | `.gemini/antigravity/agents/next-js-code-reviewer.md` | `.qwen/agents/next-js-code-reviewer.md` |
| `.kimi-code/agents/next-js-coder.md` | `.claude/agents/next-js-coder.md` | `.gemini/antigravity/agents/next-js-coder.md` | `.qwen/agents/next-js-coder.md` |
| `.kimi-code/agents/overmind-ceremony.md` | `.claude/agents/overmind-ceremony.md` | `.gemini/antigravity/agents/overmind-ceremony.md` | `.qwen/agents/overmind-ceremony.md` |
| `.kimi-code/agents/pipeline-doctor.md` | `.claude/agents/pipeline-doctor.md` | `.gemini/antigravity/agents/pipeline-doctor.md` | `.qwen/agents/pipeline-doctor.md` |
| `.kimi-code/agents/feature-orchestrator.md` | `.claude/agents/feature-orchestrator.md` | `.gemini/antigravity/agents/feature-orchestrator.md` | `.qwen/agents/feature-orchestrator.md` |
| `.kimi-code/agents/sprint-planner.md` | `.claude/agents/sprint-planner.md` | `.gemini/antigravity/agents/sprint-planner.md` | `.qwen/agents/sprint-planner.md` |
| `.kimi-code/agents/tutor-master.md` | `.claude/agents/tutor-master.md` | `.gemini/antigravity/agents/tutor-master.md` | `.qwen/agents/tutor-master.md` |
| `.kimi-code/skills/ai-code-detector/` | `.claude/skills/ai-code-detector/` | `.gemini/antigravity/skills/ai-code-detector/` | `.qwen/skills/ai-code-detector/` |
| `.kimi-code/skills/ai-obfuscation-reviewer/` | `.claude/skills/ai-obfuscation-reviewer/` | `.gemini/antigravity/skills/ai-obfuscation-reviewer/` | `.qwen/skills/ai-obfuscation-reviewer/` |
| `.kimi-code/skills/ai-obfuscator/` | `.claude/skills/ai-obfuscator/` | `.gemini/antigravity/skills/ai-obfuscator/` | `.qwen/skills/ai-obfuscator/` |
| `.kimi-code/skills/architect-review/` | `.claude/skills/architect-review/` | `.gemini/antigravity/skills/architect-review/` | `.qwen/skills/architect-review/` |
| `.kimi-code/skills/audit-orchestrator/` | `.claude/skills/audit-orchestrator/` | `.gemini/antigravity/skills/audit-orchestrator/` | `.qwen/skills/audit-orchestrator/` |
| `.kimi-code/skills/coding-challenge-orchestrator/` | `.claude/skills/coding-challenge-orchestrator/` | `.gemini/antigravity/skills/coding-challenge-orchestrator/` | `.qwen/skills/coding-challenge-orchestrator/` |
| `.kimi-code/skills/documentation-writer/` | `.claude/skills/documentation-writer/` | `.gemini/antigravity/skills/documentation-writer/` | `.qwen/skills/documentation-writer/` |
| `.kimi-code/skills/dotnet-architect/` | `.claude/skills/dotnet-architect/` | `.gemini/antigravity/skills/dotnet-architect/` | `.qwen/skills/dotnet-architect/` |
| `.kimi-code/skills/dotnet-best-practices/` | `.claude/skills/dotnet-best-practices/` | `.gemini/antigravity/skills/dotnet-best-practices/` | `.qwen/skills/dotnet-best-practices/` |
| `.kimi-code/skills/dotnet-code-reviewer/` | `.claude/skills/dotnet-code-reviewer/` | `.gemini/antigravity/skills/dotnet-code-reviewer/` | `.qwen/skills/dotnet-code-reviewer/` |
| `.kimi-code/skills/dotnet-coder/` | `.claude/skills/dotnet-coder/` | `.gemini/antigravity/skills/dotnet-coder/` | `.qwen/skills/dotnet-coder/` |
| `.kimi-code/skills/dotnet-init/` | `.claude/skills/dotnet-init/` | `.gemini/antigravity/skills/dotnet-init/` | `.qwen/skills/dotnet-init/` |
| `.kimi-code/skills/fast-fixer-coder/` | `.claude/skills/fast-fixer-coder/` | `.gemini/antigravity/skills/fast-fixer-coder/` | `.qwen/skills/fast-fixer-coder/` |
| `.kimi-code/skills/frontend-designer/` | `.claude/skills/frontend-designer/` | `.gemini/antigravity/skills/frontend-designer/` | `.qwen/skills/frontend-designer/` |
| `.kimi-code/skills/maui-coder/` | `.claude/skills/maui-coder/` | `.gemini/antigravity/skills/maui-coder/` | `.qwen/skills/maui-coder/` |
| `.kimi-code/skills/naming-helper/` | `.claude/skills/naming-helper/` | `.gemini/antigravity/skills/naming-helper/` | `.qwen/skills/naming-helper/` |
| `.kimi-code/skills/next-js-architect/` | `.claude/skills/next-js-architect/` | `.gemini/antigravity/skills/next-js-architect/` | `.qwen/skills/next-js-architect/` |
| `.kimi-code/skills/next-js-code-reviewer/` | `.claude/skills/next-js-code-reviewer/` | `.gemini/antigravity/skills/next-js-code-reviewer/` | `.qwen/skills/next-js-code-reviewer/` |
| `.kimi-code/skills/next-js-coder/` | `.claude/skills/next-js-coder/` | `.gemini/antigravity/skills/next-js-coder/` | `.qwen/skills/next-js-coder/` |
| `.kimi-code/skills/nuget-licensing-guard/` | `.claude/skills/nuget-licensing-guard/` | `.gemini/antigravity/skills/nuget-licensing-guard/` | `.qwen/skills/nuget-licensing-guard/` |
| `.kimi-code/skills/overmind-ceremony/` | `.claude/skills/overmind-ceremony/` | `.gemini/antigravity/skills/overmind-ceremony/` | `.qwen/skills/overmind-ceremony/` |
| `.kimi-code/skills/pipeline-doctor/` | `.claude/skills/pipeline-doctor/` | `.gemini/antigravity/skills/pipeline-doctor/` | `.qwen/skills/pipeline-doctor/` |
| `.kimi-code/skills/squeezr-ops/` | `.claude/skills/squeezr-ops/` | `.gemini/antigravity/skills/squeezr-ops/` | `.qwen/skills/squeezr-ops/` |
| `.kimi-code/skills/feature-orchestrator/` | `.claude/skills/feature-orchestrator/` | `.gemini/antigravity/skills/feature-orchestrator/` | `.qwen/skills/feature-orchestrator/` |
| `.kimi-code/skills/sprint-planner/` | `.claude/skills/sprint-planner/` | `.gemini/antigravity/skills/sprint-planner/` | `.qwen/skills/sprint-planner/` |
| `.kimi-code/skills/tutor-master/` | `.claude/skills/tutor-master/` | `.gemini/antigravity/skills/tutor-master/` | `.qwen/skills/tutor-master/` |

## Non-paired entries

| Entry | Location | Rule |
|-------|----------|------|
| `.kimi-code/BACKLOG/_TEMPLATE/` + `.kimi-code/TASKS/_TEMPLATE/` | <removed 2026-09-24 — artifact format is owned by Overmind materialization; BACKLOG deploy entries removed from manifest.json> | Planning state lives on the Overmind board; `BACKLOG/` in project roots is a read-only materialized mirror, never deployed by this repo |
| `.kimi-code/SOCIAL_DICTIONARY.md` | `.kimi-code/` only in repo | Referenced by all sides via path. The install script copies it into all four tool homes (`~/.kimi-code/`, `~/.claude/`, `~/.gemini/`, `~/.qwen/`). |
| `.kimi-code/config.toml` | `.kimi-code/` only | Kimi-only (secondary-model pool). Claude analog is `~/.claude/settings.json`; Antigravity analog is UI permission settings; Qwen analog is `~/.qwen/settings.json` — NOT content-synced; each side's runtime config is managed independently. |
| `.qwen/settings-snippet.jsonc` | `.qwen/` only | Qwen-only. Merge-ready `permissions` + `hooks` layer, deployed to `~/.qwen/` as a **reference copy**. It is NEVER installed as `settings.json` — merging is a human action (config self-modification is human-only per the crew contract). |
| `.qwen/hooks/post-edit-verify.ps1` / `.sh` | `.qwen/` only | Qwen-only. `PostToolUse` verification hook (dotnet build after `*.cs`, lint + `tsc --noEmit` after frontend edits). Fail-open by design. Referenced by the settings snippet; Windows entry is active, the bash variant is a documented swap. |
| `manifest.json` + `install.ps1` / `install.sh` / `uninstall.ps1` / `uninstall.sh` | repo root | Deployment scripts driven by `manifest.json` (single source of truth for the deploy set). Not mirrored to each other — they are per-OS entry points to the same logic. Every entry carries an explicit `mode` (see Deploy modes below). |

## Known intentional differences

| Aspect | Kimi side | Claude side | Antigravity side | Qwen side |
|--------|-----------|-------------|------------------|-----------|
| Sub-agent allowlists | `agents:` / model-pool restrictions enforced via config.toml + agent bodies | `subagents:` frontmatter DROPPED — unsupported key; policy in skill bodies, prompt-enforced | Policy in GEMINI.md Delegation Rules + skill bodies, prompt-enforced; delegation via Agent Manager | `subagents:`/`whenToUse:` DROPPED — unsupported keys; `tools:` allowlists KEPT (Qwen supports them) with canonical Qwen tool names; delegation policy in QWEN.md + skill bodies |
| Model pool enforcement | `[secondary_model]` in config.toml limits spawnable models | No config-level pool; policy text in CLAUDE.md + skill bodies | No fixed pool; role-based wording (strong execution / strongest reasoning model), model picked in Agent Manager | `model:` frontmatter pin per agent (`qwen3.7-plus` / `qwen3.8-max`), resolved through `modelProviders.openai` in settings.json; `inherit` forbidden for crew sub-agents |
| Parallel spawn primitive | `AgentSwarm` | Multiple `Task` tool calls in one message | Parallel background agents in Agent Manager | Multiple `agent` tool calls in one message; `subagent_type: "fork"` when the child needs parent context |
| Deterministic enforcement | Hooks (PreToolUse/PostToolUse) + config deny-list | Prompt-enforced only | Behavioral rules in GEMINI.md + IDE permission settings configured by the user | `permissions.deny`/`ask` + `PostToolUse` hooks in `~/.qwen/settings.json`, shipped as `.qwen/settings-snippet.jsonc` for hand-merging. Read/Edit deny rules also cover equivalent shell commands, so there is no `cat .env` bypass |
| Agent frontmatter extras | — | — | `whenToUse`, `tools`, `subagents` keys kept; unknown keys are ignored by the runtime | `tools` kept (canonical names); `whenToUse` folded into `description` (Qwen delegates on `description`) |
| Tool names in allowlists | Kimi tool ids | Claude Code tool ids (`Read`, `Grep`, `Glob`, `Bash`, `WebSearch`, `FetchURL`) | as authored | Canonical Qwen tool ids (`read_file`, `grep_search`, `glob`, `run_shell_command`, `web_fetch`). `WebSearch` has no Qwen equivalent — it and `FetchURL` collapse into a single `web_fetch` entry |

## Translation table (Kimi → Claude)

Apply strictly in this order (most specific first). Used when porting a Kimi-side change to the Claude side; reverse for the other direction.

| # | Find | Replace |
|---|------|---------|
| 1 | `~/.kimi-code/skills` | `~/.claude/skills` |
| 2 | `~/.kimi-code/` | `~/.claude/` |
| 3 | `kimi-code/kimi-for-coding-highspeed` | `haiku` |
| 4 | `kimi-code/kimi-for-coding` | `sonnet` |
| 5 | `kimi-code/kimi-k3` | `opus` |
| 6 | `kimi-code/k3-256k` | `opus` |
| 7 | `kimi-code/k3` | `opus` |
| 8 | `Kimi K2.7` | `Claude Sonnet` |
| 9 | `Kimi K3` | `Claude Opus` |
| 10 | `K2.7` | `Sonnet` |
| 11 | `K3` | `Opus` |
| 12 | `Kimi Code CLI` | `Claude Code` |
| 13 | `kimi-code` | `claude` (path/identifier contexts only — eyeball every hit) |
| 14 | `AgentSwarm` | `multiple Task tool calls in one message` |
| 15 | `Agent tool` / `` `Agent` `` | `Task tool` / `` `Task` `` |
| — | `mcp__overmind__*` / `planning_*` / `overmind_*` tool names | DO NOT TRANSLATE — MCP tool names are identical on every runtime |

## Translation table (Kimi → Antigravity)

Apply strictly in this order (most specific first). After the table, a manual pass is MANDATORY: hook sections become behavioral rules referencing Antigravity permission settings; sub-agent model pools become role-based wording; Kimi CLI flags/slash-commands are removed or mapped to Agent Manager equivalents.

| # | Find | Replace |
|---|------|---------|
| 1 | `~/.kimi-code/skills` | `~/.gemini/antigravity/skills` |
| 2 | `~/.kimi-code/` | `~/.gemini/antigravity/` |
| 3 | `kimi-code/kimi-for-coding-highspeed` | `fast execution model (selected in Agent Manager)` |
| 4 | `kimi-code/kimi-for-coding` | `strong execution model (selected in Agent Manager)` |
| 5 | `kimi-code/kimi-k3` / `kimi-code/k3-256k` | `strongest reasoning model (selected in Agent Manager)` |
| 6 | `Kimi K2.7` | `strong execution model` |
| 7 | `Kimi K3` | `strongest reasoning model` |
| 8 | `AgentSwarm` | `parallel background agents (Agent Manager)` |
| 9 | `config.toml` | `Antigravity permission settings` |
| 10 | `Kimi Code CLI` | `Google Antigravity` |
| 11 | `Kimi Code` | `Antigravity` |
| 12 | `~/.claude` / `sonnet` / `opus` / `haiku` (if present in source) | manual pass — replace with role-based wording |
| — | `mcp__overmind__*` / `planning_*` / `overmind_*` tool names | DO NOT TRANSLATE — MCP tool names are identical on every runtime |

## Translation table (Kimi → Qwen)

Apply strictly in this order (most specific first). After the table, a manual pass is MANDATORY: hook/config sections become `permissions` + `hooks` in `~/.qwen/settings.json` (point at `.qwen/settings-snippet.jsonc`); agent frontmatter tool names become canonical Qwen ids.

| # | Find | Replace |
|---|------|---------|
| 1 | `~/.kimi-code/skills` | `~/.qwen/skills` |
| 2 | `~/.kimi-code/` | `~/.qwen/` |
| 3 | `kimi-code/kimi-for-coding-highspeed` / `kimi-code/kimi-for-coding` | `qwen3.7-plus` |
| 4 | `kimi-code/kimi-k3` / `kimi-code/k3-256k` / `kimi-code/k3` | `qwen3.8-max` |
| 5 | `Kimi K2.7` / `K2.7` | `qwen3.7-plus` |
| 6 | `Kimi K3` / `K3` | `qwen3.8-max` |
| 7 | `Kimi Code CLI` / `Kimi Code` | `Qwen Code` |
| 8 | `kimi-code` | `qwen` (path/identifier contexts only — eyeball every hit) |
| 9 | `AgentSwarm` | `multiple agent tool calls in one message` |
| 10 | `config.toml` | `~/.qwen/settings.json` (crew ships `.qwen/settings-snippet.jsonc` to merge by hand) |
| 11 | agent frontmatter `Read` / `Grep` / `Glob` / `Bash` / `WebSearch` / `FetchURL` | `read_file` / `grep_search` / `glob` / `run_shell_command` / `web_fetch` / `web_fetch` — then dedupe (`WebSearch` has no Qwen equivalent, it collapses into `web_fetch`) |
| 12 | `AGENTS.md` **as the name of the crew contract file** (global-context header, repo-layout line) | `QWEN.md`. Leave `AGENTS.md` alone where skill bodies cite it generically as the crew contract — all four sides do that identically, and Qwen Code reads a project `AGENTS.md` natively |
| 13 | `~/.claude` / `sonnet` / `opus` / `haiku` (if porting from the Claude side instead) | `~/.qwen` / `qwen3.7-plus` / `qwen3.8-max` |
| — | `mcp__overmind__*` / `planning_*` / `overmind_*` tool names | DO NOT TRANSLATE — MCP tool names are identical on every runtime |

Porting from the **Claude side** is the shortest path for agents and skills: only rows 11–13 plus `~/.claude/skills` → `~/.qwen/skills` apply.

## Deploy modes (`manifest.json`)

Every manifest entry carries an explicit `mode` — the `install.sh`/`uninstall.sh` no-python fallback groups fields positionally, so a missing `mode` desynchronises the whole list.

| Mode | Install | Uninstall | Use for |
|------|---------|-----------|---------|
| `mirror` | whole-directory mirror: `robocopy /MIR` (Windows) / `rsync -a --delete` (Unix) | removes the deployed directory | crew-owned destinations (`~/.kimi-code/skills`, `~/.claude/agents`, …) |
| `merge` | each immediate child mirrored separately | removes only the children the repo still has | shared destinations that also hold third-party content — `~/.qwen/skills`, `~/.qwen/agents`, `~/.qwen/hooks` |

`merge` exists because `~/.qwen/` hosts non-crew content (e.g. `bailian-*` skills installed by `bl skill init`) that a whole-directory mirror would delete. Known limitation: `merge` uninstall only removes children that still exist in the repo — a crew skill deleted from the repo leaves an orphan in the home until removed by hand.

## Skill authoring rules

Applies to creating or editing any skill in this repo. The verification half lives in the pre-commit checklist below — this is the authoring half.

- The frontmatter `description` is a TRIGGER, not a summary: name the situations and the user phrasings that should fire the skill. If no real user sentence would match it, rewrite the description, not the docs.
- One skill, one job. The moment a draft grows an "and also", split it — the crew prefers many small skills over God-skills.
- Write for a cold reader: exact tool names, exact commands, exact file paths. "As appropriate" inside a skill body is a bug.
- Anti-scope is as load-bearing as scope: state what the skill refuses and where it hands off.
- A skill is done when it passes the pre-commit checklist below and, where practical, has been dogfooded — the feature that adds a skill exercises it at least once.

## Pre-commit checklist (run before committing any crew edit)

1. All four sides of every touched quadruple updated in the same commit.
2. No cross-runtime leakage. **Known benign exceptions:** `skills/coding-challenge-orchestrator/SKILL.md` names every runtime on purpose (its AI-leakage deny-list must stay identical on all four sides); `skills/overmind-ceremony/SKILL.md` carries the verbatim sentence "Antigravity runtime: overmind MCP is expected but not verified" on all four sides (a cross-runtime fact); `skills/squeezr-ops/SKILL.md` is a product doc that names several runtimes on purpose (a Claude settings example in its hook section, qwen/kimi client notes) and is identical on all four sides. Filter all three out of every grep below.
   ```bash
   grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|config\.toml\|sonnet\|opus\|haiku\|\.claude\|qwen3\.' .gemini/ | grep -v coding-challenge-orchestrator | grep -v overmind-ceremony | grep -v squeezr   # must be empty
   grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|qwen3\.\|\.qwen' .claude/                                     | grep -v coding-challenge-orchestrator | grep -v overmind-ceremony | grep -v squeezr   # must be empty
   grep -rni 'claude code\|sonnet\|opus\|haiku\|qwen3\.\|\.qwen' .kimi-code/                                 | grep -v coding-challenge-orchestrator | grep -v overmind-ceremony | grep -v squeezr   # must be empty
   grep -rni 'k2\.7\|\bk3\b\|kimi\|agentswarm\|sonnet\|opus\|haiku\|\.claude\|\.gemini\|antigravity' .qwen/  | grep -v coding-challenge-orchestrator | grep -v overmind-ceremony | grep -v squeezr   # must be empty
   grep -rni 'gemini\|antigravity\|qwen' .kimi-code/ .claude/                                                | grep -v coding-challenge-orchestrator | grep -v overmind-ceremony | grep -v squeezr   # must be empty
   ```
   ripgrep-based tooling skips dot-directories by default — pass the runtime dir explicitly, or add `--hidden`.
3. `Known intentional differences` table still accurate — if a runtime gains/loses a capability, update this file, not just one side.
4. Count parity intact: 23 agent files + 26 skill dirs on each of the four sides.
   ```bash
   for d in .kimi-code .claude .gemini/antigravity .qwen; do printf '%-22s agents=%s skills=%s\n' "$d" "$(ls "$d/agents" | wc -l)" "$(ls "$d/skills" | wc -l)"; done
   ```
5. Deploy set still consistent: `manifest.json` parses, both parsers agree, and dry runs are clean.
   ```bash
   powershell -File install.ps1 -DryRun      # Windows
   ./install.sh --dry-run                    # Unix / Git Bash
   powershell -File uninstall.ps1 -DryRun
   ./uninstall.sh --dry-run
   ```
6. Qwen model pins still exist in the configured provider — if the ModelStudio lineup changed, `.qwen/agents/*.md` `model:` values, QWEN.md «Sub-Agent Rules» and row 5–8 of the Kimi→Qwen table are updated together.
7. Frontmatter stays valid YAML on all four sides (see «Frontmatter convention») — an unquoted `: ` in a single-line value is a silent regression:
   ```bash
   grep -rnE '^(description|whenToUse): [^"|>-].*: ' .kimi-code/agents .claude/agents .gemini/antigravity/agents .qwen/agents   # must be empty
   ```
